using System.Collections.Generic;
using UnityEngine;

// Same supplied FBX and material palette as MovingLoadViewer; visuals only.
public sealed class CampusAvatar : MonoBehaviour
{
    Transform body;
    Vector3 lastPosition;
    float phase;
    readonly List<Material> materials=new List<Material>();
    void Awake()
    {
        var source=Resources.Load<GameObject>("AmongUs");
        if(!source){Debug.LogError("Falta el AmongUs.fbx original en Resources");return;}
        body=new GameObject("Among Us · visitante").transform;body.SetParent(transform,false);
        var mesh=Instantiate(source,body).transform;
        foreach(var c in mesh.GetComponentsInChildren<Collider>())Destroy(c);
        var renderers=mesh.GetComponentsInChildren<Renderer>();
        if(renderers.Length==0){Debug.LogError("Among Us sin mallas");return;}
        foreach(var renderer in renderers) {
            var palette=new Material[renderer.sharedMaterials.Length];
            for(int k=0;k<palette.Length;k++) {
                string part=renderer.sharedMaterials[k]?renderer.sharedMaterials[k].name.ToLowerInvariant():"";
                Color color=part.Contains("ecran")||part.Contains("screen")?new Color(.56f,.91f,1f):
                    part.Contains("pourtour")?new Color(.42f,.09f,.15f):
                    part.Contains("001")?new Color(.68f,.15f,.23f):new Color(.91f,.13f,.22f);
                palette[k]=new Material(Shader.Find("Standard")){color=color};palette[k].SetFloat("_Glossiness",.38f);materials.Add(palette[k]);
            }
            renderer.sharedMaterials=palette;
        }
        Bounds bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
        mesh.localScale*=1.65f/Mathf.Max(.001f,bounds.size.y);
        bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
        Vector3 center=body.InverseTransformPoint(bounds.center),bottom=body.InverseTransformPoint(new Vector3(bounds.center.x,bounds.min.y,bounds.center.z));
        mesh.localPosition-=new Vector3(center.x,bottom.y,center.z);
        lastPosition=transform.position;body.gameObject.SetActive(false);
    }
    public void SetVisible(bool value){if(body)body.gameObject.SetActive(value);}
    void LateUpdate()
    {
        Vector3 delta=transform.position-lastPosition;delta.y=0;lastPosition=transform.position;
        bool walking=delta.sqrMagnitude>.00001f && delta.magnitude<1;
        if(walking)phase+=Time.deltaTime*11;
        if(body){body.localPosition=Vector3.up*(walking?Mathf.Abs(Mathf.Sin(phase))*.045f:0);body.localRotation=Quaternion.Euler(0,0,walking?Mathf.Sin(phase)*3:0);}
    }
    void OnDestroy(){foreach(var m in materials)if(m)Destroy(m);}
}
