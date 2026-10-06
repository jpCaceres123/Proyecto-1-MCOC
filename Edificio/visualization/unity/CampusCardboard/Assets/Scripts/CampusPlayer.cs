using UnityEngine;

// Mobile-only visitor: position belongs to locomotion; orientation belongs to Cardboard.
public class CampusPlayer : MonoBehaviour
{
    public CampusWorld world;
    public Camera eye;
    public CharacterController controller;
    public int equippedTool=1;
    public bool ThirdPerson { get { return false; } }
    public bool VRControlled { get; private set; }
    public bool InspectionAllowed { get { return false; } }
    void Awake()
    {
        controller=gameObject.AddComponent<CharacterController>();
        controller.height=1.8f;controller.radius=.27f;controller.center=new Vector3(0,.9f,0);
        controller.stepOffset=.42f;controller.slopeLimit=48;controller.skinWidth=.035f;
        var camera=new GameObject("Cabeza Cardboard");camera.tag="MainCamera";
        camera.transform.SetParent(transform,false);camera.transform.localPosition=new Vector3(0,1.65f,0);
        eye=camera.AddComponent<Camera>();eye.fieldOfView=72;eye.nearClipPlane=.05f;eye.farClipPlane=550;
        camera.AddComponent<AudioListener>();
    }
    public void Teleport(Vector3 feet){controller.enabled=false;transform.position=feet;controller.enabled=true;Physics.SyncTransforms();}
    public void SetVRControl(bool value){VRControlled=value;}
    // Compatibility with shared generation helpers, not controls of this application.
    public void SetThirdPerson(bool value){}
    public void Recoil(float amount){}
}
