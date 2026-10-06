using System.Collections.Generic;
using UnityEngine;

// Game effect only: no analytical forces, geometry contract or collision meshes change.
public class CampusEarthquake : MonoBehaviour
{
    const float Duration=25f;
    public CampusPlayer player;
    public bool Active { get; private set; }
    public float Remaining { get { return Mathf.Max(0,Duration-elapsed); } }
    float elapsed, intensity, stopping;
    bool fading, prepared;
    AudioSource rumble;
    AudioClip clip;
    readonly List<Surface> surfaces=new List<Surface>();
    class Surface {
        public MeshFilter filter;
        public Mesh original, animated;
        public Vector3[] rest, vertices;
        public float[] weights;
    }

    public void Toggle()
    {
        if(Active){Active=false;fading=true;stopping=intensity;return;}
        Prepare();elapsed=0;intensity=0;fading=false;Active=true;
        rumble.Play();
    }

    void Prepare()
    {
        if(prepared)return;
        prepared=true;
        foreach(var filter in GetComponentsInChildren<MeshFilter>(true)){
            var renderer=filter.GetComponent<MeshRenderer>();
            if(!renderer || !renderer.enabled || filter.GetComponent<TextMesh>() || !filter.sharedMesh || !filter.sharedMesh.isReadable)continue;
            // Keep the landscape stationary so the building's sway is visible against it.
            string materialName=renderer.sharedMaterial?renderer.sharedMaterial.name:"";
            if(materialName.Contains("Paisaje") || materialName.Contains("Vegetacion"))continue;
            var rest=filter.sharedMesh.vertices;
            var weights=new float[rest.Length];bool elevated=false;
            for(int i=0;i<rest.Length;i++){
                weights[i]=Mathf.Clamp01((filter.transform.TransformPoint(rest[i]).y-CampusWorld.Finish)/20f);
                elevated|=weights[i]>.01f;
            }
            if(!elevated)continue;
            var original=filter.sharedMesh;
            var mesh=Instantiate(original);mesh.name=original.name+" · terremoto visual";mesh.MarkDynamic();
            var bounds=mesh.bounds;bounds.Expand(6);mesh.bounds=bounds;filter.sharedMesh=mesh;
            surfaces.Add(new Surface{filter=filter,original=original,animated=mesh,rest=rest,vertices=new Vector3[rest.Length],weights=weights});
        }
        rumble=gameObject.AddComponent<AudioSource>();rumble.loop=true;rumble.spatialBlend=0;rumble.volume=0;
        const int rate=22050;
        var samples=new float[rate*4];
        for(int i=0;i<samples.Length;i++){
            float t=(float)i/rate;
            samples[i]=(.35f*Mathf.Sin(2*Mathf.PI*42*t)+.2f*Mathf.Sin(2*Mathf.PI*57*t)+.1f*Mathf.Sin(2*Mathf.PI*83*t))*(.7f+.3f*Mathf.Sin(2*Mathf.PI*2*t));
        }
        clip=AudioClip.Create("Retumbo de terremoto",samples.Length,1,rate,false);clip.SetData(samples,0);rumble.clip=clip;
    }

    void Update()
    {
        if(!Active && !fading)return;
        bool running=player && player.InspectionAllowed;
        if(running){
            elapsed+=Time.deltaTime;
            if(Active && elapsed>=Duration){Active=false;fading=true;stopping=intensity;}
            intensity=Active?Mathf.SmoothStep(0,1,elapsed/2.5f)*Mathf.Clamp01((Duration-elapsed)/3f):Mathf.MoveTowards(stopping,0,Time.deltaTime/1.2f);
            stopping=intensity;
        }
        float amount=running?intensity:0;
        // Exaggerated visual story drift: fixed base, progressively larger upper-floor motion.
        Vector3 sway=new Vector3(Mathf.Sin(elapsed*4.8f)+.22f*Mathf.Sin(elapsed*11.3f),0,Mathf.Sin(elapsed*3.7f+.8f))*(1.35f*amount);
        foreach(var surface in surfaces){
            if(!surface.filter)continue;
            Vector3 localSway=surface.filter.transform.InverseTransformVector(sway);
            for(int i=0;i<surface.rest.Length;i++)surface.vertices[i]=surface.rest[i]+localSway*surface.weights[i];
            surface.animated.vertices=surface.vertices;
        }
        if(running && !rumble.isPlaying && amount>0)rumble.UnPause();
        if(!running && rumble.isPlaying)rumble.Pause();
        rumble.volume=.38f*amount;
        if(fading && intensity<=0){fading=false;rumble.Stop();Restore();}
    }

    public void ShakeCamera(Camera camera)
    {
        if(!camera || !player || !player.InspectionAllowed || intensity<=0)return;
        float t=elapsed;
        camera.transform.localPosition+=new Vector3(Mathf.Sin(t*31),Mathf.Sin(t*37+.5f),0)*(.012f*intensity);
        camera.transform.localRotation*=Quaternion.Euler(new Vector3(Mathf.Sin(t*23)*.2f,Mathf.Sin(t*19)*.15f,Mathf.Sin(t*17)*.3f)*intensity);
    }

    void Restore(){foreach(var s in surfaces)if(s.animated)s.animated.vertices=s.rest;}
    void OnDisable(){Active=false;fading=false;intensity=0;Restore();if(rumble)rumble.Stop();}
    void OnDestroy(){foreach(var s in surfaces){if(s.filter)s.filter.sharedMesh=s.original;if(s.animated)Destroy(s.animated);}if(clip)Destroy(clip);}
}
