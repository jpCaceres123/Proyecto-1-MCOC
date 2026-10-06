using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public partial class CampusWorld
{
    public bool StructuralOnly { get; private set; }
    Transform beamLayer, slabLayer;
    readonly Dictionary<Renderer,bool> architectureRenderers=new Dictionary<Renderer,bool>();
    readonly Dictionary<Collider,bool> architectureColliders=new Dictionary<Collider,bool>();
    readonly Rect[] elevatorHoles=new Rect[2];
    readonly int[][] elevatorFloors=new int[2][];
    public int[] ElevatorFloors(int shaft){return elevatorFloors[Mathf.Clamp(shaft,0,1)];}

    void PrepareStructuralLayers()
    {
        beamLayer=new GameObject("Vigas_originales_IDS").transform;beamLayer.SetParent(structure,false);
        slabLayer=new GameObject("Losas_analiticas_IDS").transform;slabLayer.SetParent(transform,false);
        // Use the common intersection of the exported openings, not new analytical voids.
        Vector2[] seeds={new Vector2(5,4),new Vector2(5,11)};
        for(int shaft=0;shaft<2;shaft++) {
            var holes=slabs.Values.SelectMany(s=>s.holes).Where(h=>h.Contains(seeds[shaft])).ToArray();
            if(holes.Length==0)throw new InvalidOperationException("No se encuentra el hueco del ascensor "+(shaft+1));
            elevatorHoles[shaft]=Rect.MinMaxRect(holes.Max(h=>h.xMin),holes.Max(h=>h.yMin),holes.Min(h=>h.xMax),holes.Min(h=>h.yMax));
            elevatorFloors[shaft]=slabs.Values.Where(s=>s.holes.Any(h=>h.Contains(seeds[shaft])))
                .Select(s=>Mathf.RoundToInt(s.height/Storey)).Distinct().OrderBy(level=>level).ToArray();
            if(elevatorHoles[shaft].width<1.2f || elevatorHoles[shaft].height<1.2f)
                throw new InvalidOperationException("Hueco insuficiente para ascensor "+(shaft+1));
            Debug.Log("CAMPUS_LIFT_HOLE "+shaft+" "+elevatorHoles[shaft]);
        }
    }

    void StructuralSlabs()
    {
        foreach(var slab in slabs.Values) {
            var parts=new List<Rect>{slab.rect};
            foreach(var hole in slab.holes)parts=parts.SelectMany(r=>Cut(r,hole)).ToList();
            foreach(var part in parts) {
                // The CSV is a surface contract. 15 cm is the documented base slab thickness,
                // used only for display; no plate stiffness or loads are introduced.
                var go=Box("Losa_"+slab.id,new Vector3(part.center.x,slab.height-.075f,part.center.y),
                    new Vector3(part.width,.15f,part.height),concrete,false,slabLayer);
                var id=go.AddComponent<StructuralIdentity>();id.key="S:"+slab.id;id.description="Losa "+slab.id+" · superficie original";
            }
        }
    }

    void RememberArchitectureState()
    {
        foreach(var r in architecture.GetComponentsInChildren<Renderer>(true))architectureRenderers[r]=r.enabled;
        foreach(var c in architecture.GetComponentsInChildren<Collider>(true))architectureColliders[c]=c.enabled;
        slabLayer.gameObject.SetActive(false);
    }

    bool WalkingSupport(Collider c)
    {
        string n=c.gameObject.name.ToLowerInvariant();
        return n=="campus" || n.StartsWith("piso_") || n.Contains("terreno") || n.Contains("suelo") || n.Contains("talud") ||
            n.Contains("escalon") || n.Contains("pelda") || n.Contains("losa") || n.Contains("descanso") ||
            n.Contains("plataforma") || n.Contains("sendero") || n.Contains("paseo") || n.Contains("patio") ||
            n.Contains("ascensor") || n.Contains("rampa") || n.Contains("cancha") || n.EndsWith(" · colision");
    }

    public void SetStructuralOnly(bool value)
    {
        StructuralOnly=value;
        foreach(var item in architectureRenderers)if(item.Key)item.Key.enabled=!value && item.Value;
        foreach(var item in architectureColliders)if(item.Key)item.Key.enabled=item.Value && (!value || WalkingSupport(item.Key));
        beamLayer.gameObject.SetActive(!value);
        slabLayer.gameObject.SetActive(value);
        Physics.SyncTransforms();
    }
    // VR inspection includes beams; the existing F6 desktop option stays unchanged.
    public void SetVRStructuralOnly(bool value){SetStructuralOnly(value);beamLayer.gameObject.SetActive(true);}

    public Vector3 ElevatorArrival(int shaft,int level)
    {
        Rect hole=elevatorHoles[Mathf.Clamp(shaft,0,1)];
        float z=shaft==0?hole.yMax-.65f:hole.yMin+.65f;
        int available=ElevatorFloors(shaft).OrderBy(floor=>Mathf.Abs(floor-level)).First();
        return new Vector3(hole.center.x,available*Storey+Finish+.04f,z);
    }

    void InteriorElevators()
    {
        StructuralSlabs();
        for(int shaft=0;shaft<2;shaft++) {
            Rect h=elevatorHoles[shaft];float w=h.width-.24f,d=h.height-.24f;
            foreach(int level in ElevatorFloors(shaft)) {
                float y=level*Storey+Finish;Vector3 center=new Vector3(h.center.x,y,h.center.y);
                Box("Ascensor "+(shaft+1)+" plataforma",center-Vector3.up*.10f,new Vector3(w,.20f,d),metal);
                // Open frontage: shaft 1 north (+Y OpenSees), shaft 2 south (-Y).
                float front=shaft==0?h.yMax:h.yMin,back=shaft==0?h.yMin+.12f:h.yMax-.12f;
                Box("Ascensor fondo",new Vector3(h.center.x,y+1.35f,back),new Vector3(w,2.7f,.08f),metal);
                for(int side=-1;side<=1;side+=2)
                    Box("Ascensor lateral",center+new Vector3(side*(w/2),1.35f,0),new Vector3(.06f,2.7f,d),metal);
                // Threshold bridges only the door opening; it does not fill the shaft.
                Box("Ascensor umbral",new Vector3(h.center.x,y-.04f,front),new Vector3(1.4f,.08f,.5f),floor);
                var panel=Box("Ascensor panel "+(shaft+1),new Vector3(h.center.x-.50f,y+1.1f,front+(shaft==0?-.22f:.22f)),new Vector3(.20f,.48f,.10f),orange);
                var action=panel.AddComponent<CampusAction>();action.kind=1;action.index=shaft;
                action.title="Ascensor interior "+(shaft+1)+" · elegir nivel";actions.Add(action);
                Label(new Vector3(h.center.x-.50f,y+2.35f,front),"ASCENSOR "+(shaft+1)+" / "+level,.075f);
            }
        }
    }

    void ClearLiftIntersections()
    {
        // Remove only interpreted classroom fit-out intersecting a real shaft/door approach.
        foreach(var renderer in architecture.GetComponentsInChildren<MeshRenderer>()) {
            string name=renderer.gameObject.name;
            bool fitout=name.Contains("aula") || name.Contains("acustico") || name=="Hoja" || name=="Visor" || name=="Manilla" ||
                name.Contains("puerta") || name=="Mesa" || name=="Silla" || name=="Respaldo silla" || name.Contains("Pata");
            if(!fitout)continue;
            Bounds b=renderer.bounds;
            if(!elevatorHoles.Any(h=>b.max.x>h.xMin-.10f && b.min.x<h.xMax+.10f && b.max.z>h.yMin-.45f && b.min.z<h.yMax+.45f))continue;
            renderer.enabled=false;
            var c=renderer.GetComponent<Collider>();if(c)c.enabled=false;
        }
    }

    System.Collections.IEnumerator FeatureCheck()
    {
        yield return null;yield return null;
        player.enabled=false;player.GetComponent<CampusLaser>().enabled=false;player.GetComponent<CampusRifle>().enabled=false;
        var args=Environment.GetCommandLineArgs();string output=Application.persistentDataPath;
        for(int i=0;i<args.Length-1;i++)if(args[i]=="-campus-output")output=args[i+1];
        System.IO.Directory.CreateDirectory(output);
        int stopCount=elevatorFloors.Sum(floors=>floors.Length);
        bool valid=actions.Count(a=>a.kind==1)==stopCount;
        for(int shaft=0;shaft<2;shaft++)foreach(int level in ElevatorFloors(shaft)) {
            Vector3 arrival=ElevatorArrival(shaft,level);RaycastHit hit;
            bool support=Physics.Raycast(arrival+Vector3.up*.6f,Vector3.down,out hit,1.5f) && hit.collider.name.Contains("Ascensor");
            valid &= support;Debug.Log("CAMPUS_NEW_LIFT_CHECK shaft="+shaft+" floor="+level+" supported="+support);
        }
        SetStructuralOnly(true);
        bool architectureHidden=architectureRenderers.Keys.All(r=>!r || !r.enabled);
        valid &= architectureHidden && !beamLayer.gameObject.activeSelf && slabLayer.gameObject.activeSelf;
        player.Teleport(new Vector3(12,2*Storey+Finish+.04f,5));player.SetThirdPerson(true);
        yield return null;yield return null;
        valid &= player.ThirdPerson && player.GetComponentInChildren<CampusAvatar>()!=null && Resources.Load<GameObject>("AmongUs")!=null;
        Capture(player.eye,System.IO.Path.Combine(output,"01_estructura_tercera_persona.png"));
        SetStructuralOnly(false);
        valid &= architectureRenderers.All(item=>!item.Key || item.Key.enabled==item.Value);
        valid &= architectureColliders.All(item=>!item.Key || item.Key.enabled==item.Value);
        player.SetThirdPerson(false);
        player.eye.transform.position=new Vector3(5,Storey*2+Finish+1.6f,8.1f);
        player.eye.transform.LookAt(ElevatorArrival(0,2)+Vector3.up*1.2f);
        yield return null;Capture(player.eye,System.IO.Path.Combine(output,"02_ascensor_interior.png"));
        System.IO.File.WriteAllText(System.IO.Path.Combine(output,"feature-check.json"),
            "{\"passed\":"+valid.ToString().ToLowerInvariant()+",\"elevators\":2,\"stops\":"+stopCount+",\"architecture_hidden\":"+architectureHidden.ToString().ToLowerInvariant()+",\"original_among_us\":true}");
        Debug.Log("CAMPUS_FEATURE_CHECK_COMPLETE "+valid);Application.Quit(valid?0:2);
    }
}
