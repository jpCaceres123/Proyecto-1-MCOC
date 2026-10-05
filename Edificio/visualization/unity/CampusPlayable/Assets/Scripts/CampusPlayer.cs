using System;
using System.Linq;
using UnityEngine;

public class CampusPlayer : MonoBehaviour
{
    public CampusWorld world;
    public int equippedTool=1;
    public void Recoil(float amount){pitch=Mathf.Clamp(pitch-amount,-82,82);}
    public Camera eye;
    public CharacterController controller;
    public bool ThirdPerson { get; private set; }
    CampusAvatar avatar;
    Vector3 headPosition=new Vector3(0,1.65f,0);
    int selectedLift;
    public bool InspectionAllowed { get { return !paused && !lift; } }
    Light flashlight;
    float yaw,pitch,vertical,sensitivity=1.8f,walkSpeed=3.6f;
    bool paused=true,lift;
    readonly bool[] discoveries=new bool[5];
    string message="Bienvenido. Recorre los cinco espacios y descubre las placas de cada nivel.";
    float messageUntil=14;
    CampusAction focused;
    StructuralIdentity identity;
    GUIStyle title,text,small;
    AudioSource audioSource;
    AudioClip footstep;
    float stepTime;
    void Awake()
    {
        controller=gameObject.AddComponent<CharacterController>();
        controller.height=1.8f;controller.radius=.27f;controller.center=new Vector3(0,.9f,0);controller.stepOffset=.42f;controller.slopeLimit=48;controller.skinWidth=.035f;
        var camera=new GameObject("Vista en primera persona");camera.transform.SetParent(transform,false);camera.transform.localPosition=new Vector3(0,1.65f,0);
        eye=camera.AddComponent<Camera>();eye.fieldOfView=72;eye.nearClipPlane=.05f;eye.farClipPlane=550;camera.AddComponent<AudioListener>();
        avatar=gameObject.AddComponent<CampusAvatar>();
        flashlight=camera.AddComponent<Light>();flashlight.type=LightType.Spot;flashlight.range=22;flashlight.spotAngle=48;flashlight.intensity=2;flashlight.enabled=false;
        audioSource=gameObject.AddComponent<AudioSource>();audioSource.volume=.1f;
        var noise=new System.Random(51);float[] wave=new float[3300];
        for(int i=0;i<wave.Length;i++)wave[i]=(float)(noise.NextDouble()*2-1)*Mathf.Exp(-i/450f);
        footstep=AudioClip.Create("Pisada",wave.Length,1,22050,false);footstep.SetData(wave,0);
        paused=!Environment.GetCommandLineArgs().Contains("-campus-check") && !Environment.GetCommandLineArgs().Contains("-campus-ui-preview") && !Environment.GetCommandLineArgs().Contains("-campus-feature-check");
        SetCursor();
    }
    public void Teleport(Vector3 feet)
    {
        controller.enabled=false;transform.position=feet;controller.enabled=true;vertical=0;Physics.SyncTransforms();
    }
    void SetCursor(){Cursor.lockState=paused||lift?CursorLockMode.None:CursorLockMode.Locked;Cursor.visible=paused||lift;}
    void Update()
    {
        if(Input.GetKeyDown(KeyCode.Escape)){if(lift)lift=false;else paused=!paused;SetCursor();}
        if(paused||lift)return;
        if(Input.GetKeyDown(KeyCode.F5))SetThirdPerson(!ThirdPerson);
        if(Input.GetKeyDown(KeyCode.F6)){world.SetStructuralOnly(!world.StructuralOnly);Notify(world.StructuralOnly?"Modo estructural: losas, columnas y muros. F6 restaura arquitectura.":"Arquitectura restaurada.");}
        if(Input.GetKeyDown(KeyCode.Alpha1)||Input.GetKeyDown(KeyCode.Keypad1))equippedTool=1;
        if(Input.GetKeyDown(KeyCode.Alpha2)||Input.GetKeyDown(KeyCode.Keypad2))equippedTool=2;
        yaw+=Input.GetAxis("Mouse X")*sensitivity;
        pitch=Mathf.Clamp(pitch-Input.GetAxis("Mouse Y")*sensitivity,-82,82);
        transform.rotation=Quaternion.Euler(0,yaw,0);eye.transform.localRotation=Quaternion.Euler(pitch,0,0);
        float speed=Input.GetKey(KeyCode.LeftShift)?6.2f:walkSpeed;
        Vector3 move=transform.right*Input.GetAxisRaw("Horizontal")+transform.forward*Input.GetAxisRaw("Vertical");
        if(move.sqrMagnitude>1)move.Normalize();
        if(controller.isGrounded && vertical<0)vertical=-2;
        if(controller.isGrounded && Input.GetKeyDown(KeyCode.Space))vertical=5.5f;
        vertical-=18*Time.deltaTime;
        controller.Move((move*speed+Vector3.up*vertical)*Time.deltaTime);
        if(controller.isGrounded && move.sqrMagnitude>.1f && Time.time>stepTime){audioSource.PlayOneShot(footstep);stepTime=Time.time+(speed>5?.3f:.48f);}
        if(transform.position.y<-10 || Input.GetKeyDown(KeyCode.R)){Teleport(world.Entrance);Notify("Regresaste al acceso.");}
        if(Input.GetKeyDown(KeyCode.F))flashlight.enabled=!flashlight.enabled;
        focused=null;identity=null;
        RaycastHit hit;
        Vector3 interactionOrigin=transform.TransformPoint(headPosition);
        if(Physics.Raycast(interactionOrigin,eye.transform.forward,out hit,3.5f)){
            focused=hit.collider.GetComponentInParent<CampusAction>();identity=hit.collider.GetComponentInParent<StructuralIdentity>();
        }
        if(focused==null)focused=world.actions.Where(a=>a && a.kind!=2 && (!world.StructuralOnly || a.kind==1) && Vector3.Distance(interactionOrigin,a.transform.position)<2.4f)
            .OrderBy(a=>Vector3.Distance(interactionOrigin,a.transform.position)).FirstOrDefault();
        if(Input.GetKeyDown(KeyCode.E)){
            if(focused!=null){
                if(focused.kind==1){selectedLift=focused.index;lift=true;SetCursor();}
                else if(focused.kind==2)focused.door.Toggle();
                else {discoveries[focused.index]=true;Notify("Descubriste "+focused.title+". "+discoveries.Count(v=>v)+"/5 espacios visitados.");}
            }else if(identity!=null)Notify(identity.description);
        }
    }
    public void SetThirdPerson(bool value){ThirdPerson=value;UpdateCamera();}
    void LateUpdate(){UpdateCamera();}
    void UpdateCamera()
    {
        if(!eye)return;
        if(!ThirdPerson){eye.transform.localPosition=headPosition;if(avatar)avatar.SetVisible(false);return;}
        Vector3 pivot=transform.TransformPoint(new Vector3(0,1.25f,0));
        Vector3 desired=pivot-eye.transform.forward*3.2f+Vector3.up*.25f;
        Vector3 direction=desired-pivot;float distance=direction.magnitude;
        foreach(var hit in Physics.SphereCastAll(pivot,.18f,direction.normalized,distance,~0,QueryTriggerInteraction.Ignore))
            if(!hit.collider.transform.IsChildOf(transform))distance=Mathf.Min(distance,Mathf.Max(.12f,hit.distance-.08f));
        eye.transform.position=pivot+direction.normalized*distance;
        if(avatar)avatar.SetVisible(distance>.55f);
    }
    void Notify(string s){message=s;messageUntil=Time.time+8;}
    void OnApplicationFocus(bool focus){if(!focus && !Environment.GetCommandLineArgs().Contains("-campus-check")){paused=true;SetCursor();}}
    void Styles()
    {
        if(title!=null)return;
        title=new GUIStyle(GUI.skin.label){fontSize=24,fontStyle=FontStyle.Bold,normal={textColor=Color.white}};
        text=new GUIStyle(GUI.skin.label){fontSize=16,wordWrap=true,normal={textColor=new Color(.92f,.94f,.95f)}};
        small=new GUIStyle(text){fontSize=13};
    }
    void OnGUI()
    {
        Styles();int level=Mathf.Clamp(Mathf.RoundToInt((transform.position.y-CampusWorld.Finish)/CampusWorld.Storey),0,5);
        GUI.color=new Color(.04f,.07f,.10f,.94f);GUI.Box(new Rect(20,20,330,88),"");GUI.color=Color.white;
        GUI.Label(new Rect(36,30,300,30),"CAMPUS / INGENIERÍA",title);
        GUI.Label(new Rect(36,62,300,24),CampusWorld.FloorNames[level]+" · "+(world.StructuralOnly?"ESTRUCTURA":"CAMPUS")+" · "+(ThirdPerson?"3ª persona":"1ª persona"),small);
        GUI.Label(new Rect(20,Screen.height-37,Screen.width-40,24),"WASD mover  ·  Mouse mirar  ·  Shift correr  ·  Espacio saltar  ·  E interactuar  ·  1 láser  ·  2 AK47  ·  clic disparar  ·  T recargar  ·  Q caso  ·  clic derecho fijar  ·  F linterna  ·  Esc menú",small);
        if(!paused && !lift){
            GUI.Label(new Rect(Screen.width/2-5,Screen.height/2-10,20,20),"+");
            if(focused!=null || identity!=null){var style=new GUIStyle(text){alignment=TextAnchor.MiddleCenter};
                GUI.Label(new Rect(Screen.width/2-230,Screen.height/2+30,460,60),"[E] "+(focused!=null?focused.title:"Inspeccionar "+identity.gameObject.name),style);}
            if(Time.time<messageUntil){GUI.color=new Color(.04f,.07f,.10f,.9f);GUI.Box(new Rect(20,122,370,100),"");GUI.color=Color.white;GUI.Label(new Rect(35,133,340,85),message,text);}
        }
        if(paused){
            float x=(Screen.width-520)/2f,y=(Screen.height-495)/2f;
            GUI.color=new Color(.025f,.045f,.065f,.98f);GUI.Box(new Rect(x,y,520,495),"");GUI.color=Color.white;
            GUI.Label(new Rect(x+28,y+25,470,45),"RECORRIDO DEL CAMPUS",title);
            GUI.Label(new Rect(x+28,y+78,465,92),"Explora el edificio, sube por la escalera naranja o utiliza el ascensor. Abre las puertas con E y descubre los cinco espacios de ingeniería.",text);
            GUI.Label(new Rect(x+28,y+174,330,25),"Sensibilidad del mouse",small);sensitivity=GUI.HorizontalSlider(new Rect(x+28,y+207,460,20),sensitivity,.6f,4);
            if(GUI.Button(new Rect(x+28,y+238,465,36),world.StructuralOnly?"F6 · Restaurar arquitectura":"F6 · Solo losas, columnas y muros"))world.SetStructuralOnly(!world.StructuralOnly);
            if(GUI.Button(new Rect(x+28,y+282,465,36),ThirdPerson?"F5 · Primera persona":"F5 · Tercera persona / Among Us"))SetThirdPerson(!ThirdPerson);
            if(GUI.Button(new Rect(x+28,y+330,465,42),"Entrar / continuar")){paused=false;SetCursor();}
            if(GUI.Button(new Rect(x+28,y+384,226,36),"Volver al acceso")){Teleport(world.Entrance);paused=false;SetCursor();}
            if(GUI.Button(new Rect(x+267,y+384,226,36),"Salir"))Application.Quit();
            GUI.Label(new Rect(x+28,y+435,460,35),"F5 cámara · F6 estructura · E ascensores interiores",small);
        }
        if(lift){
            float x=(Screen.width-440)/2f,y=(Screen.height-425)/2f;
            GUI.color=new Color(.025f,.045f,.065f,.98f);GUI.Box(new Rect(x,y,440,425),"");GUI.color=Color.white;
            GUI.Label(new Rect(x+25,y+22,390,38),"ASCENSOR INTERIOR "+(selectedLift+1),title);
            var levels=world.ElevatorFloors(selectedLift);
            for(int i=0;i<levels.Length;i++){int floor=levels[i];if(GUI.Button(new Rect(x+25,y+78+i*45,390,36),floor+" · "+CampusWorld.FloorNames[floor])){
                Teleport(world.ElevatorArrival(selectedLift,floor));lift=false;SetCursor();Notify("Llegaste a "+CampusWorld.FloorNames[floor]);}}
            if(GUI.Button(new Rect(x+25,y+357,390,35),"Cerrar")){lift=false;SetCursor();}
        }
    }
}
