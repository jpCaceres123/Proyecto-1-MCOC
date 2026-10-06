using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// First-person game prop. Hits produce temporary visual impacts in the campus.
public class CampusRifle : MonoBehaviour {
    CampusPlayer player; Transform model,muzzle; GameObject flash;
    Material steel,wood,skin,glow; AudioSource sound; AudioClip shot;
    readonly Queue<GameObject> impacts=new Queue<GameObject>();
    int rounds=30;float nextShot,reloadUntil,kick,flashUntil;bool reloading;
    Vector3 rest=new Vector3(.24f,-.25f,.47f);
    void Start() {
        player=GetComponent<CampusPlayer>();
        steel=Mat(new Color(.065f,.075f,.08f));wood=Mat(new Color(.40f,.17f,.06f));skin=Mat(new Color(.55f,.37f,.24f));glow=Mat(new Color(1,.7f,.13f));
        glow.EnableKeyword("_EMISSION");glow.SetColor("_EmissionColor",new Color(1,.48f,.04f)*5);
        model=new GameObject("AK47 · modelo primera persona").transform;model.SetParent(player.eye.transform,false);model.localPosition=rest;
        Piece("Cajon de mecanismos",new Vector3(0,0,0),new Vector3(.075f,.11f,.29f),steel);
        Piece("Culata de madera",new Vector3(0,-.01f,-.27f),new Vector3(.072f,.10f,.28f),wood);
        Piece("Empuñadura",new Vector3(0,-.105f,-.07f),new Vector3(.057f,.17f,.08f),wood).localRotation=Quaternion.Euler(-18,0,0);
        Piece("Guardamanos",new Vector3(0,-.018f,.24f),new Vector3(.085f,.085f,.23f),wood);
        Tube("Cañon",new Vector3(0,.023f,.47f),.014f,.40f,steel);
        Tube("Tubo de gases",new Vector3(0,.065f,.35f),.014f,.23f,steel);
        Piece("Mira posterior",new Vector3(0,.095f,.055f),new Vector3(.035f,.025f,.045f),steel);
        Piece("Mira anterior",new Vector3(0,.075f,.59f),new Vector3(.024f,.095f,.035f),steel);
        Piece("Gatillo",new Vector3(0,-.09f,-.01f),new Vector3(.013f,.06f,.012f),steel);
        // Segmented curved magazine is visible beneath the receiver.
        for(int i=0;i<9;i++) {
            float t=i/8f;var segment=Piece("Cargador curvo",new Vector3(0,-.075f-t*.21f,.045f+t*t*.105f),new Vector3(.052f,.035f,.11f),steel);
            segment.localRotation=Quaternion.Euler(-t*35,0,0);
        }
        Piece("Mano derecha",new Vector3(.03f,-.12f,-.085f),new Vector3(.075f,.075f,.10f),skin);
        Piece("Brazo derecho",new Vector3(.09f,-.20f,-.20f),new Vector3(.085f,.085f,.28f),skin).localRotation=Quaternion.Euler(15,-20,0);
        Piece("Mano izquierda",new Vector3(-.025f,-.075f,.25f),new Vector3(.08f,.075f,.10f),skin);
        Piece("Brazo izquierdo",new Vector3(-.095f,-.15f,.08f),new Vector3(.085f,.085f,.30f),skin).localRotation=Quaternion.Euler(-20,22,0);
        muzzle=new GameObject("Boca del cañon").transform;muzzle.SetParent(model,false);muzzle.localPosition=new Vector3(0,.025f,.68f);
        flash=Piece("Destello",muzzle.localPosition+Vector3.forward*.04f,new Vector3(.07f,.07f,.14f),glow).gameObject;flash.SetActive(false);
        sound=gameObject.AddComponent<AudioSource>();sound.volume=.22f;sound.spatialBlend=0;
        var rng=new System.Random(492);var samples=new float[6500];
        for(int i=0;i<samples.Length;i++){float t=i/22050f;samples[i]=(float)((rng.NextDouble()*2-1)*.7+System.Math.Sin(t*460)*.3)*Mathf.Exp(-t*28);}
        shot=AudioClip.Create("Disparo sintetizado",samples.Length,1,22050,false);shot.SetData(samples,0);
    }
    Material Mat(Color c){var m=new Material(Shader.Find("Standard"));m.color=c;m.SetFloat("_Glossiness",.35f);return m;}
    Transform Piece(string name,Vector3 pos,Vector3 scale,Material material) {
        var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=name;g.transform.SetParent(model,false);g.transform.localPosition=pos;g.transform.localScale=scale;
        g.GetComponent<Collider>().enabled=false;g.GetComponent<Renderer>().sharedMaterial=material;g.GetComponent<Renderer>().shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;return g.transform;
    }
    void Tube(string name,Vector3 pos,float radius,float length,Material material) {
        var g=GameObject.CreatePrimitive(PrimitiveType.Cylinder);g.name=name;g.transform.SetParent(model,false);g.transform.localPosition=pos;g.transform.localRotation=Quaternion.Euler(90,0,0);g.transform.localScale=new Vector3(radius*2,length/2,radius*2);
        g.GetComponent<Collider>().enabled=false;g.GetComponent<Renderer>().sharedMaterial=material;
    }
    void Update() {
        if(!model)return;
        bool equipped=player.equippedTool==2 && player.InspectionAllowed && !player.ThirdPerson;model.gameObject.SetActive(equipped);
        if(!equipped){flash.SetActive(false);return;}
        if(Input.GetKeyDown(KeyCode.T) && rounds<30 && !reloading){reloading=true;reloadUntil=Time.time+1.6f;}
        if(reloading && Time.time>=reloadUntil){rounds=30;reloading=false;}
        if(Input.GetMouseButton(0) && !reloading && rounds>0 && Time.time>=nextShot)Fire();
        kick=Mathf.MoveTowards(kick,0,Time.deltaTime*9);
        model.localPosition=rest+new Vector3(0,-(reloading?.12f:0),-kick*.045f);
        model.localRotation=Quaternion.Euler(-kick*4+(reloading?25:0),-4,0);
        flash.SetActive(Time.time<flashUntil);
    }
    void Fire() {
        rounds--;nextShot=Time.time+.11f;kick=1;flashUntil=Time.time+.045f;player.Recoil(.55f);sound.PlayOneShot(shot);
        Vector3 end=player.eye.transform.position+player.eye.transform.forward*150;
        var hits=Physics.RaycastAll(player.eye.transform.position,player.eye.transform.forward,150,~0,QueryTriggerInteraction.Ignore).OrderBy(h=>h.distance);
        foreach(var hit in hits) {
            if(hit.collider.transform.IsChildOf(transform))continue;
            end=hit.point;
            var mark=GameObject.CreatePrimitive(PrimitiveType.Sphere);mark.name="Impacto temporal";mark.GetComponent<Collider>().enabled=false;
            mark.transform.position=hit.point+hit.normal*.006f;mark.transform.rotation=Quaternion.FromToRotation(Vector3.forward,hit.normal);mark.transform.localScale=new Vector3(.065f,.065f,.012f);mark.GetComponent<Renderer>().sharedMaterial=steel;
            impacts.Enqueue(mark);Destroy(mark,12);while(impacts.Count>60){var old=impacts.Dequeue();if(old)Destroy(old);}break;
        }
        var trace=new GameObject("Traza de disparo").AddComponent<LineRenderer>();trace.positionCount=2;trace.SetPosition(0,muzzle.position);trace.SetPosition(1,end);trace.startWidth=.01f;trace.endWidth=.003f;trace.sharedMaterial=glow;Destroy(trace.gameObject,.04f);
    }
    void OnGUI() {
        if(!player || player.equippedTool!=2 || !player.InspectionAllowed)return;
        var style=new GUIStyle(GUI.skin.label){fontSize=18,normal={textColor=Color.white},alignment=TextAnchor.MiddleRight};
        GUI.Label(new Rect(Screen.width-330,Screen.height-110,300,60),"AK-47  ·  "+rounds+" / 30\n"+(reloading?"Recargando…":"Clic izquierdo: disparar · T: recargar"),style);
    }
    void OnDestroy(){foreach(var g in impacts)if(g)Destroy(g);if(steel)Destroy(steel);if(wood)Destroy(wood);if(skin)Destroy(skin);if(glow)Destroy(glow);if(shot)Destroy(shot);}
}
