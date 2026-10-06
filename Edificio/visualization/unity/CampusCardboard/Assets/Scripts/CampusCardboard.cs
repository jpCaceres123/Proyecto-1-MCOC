using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.InputSystem;

// Desktop preview tests interaction only. Native stereo and head tracking use the official SDK.
public sealed class CampusCardboard : MonoBehaviour
{
    public bool Active { get; private set; }
    public bool NativeStereo { get; private set; }
    public string Status { get; private set; }="Cardboard: listo para iniciar";
    public string SelectedKey { get { return selectedKey; } }
    CampusPlayer player;
    CampusLaser inspector;
    CampusBeamDiagram beamDiagram;
    AnalysisNetworkClient network;
    ReinforcementPanel reinforcement;
    CampusCapacityDiagram capacityDiagram;
    Transform backendSelector;
    StructuralIdentity selectedBar;
    Transform selector;
    Transform menu;
    TextMesh backendStatus;
    TextMesh heading,readout,sampleLabel;
    LineRenderer diagram,reticle,progress,outline;
    readonly List<Material> materials=new List<Material>();
    readonly Dictionary<Collider,Action> buttons=new Dictionary<Collider,Action>();
    readonly Dictionary<Collider,Renderer> buttonRenderers=new Dictionary<Collider,Renderer>();
    Collider forwardButton,backButton,previousTarget;
    string previousKey="",selectedKey="";
    float gazeSince,fallVelocity,previewYaw,previewPitch;
    bool activated,starting,structuralBefore,thirdBefore;
    Quaternion cameraRotationBefore;
    int caseIndex,graphIndex=4,stationIndex;
    CampusLaser.Entry selected;
    ScreenOrientation orientationBefore;
    int sleepBefore;
    public const float Dwell=2f;
    const float WalkSpeed=1.4f;
    public bool PointLocomotion { get; private set; }
    bool stepAvailable=true;
    readonly Color accent=new Color(.18f,.86f,.75f);
#if UNITY_ANDROID && !UNITY_EDITOR
    Google.XR.Cardboard.XRLoader loader;
#endif

    IEnumerator Start()
    {
        player=GetComponent<CampusPlayer>();inspector=GetComponent<CampusLaser>();
        beamDiagram=gameObject.AddComponent<CampusBeamDiagram>();
        gameObject.AddComponent<CampusPerformance>();
        network=gameObject.AddComponent<AnalysisNetworkClient>();reinforcement=gameObject.AddComponent<ReinforcementPanel>();capacityDiagram=gameObject.AddComponent<CampusCapacityDiagram>();
        yield return null;
        bool validate=Environment.GetCommandLineArgs().Contains("-campus-vr-check");
#if UNITY_EDITOR
        validate|=UnityEditor.SessionState.GetBool("CampusVRValidation",false);
        UnityEditor.SessionState.EraseBool("CampusVRValidation");
#endif
        if(validate)StartCoroutine(ValidatePreview());
        else Enter(); // This separate application is Cardboard-only, not a mode of the PC game.
    }
    public void Enter(){if(!Active && !starting && player)StartCoroutine(Begin());}
    IEnumerator Begin()
    {
        starting=true;Status="Iniciando Cardboard…";
        orientationBefore=Screen.orientation;sleepBefore=Screen.sleepTimeout;
        Screen.orientation=ScreenOrientation.LandscapeLeft;
        yield return null;yield return null;
#if UNITY_ANDROID && !UNITY_EDITOR
        try {
            loader=ScriptableObject.CreateInstance<Google.XR.Cardboard.XRLoader>();
            bool ready=loader.Initialize() && loader.Start();
            NativeStereo=ready && loader.GetLoadedSubsystem<XRDisplaySubsystem>()?.running==true &&
                loader.GetLoadedSubsystem<XRInputSubsystem>()?.running==true;
        }catch(Exception error){Debug.LogException(error);NativeStereo=false;}
        if(!NativeStereo){ReleaseLoader();Screen.orientation=orientationBefore;starting=false;
            Status="No se pudo iniciar Cardboard. Revisa proveedor XR, SDK y teléfono.";yield break;}
        if(!Google.XR.Cardboard.Api.HasDeviceParams())Google.XR.Cardboard.Api.ScanDeviceParams();
#else
        NativeStereo=false;
#endif
        structuralBefore=player.world.StructuralOnly;thirdBefore=player.ThirdPerson;
        cameraRotationBefore=player.eye.transform.localRotation;
        previewYaw=player.eye.transform.eulerAngles.y;previewPitch=0;
        player.SetVRControl(true);player.eye.stereoTargetEye=StereoTargetEyeMask.Both;
        Screen.sleepTimeout=SleepTimeout.NeverSleep;Active=true;starting=false;
        BuildMenu();RecenterMenu();
        Status=NativeStereo?"Cardboard nativo":"PREVISUALIZACIÓN PC · sin estéreo ni head tracking";
        Refresh();Debug.Log("CAMPUS_VR_ENTER native="+NativeStereo);
    }
    void ReleaseLoader()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if(loader){
            try{loader.Stop();loader.Deinitialize();}catch(Exception error){Debug.LogException(error);}
            Destroy(loader);loader=null;
        }
#endif
        NativeStereo=false;
    }
    public void Exit()
    {
        if(!Active)return;
        Active=false;ReleaseLoader();Screen.orientation=orientationBefore;Screen.sleepTimeout=sleepBefore;
        if(menu)Destroy(menu.gameObject);
        if(reticle)Destroy(reticle.gameObject);if(progress)Destroy(progress.gameObject);
        if(outline)Destroy(outline.gameObject);
        if(beamDiagram)beamDiagram.Clear();if(capacityDiagram)capacityDiagram.Clear();
        buttons.Clear();buttonRenderers.Clear();forwardButton=backButton=null;previousTarget=null;previousKey="";activated=false;
        foreach(var material in materials)if(material)Destroy(material);materials.Clear();
        player.world.SetStructuralOnly(structuralBefore);player.SetVRControl(false);player.SetThirdPerson(thirdBefore);
        player.eye.transform.localRotation=cameraRotationBefore;
        player.eye.ResetAspect();player.eye.ResetProjectionMatrix();player.eye.ResetWorldToCameraMatrix();
        player.eye.ResetStereoProjectionMatrices();player.eye.ResetStereoViewMatrices();
        Status="Cardboard cerrado";Debug.Log("CAMPUS_VR_EXIT");
    }
    void Update()
    {
        if(!Active){
#if !UNITY_ANDROID || UNITY_EDITOR
            if(Keyboard.current!=null && Keyboard.current.f7Key.wasPressedThisFrame)Enter();
#endif
            return;
        }
        bool trigger=false;
#if UNITY_ANDROID && !UNITY_EDITOR
        Google.XR.Cardboard.Api.UpdateScreenParams();
        if(Google.XR.Cardboard.Api.HasNewDeviceParams())Google.XR.Cardboard.Api.ReloadDeviceParams();
        if(Google.XR.Cardboard.Api.IsCloseButtonPressed){Exit();return;}
        if(Google.XR.Cardboard.Api.IsGearButtonPressed)Google.XR.Cardboard.Api.ScanDeviceParams();
        if(Google.XR.Cardboard.Api.IsTriggerHeldPressed){Google.XR.Cardboard.Api.Recenter();RecenterMenu();}
        trigger=Google.XR.Cardboard.Api.IsTriggerPressed;
        var head=InputDevices.GetDeviceAtXRNode(XRNode.Head);
        Quaternion rotation;
        bool tracked=head.TryGetFeatureValue(UnityEngine.XR.CommonUsages.centerEyeRotation,out rotation) ||
            head.TryGetFeatureValue(UnityEngine.XR.CommonUsages.deviceRotation,out rotation);
        if(!tracked){Status="Sin seguimiento de cabeza · movimiento detenido";gazeSince=Time.unscaledTime;activated=false;return;}
        player.eye.transform.localRotation=rotation;
#else
        if(Keyboard.current!=null && (Keyboard.current.f7Key.wasPressedThisFrame || Keyboard.current.escapeKey.wasPressedThisFrame)){Exit();return;}
        if(Mouse.current!=null && Mouse.current.rightButton.isPressed){
            Vector2 delta=Mouse.current.delta.ReadValue();
            previewYaw+=delta.x*.12f;
            previewPitch=Mathf.Clamp(previewPitch-delta.y*.12f,-80,80);
        }
        player.eye.transform.rotation=Quaternion.Euler(previewPitch,previewYaw,0);
        if(Keyboard.current!=null && Keyboard.current.mKey.wasPressedThisFrame)RecenterMenu();
        trigger=Mouse.current!=null && Mouse.current.leftButton.wasPressedThisFrame;
#endif
        menu.position=transform.position+Vector3.up*1.45f+menu.forward*2.2f;
        // Gaze controls move with the visitor, so their physics poses must be current before raycasting.
        Physics.SyncTransforms();
        Collider target=null;string key="";
        var ray=new Ray(player.eye.transform.position,player.eye.transform.forward);
        foreach(var hit in Physics.RaycastAll(ray,45,~0,QueryTriggerInteraction.Collide).OrderBy(h=>h.distance)){
            if(buttons.ContainsKey(hit.collider)){
                if(selector && selector.gameObject.activeSelf && !hit.collider.transform.IsChildOf(selector))continue;
                if(backendSelector && backendSelector.gameObject.activeSelf && !hit.collider.transform.IsChildOf(backendSelector))continue;
                target=hit.collider;break;
            }
            if(selector && selector.gameObject.activeSelf || backendSelector && backendSelector.gameObject.activeSelf)continue;
            var id=hit.collider.GetComponentInParent<StructuralIdentity>();
            if(id && !string.IsNullOrEmpty(id.key)){target=hit.collider;key=id.key;break;}
        }
        // Look into empty space and press the viewer button to bring controls to that direction.
        if(trigger && !target)RecenterMenu();
        if(target!=previousTarget || key!=previousKey){
            stepAvailable=true;
            if(previousTarget && buttonRenderers.ContainsKey(previousTarget))buttonRenderers[previousTarget].material.color=new Color(.06f,.12f,.17f);
            previousTarget=target;previousKey=key;gazeSince=Time.unscaledTime;activated=false;
        }
        float amount=target?Mathf.Clamp01((Time.unscaledTime-gazeSince)/Dwell):0;
        DrawRing(progress,.013f,amount);
        if(target && buttonRenderers.ContainsKey(target))buttonRenderers[target].material.color=Color.Lerp(new Color(.06f,.12f,.17f),accent,amount);
        if(target && (trigger || (!activated && amount>=1))){
            activated=true;
            if(buttons.TryGetValue(target,out var action))action();
            else if(inspector.TryGetEntry(key,out selected)){selectedKey=key;selectedBar=target.GetComponentInParent<StructuralIdentity>();
                caseIndex=0;stationIndex=0;Highlight(target.bounds);Refresh();}
        }
        if(!Active)return;
        if(network){if(network.Busy)Status=network.Status;if(backendStatus)backendStatus.text=network.Status;
            if(selected!=null && selected.cases!=null && selected.cases.Length>0)capacityDiagram.Show(selectedBar,reinforcement.Result,selected.cases[caseIndex%selected.cases.Length]);else capacityDiagram.Clear();}
        Vector3 direction=Vector3.ProjectOnPlane(menu.forward,Vector3.up).normalized;
        // Walking requires looking at its control; looking away immediately stops horizontal motion.
        float movement=activated?(target==forwardButton?1:target==backButton?-1:0):0;
        if(PointLocomotion){
            if(movement!=0 && stepAvailable){SafeStep(direction*movement);stepAvailable=false;}
            movement=0;
        }
        Vector3 velocity=direction*(movement*WalkSpeed);
        if(player.controller.isGrounded && fallVelocity<0)fallVelocity=-2;
        fallVelocity-=18*Time.deltaTime;
        player.controller.Move((velocity+Vector3.up*fallVelocity)*Time.deltaTime);
        if(transform.position.y<-10){player.Teleport(player.world.Entrance);fallVelocity=0;}
    }
    void RecenterMenu(){if(menu)menu.rotation=Quaternion.Euler(0,player.eye.transform.eulerAngles.y,0);}
    void Highlight(Bounds bounds)
    {
        HighlightBounds(bounds);
    }
    void SafeStep(Vector3 direction)
    {
        Vector3 destination=transform.position+direction*2;
        float radius=player.controller.radius;
        Vector3 bottom=transform.position+player.controller.center-Vector3.up*(player.controller.height/2-radius);
        Vector3 top=bottom+Vector3.up*(player.controller.height-2*radius);
        var obstacles=Physics.CapsuleCastAll(bottom,top,radius,direction,2,~0,QueryTriggerInteraction.Ignore);
        if(obstacles.Any(h=>!h.collider.transform.IsChildOf(transform)) ||
           !Physics.Raycast(destination+Vector3.up*.4f,Vector3.down,out var floor,1,~0,QueryTriggerInteraction.Ignore) ||
           Vector3.Dot(floor.normal,Vector3.up)<.7f){Status="Paso bloqueado: obstáculo o falta de suelo";return;}
        player.Teleport(destination);fallVelocity=0;
    }
    void HighlightBounds(Bounds bounds)
    {
        Vector3 lo=bounds.min-Vector3.one*.025f,hi=bounds.max+Vector3.one*.025f;
        Vector3[] corners={new Vector3(lo.x,lo.y,lo.z),new Vector3(hi.x,lo.y,lo.z),new Vector3(hi.x,hi.y,lo.z),new Vector3(lo.x,hi.y,lo.z),
            new Vector3(lo.x,lo.y,hi.z),new Vector3(hi.x,lo.y,hi.z),new Vector3(hi.x,hi.y,hi.z),new Vector3(lo.x,hi.y,hi.z)};
        int[] route={0,1,2,3,0,4,5,1,5,6,2,6,7,3,7,4};outline.positionCount=route.Length;
        for(int i=0;i<route.Length;i++)outline.SetPosition(i,corners[route[i]]);
    }
    Material Material(Color color){var template=Resources.Load<Material>("CampusVRUnlit");
        var m=new Material(template?template.shader:Shader.Find("Unlit/Color")){color=color};materials.Add(m);return m;}
    TextMesh Text(string name,string value,Vector3 position,float size,Transform parent)
    {
        var go=new GameObject(name);go.transform.SetParent(parent,false);go.transform.localPosition=position;
        var t=go.AddComponent<TextMesh>();t.text=value;t.fontSize=48;t.characterSize=size;t.anchor=TextAnchor.UpperLeft;t.color=Color.white;
        t.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");go.GetComponent<Renderer>().sharedMaterial=t.font.material;
        return t;
    }
    Collider Button(string label,float x,float y,Action action,Transform parent=null)
    {
        var container=parent?parent:menu;
        var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name="VR · "+label;go.transform.SetParent(container,false);
        go.transform.localPosition=new Vector3(x,y,-.015f);go.transform.localScale=new Vector3(.34f,.12f,.025f);
        var renderer=go.GetComponent<Renderer>();renderer.sharedMaterial=Material(new Color(.06f,.12f,.17f));
        var collider=go.GetComponent<Collider>();collider.isTrigger=true;buttons[collider]=action;buttonRenderers[collider]=renderer;
        Text(label,label,new Vector3(x-.15f,y+.038f,-.035f),label.Length>=10?.010f:.012f,container);return collider;
    }
    LineRenderer Line(string name,Transform parent,Color color,float width)
    {
        var go=new GameObject(name);go.transform.SetParent(parent,false);var line=go.AddComponent<LineRenderer>();
        line.useWorldSpace=false;line.sharedMaterial=Material(color);line.startWidth=line.endWidth=width;
        line.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;line.receiveShadows=false;return line;
    }
    void DrawRing(LineRenderer line,float radius,float fraction)
    {
        int count=fraction>0?Mathf.Max(2,Mathf.CeilToInt(fraction*40)+1):0;line.positionCount=count;
        for(int i=0;i<count;i++){float a=i/(float)(count-1)*fraction*Mathf.PI*2;
            line.SetPosition(i,new Vector3(Mathf.Sin(a)*radius,Mathf.Cos(a)*radius,0));}
    }
    void BuildMenu()
    {
        menu=new GameObject("Campus · panel estructural VR").transform;
        var panel=GameObject.CreatePrimitive(PrimitiveType.Cube);panel.transform.SetParent(menu,false);
        panel.transform.localPosition=new Vector3(0,-.08f,0);
        panel.transform.localScale=new Vector3(1.52f,1.88f,.025f);Destroy(panel.GetComponent<Collider>());
        panel.GetComponent<Renderer>().sharedMaterial=Material(new Color(.025f,.045f,.065f));
        heading=Text("Título","CAMPUS / CARDBOARD",new Vector3(-.70f,.66f,-.04f),.018f,menu);
        backendStatus=Text("Estado backend","",new Vector3(-.70f,.73f,-.04f),.008f,menu);
        readout=Text("Resultado","",new Vector3(-.70f,.55f,-.04f),.012f,menu);
        sampleLabel=Text("Estación","",new Vector3(-.70f,-.06f,-.04f),.011f,menu);
        diagram=Line("Diagrama · estaciones OpenSees",menu,accent,.006f);
        var baseline=Line("Cero del diagrama",menu,new Color(.25f,.36f,.42f),.002f);baseline.positionCount=2;
        baseline.SetPositions(new[]{new Vector3(-.67f,.07f,-.048f),new Vector3(.67f,.07f,-.048f)});
        Button("Caso >",-.49f,-.27f,()=>{caseIndex++;stationIndex=0;Refresh();});
        Button("Diagrama",0,-.27f,()=>selector.gameObject.SetActive(true));
        Button("i / x / j >",.49f,-.27f,()=>{stationIndex++;Refresh();});
        forwardButton=Button("AVANZAR",-.49f,-.45f,()=>{});
        backButton=Button("RETROCEDER",0,-.45f,()=>{});
        Button("Estructura",.49f,-.45f,()=>{player.world.SetVRStructuralOnly(!player.world.StructuralOnly);});
        Button("Otro piso",-.49f,-.63f,NextFloor);
        Button("Recentrar",0,-.63f,RecenterMenu);
        Button("Soltar ficha",.49f,-.63f,()=>{selected=null;selectedBar=null;selectedKey="";outline.positionCount=0;beamDiagram.Clear();Refresh();});
        Button("Modo marcha",-.49f,-.81f,()=>{PointLocomotion=!PointLocomotion;Status=PointLocomotion?"Marcha por puntos · suelo validado":"Marcha continua";activated=false;gazeSince=Time.unscaledTime;Refresh();});
        Button("Backend",0,-.81f,()=>backendSelector.gameObject.SetActive(true));
        Button("Salir VR",.49f,-.81f,Exit);
        Text("Ayuda","Mirada 2 s / botón · apartar la mirada detiene",new Vector3(-.70f,-.91f,-.04f),.0085f,menu);
        reticle=Line("Mira VR",player.eye.transform,Color.white,.002f);reticle.transform.localPosition=Vector3.forward*.65f;DrawRing(reticle,.007f,1);
        progress=Line("Progreso de mirada",player.eye.transform,accent,.002f);progress.transform.localPosition=Vector3.forward*.65f;
        outline=Line("Elemento seleccionado · contorno",null,accent,.012f);outline.useWorldSpace=true;
        BuildSelector();
        BuildBackendSelector();
    }
    void BuildBackendSelector(){
        backendSelector=new GameObject("Backend Wi-Fi").transform;backendSelector.SetParent(menu,false);backendSelector.localPosition=new Vector3(0,-.1f,-.18f);
        var panel=GameObject.CreatePrimitive(PrimitiveType.Cube);panel.transform.SetParent(backendSelector,false);panel.transform.localScale=new Vector3(1.48f,1.2f,.025f);Destroy(panel.GetComponent<Collider>());panel.GetComponent<Renderer>().sharedMaterial=Material(new Color(.025f,.045f,.065f));
        Text("Backend título","PC / OPENSEES · WI-FI",new Vector3(-.68f,.56f,-.04f),.016f,backendSelector);
        Button("Configurar",-.49f,.31f,()=>StartCoroutine(ConfigureBackend()),backendSelector);
        Button("Q +2 kN/m",0,.31f,()=>Reanalyse(2),backendSelector);Button("Q +5 kN/m",.49f,.31f,()=>Reanalyse(5),backendSelector);
        Button("5/cara Ø28",-.49f,.13f,()=>RegenerateCapacity(.028),backendSelector);Button("5/cara Ø32",0,.13f,()=>RegenerateCapacity(.032),backendSelector);Button("5/cara Ø36",.49f,.13f,()=>RegenerateCapacity(.036),backendSelector);
        Button("Cancelar",-.49f,-.05f,()=>network.Cancel(),backendSelector);Button("Modelo base",0,-.05f,()=>{if(network.Busy){network.Cancel();return;}CampusData.Activate(null);UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);},backendSelector);
        Button("Cerrar",.49f,-.05f,()=>backendSelector.gameObject.SetActive(false),backendSelector);
        Text("Backend nota","Q: carga adicional uniforme, vertical −Z\nRefuerzo: sección nominal P–Mz · EI sin cambio\nRequiere columna HA y modelo base",new Vector3(-.68f,-.23f,-.04f),.011f,backendSelector);backendSelector.gameObject.SetActive(false);
    }
    IEnumerator ConfigureBackend(){
        string file=System.IO.Path.Combine(Application.persistentDataPath,"backend.json");
        if(System.IO.File.Exists(file)){var config=JsonUtility.FromJson<BackendConfig>(System.IO.File.ReadAllText(file));network.Server=config.server;network.Token=config.token;Status="Configuración local cargada";yield break;}
#if UNITY_ANDROID && !UNITY_EDITOR
        var keyboard=TouchScreenKeyboard.Open(network.Server,TouchScreenKeyboardType.URL,false,false,false);while(keyboard.status==TouchScreenKeyboard.Status.Visible)yield return null;if(keyboard.status!=TouchScreenKeyboard.Status.Done)yield break;network.Server=keyboard.text;
        keyboard=TouchScreenKeyboard.Open("",TouchScreenKeyboardType.Default,false,false,true);while(keyboard.status==TouchScreenKeyboard.Status.Visible)yield return null;if(keyboard.status==TouchScreenKeyboard.Status.Done)network.Token=keyboard.text;
#else
        Status="Configura backend.json en persistentDataPath; token no se guarda en el repositorio";
#endif
    }
    [Serializable] class BackendConfig { public string server,token; }
    void Reanalyse(double q){
        if(!selectedBar || !selectedBar.isBar || network.Busy){Status="Selecciona una barra y espera el cálculo";return;}
        int id=int.Parse(selectedBar.key.Split(':')[1]);string kind=selectedBar.description.StartsWith("Columna")?"Columna":"Viga";
        backendSelector.gameObject.SetActive(false);StartCoroutine(network.Submit(new AnalysisNetworkClient.Input{modelHash=AnalysisNetworkClient.BaseHash,
            changes=new[]{new AnalysisNetworkClient.Edit{kind=kind,id=id,changeLoad=true,q=q}}},(folder,manifest)=>{CampusData.Activate(folder);UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);}));
    }
    void RegenerateCapacity(double diameter){
        if(!selectedBar || !selectedBar.description.StartsWith("Columna") || !string.IsNullOrEmpty(CampusData.DirectoryPath)){Status="Refuerzo: selecciona una columna HA del modelo base";return;}
        backendSelector.gameObject.SetActive(false);reinforcement.Calculate(int.Parse(selectedBar.key.Split(':')[1]),5,diameter);
    }
    void BuildSelector()
    {
        selector=new GameObject("Elegir diagrama sobre el elemento").transform;selector.SetParent(menu,false);
        selector.localPosition=new Vector3(0,-.10f,-.12f);
        var panel=GameObject.CreatePrimitive(PrimitiveType.Cube);panel.transform.SetParent(selector,false);
        panel.transform.localPosition=new Vector3(0,.13f,.02f);panel.transform.localScale=new Vector3(1.48f,1.10f,.025f);
        Destroy(panel.GetComponent<Collider>());panel.GetComponent<Renderer>().sharedMaterial=Material(new Color(.025f,.045f,.065f));
        Text("Selector","DIAGRAMA SOBRE LA VIGA",new Vector3(-.68f,.61f,-.04f),.017f,selector);
        for(int index=0;index<CampusBeamDiagram.Modes.Length;index++){
            int mode=index;Button(CampusBeamDiagram.Modes[index],-.49f+(index%3)*.49f,.38f-(index/3)*.18f,
                ()=>{graphIndex=mode;stationIndex=0;selector.gameObject.SetActive(false);Refresh();},selector);
        }
        Button("Cerrar",.49f,.02f,()=>selector.gameObject.SetActive(false),selector);
        Text("Escalas","Esfuerzos: estaciones exportadas\nDeformada: Hermite nodal amplificada\nLa escala se indica junto al elemento",new Vector3(-.68f,-.11f,-.04f),.011f,selector);
        selector.gameObject.SetActive(false);
    }
    void NextFloor()
    {
        var floors=player.world.ElevatorFloors(0);
        int current=Mathf.RoundToInt((transform.position.y-CampusWorld.Finish)/CampusWorld.Storey);
        int next=floors.FirstOrDefault(f=>f>current);if(next==0)next=floors[0];
        var destination=player.world.ElevatorArrival(0,next);
        // Only destinations with a real platform are accepted; no arbitrary world-space teleport.
        if(!Physics.Raycast(destination+Vector3.up*.4f,Vector3.down,out var hit,1) || !hit.collider.name.Contains("Ascensor")){
            Status="Destino sin plataforma: traslado cancelado";Refresh();return;
        }
        player.Teleport(destination);fallVelocity=0;Status="Piso "+next;Refresh();
    }
    string Number(float value){float magnitude=Mathf.Abs(value);
        return value.ToString(magnitude>0 && (magnitude<.01f || magnitude>=1000000)?"0.##E+0":"0.###",CultureInfo.InvariantCulture);}
    void Refresh()
    {
        if(!readout)return;diagram.positionCount=0;sampleLabel.text="";beamDiagram.Clear();
        if(selected==null){heading.text="CAMPUS / CARDBOARD";readout.text=Status+"\nMira una viga, columna, muro o losa.\nLa ficha conserva el identificador del modelo.\nResultados verificados; reanálisis opcional en PC Wi-Fi.";return;}
        var cases=selected.cases;
        if(cases==null || cases.Length==0){readout.text=selectedKey+"\nSin resultados exportados.";return;}
        var result=cases[caseIndex%cases.Length];
        beamDiagram.Show(selectedBar,result,graphIndex,player.eye.transform.position);
        beamDiagram.SetStation(stationIndex);
        heading.text="CAMPUS / "+selectedKey+" / "+result.name;
        string values="";
        if(result.endI!=null && result.endI.Length>=6)values="Acciones i · ejes locales\nN "+Number(result.endI[0])+" kN   Vy "+Number(result.endI[1])+" kN\nMy "+Number(result.endI[4])+" kN·m   Mz "+Number(result.endI[5])+" kN·m";
        else if(result.wall!=null && result.wall.Length>0)values="Paño: demanda P "+Number(result.wall[0])+" kN";
        else values="G "+Number(selected.loadG)+" kN   Q "+Number(selected.loadQ)+" kN\nLosa de reparto: sin esfuerzos de placa.";
        string movement=result.moveI!=null && result.moveI.Length>=3?"\nUx/y/z [mm] "+Number(result.moveI[0]*1000)+" / "+Number(result.moveI[1]*1000)+" / "+Number(result.moveI[2]*1000):"";
        readout.text=selected.title+"\n"+values+movement;
        if(graphIndex==7){sampleLabel.text="Diagramas ocultos.";return;}
        CampusLaser.Graph graph;
        if(graphIndex==6){
            graph=new CampusLaser.Graph{label=beamDiagram.Label,unit=beamDiagram.Unit,values=beamDiagram.Values,stations=beamDiagram.Stations};
            if(graph.values.Length==0){sampleLabel.text=beamDiagram.Description;return;}
        }else{
            if(result.graphs==null || graphIndex>=result.graphs.Length){sampleLabel.text="Sin diagrama exportado para este elemento/caso.";return;}
            graph=result.graphs[graphIndex];
        }
        if(graph.values==null || graph.values.Length==0){sampleLabel.text="Sin estaciones exportadas.";return;}
        // Preserve every exported station and sign. Normalization is display-only.
        float range=Mathf.Max(.000001f,graph.values.Max(v=>Mathf.Abs(v)));
        int count=graph.values.Length;diagram.positionCount=count;
        for(int i=0;i<count;i++){
            float station=graph.stations!=null && graph.stations.Length==count?graph.stations[i]:(count==1?0:i/(float)(count-1));
            diagram.SetPosition(i,new Vector3(-.67f+station*1.34f,.07f+graph.values[i]/range*.10f,-.05f));
        }
        int index=stationIndex%count;
        float x=graph.stations!=null && graph.stations.Length==count?graph.stations[index]:(count==1?0:index/(float)(count-1));
        sampleLabel.text="Sección "+graph.label+" ["+graph.unit+"] · x/L="+Number(x)+" · valor="+Number(graph.values[index])+"\nmín="+Number(graph.values.Min())+" · máx="+Number(graph.values.Max());
    }
    void OnGUI()
    {
        if(Active || starting)return;
#if UNITY_ANDROID && !UNITY_EDITOR
        GUI.Label(new Rect(24,24,Screen.width-48,90),"CAMPUS / CARDBOARD\n"+Status);
        if(GUI.Button(new Rect(24,130,Screen.width-48,70),"Entrar a Google Cardboard"))Enter();
#else
        if(GUI.Button(new Rect(Screen.width-270,20,250,32),"F7 · Previsualizar interacción VR"))Enter();
#endif
    }
    void OnDisable(){if(Active)Exit();}
    IEnumerator ValidatePreview()
    {
        yield return null;Enter();
        while(starting)yield return null;
        yield return null;
        var checks=new Dictionary<string,bool>();
        checks["preview_not_native_stereo"]=Active && !NativeStereo;
        checks["desktop_controls_suspended"]=player.VRControlled && !player.InspectionAllowed && !player.ThirdPerson;
        checks["exported_id_and_diagram"]=inspector.TryGetEntry("E:207",out selected);
        selectedBar=UnityEngine.Object.FindObjectsByType<StructuralIdentity>(FindObjectsSortMode.None).FirstOrDefault(id=>id.key=="E:207");
        selectedKey="E:207";Refresh();
        checks["all_stations_preserved"]=selected!=null && selected.cases!=null && selected.cases.Length>0 &&
            selected.cases[0].graphs!=null && selected.cases[0].graphs.Length>0 && diagram.positionCount==selected.cases[0].graphs[graphIndex%selected.cases[0].graphs.Length].values.Length;
        CapturePreview();
        checks["beam_overlay_stations"]=beamDiagram.Curve.Length==selected.cases[0].graphs[graphIndex].values.Length;
        var savedValues=selected.cases[0].graphs[graphIndex].values;
        checks["beam_overlay_values_unchanged"]=beamDiagram.Values.SequenceEqual(savedValues);
        graphIndex=6;Refresh();
        var response=selected.cases[0];
        Vector3 start=selectedBar.start,end=selectedBar.end;
        checks["deformed_i_matches_export"]=Vector3.Distance(beamDiagram.Curve[0],start+CampusBeamDiagram.ToUnity(CampusBeamDiagram.Vector(response.moveI))*beamDiagram.VisualScale)<1e-5f;
        checks["deformed_j_matches_export"]=Vector3.Distance(beamDiagram.Curve.Last(),end+CampusBeamDiagram.ToUnity(CampusBeamDiagram.Vector(response.moveJ))*beamDiagram.VisualScale)<1e-5f;
        float[] rigid={.001f,.002f,.003f,0,0,0};
        checks["rigid_translation_preserved"]=Vector3.Distance(CampusBeamDiagram.Displacement(.4f,4,Vector3.right,rigid,rigid),new Vector3(.001f,.002f,.003f))<1e-7f;
        float[] bendI={0,0,0,0,.01f,0},bendJ={0,0,0,0,-.01f,0};
        checks["rotation_sign_open_to_unity"]=Vector3.Distance(CampusBeamDiagram.ToUnity(CampusBeamDiagram.Displacement(.5f,4,Vector3.right,bendI,bendJ)),new Vector3(0,-.01f,0))<1e-7f;
        graphIndex=7;Refresh();checks["hide_diagram"]=beamDiagram.Curve.Length==0;
        graphIndex=4;Refresh();
        CaptureBeamEvidence();
        var origin=transform.position;
        Vector3 aim=forwardButton.bounds.center-player.eye.transform.position;
        previewYaw=Quaternion.LookRotation(aim).eulerAngles.y;
        previewPitch=Quaternion.LookRotation(aim).eulerAngles.x;
        if(previewPitch>180)previewPitch-=360;
        yield return new WaitForSecondsRealtime(1f);
        checks["no_early_selection"]=Vector3.ProjectOnPlane(transform.position-origin,Vector3.up).magnitude<.01f;
        float deadline=Time.realtimeSinceStartup+Dwell+3;
        while(Time.realtimeSinceStartup<deadline && Vector3.ProjectOnPlane(transform.position-origin,Vector3.up).magnitude<=.04f)yield return null;
        checks["gaze_locomotion"]=Vector3.ProjectOnPlane(transform.position-origin,Vector3.up).magnitude>.04f;
        checks["forward_direction"]=Vector3.Dot(transform.position-origin,menu.forward)>.04f;
        previewYaw+=150;
        yield return null;var stopped=transform.position;
        yield return new WaitForSecondsRealtime(.2f);
        checks["look_away_stops"]=Vector3.ProjectOnPlane(transform.position-stopped,Vector3.up).magnitude<.01f;
        yield return null;Physics.SyncTransforms();
        origin=transform.position;aim=backButton.bounds.center-player.eye.transform.position;
        previewYaw=Quaternion.LookRotation(aim).eulerAngles.y;previewPitch=Quaternion.LookRotation(aim).eulerAngles.x;
        if(previewPitch>180)previewPitch-=360;
        deadline=Time.realtimeSinceStartup+Dwell+3;
        while(Time.realtimeSinceStartup<deadline && Vector3.ProjectOnPlane(transform.position-origin,Vector3.up).magnitude<=.04f)yield return null;
        checks["backward_direction"]=Vector3.Dot(transform.position-origin,menu.forward)<-.04f;
        checks["two_second_dwell"]=Mathf.Approximately(Dwell,2f);
        // Isolated collider fixture: exercise point mode without depending on campus geometry.
        Vector3 savedPosition=transform.position;
        var testFloor=GameObject.CreatePrimitive(PrimitiveType.Cube);testFloor.name="VR_TEST_FLOOR";testFloor.transform.position=new Vector3(1000,-.1f,1000);testFloor.transform.localScale=new Vector3(12,.2f,12);
        player.Teleport(new Vector3(1000,.04f,1000));Physics.SyncTransforms();origin=transform.position;SafeStep(Vector3.forward);
        checks["point_step_two_metres"]=Mathf.Abs(Vector3.Distance(origin,transform.position)-2)<.01f;
        var obstacle=GameObject.CreatePrimitive(PrimitiveType.Cube);obstacle.name="VR_TEST_OBSTACLE";obstacle.transform.position=transform.position+Vector3.forward+Vector3.up;obstacle.transform.localScale=new Vector3(1,2,.2f);Physics.SyncTransforms();origin=transform.position;SafeStep(Vector3.forward);
        checks["point_step_collision_rejected"]=Vector3.Distance(origin,transform.position)<.01f;
        obstacle.SetActive(false);testFloor.SetActive(false);Physics.SyncTransforms();SafeStep(Vector3.right);
        checks["point_step_void_rejected"]=Vector3.Distance(origin,transform.position)<.01f;
        Destroy(obstacle);Destroy(testFloor);player.Teleport(savedPosition);
        NextFloor();checks["floor_platform"]=Mathf.Abs(transform.position.y-(CampusWorld.Storey+CampusWorld.Finish+.04f))<.05f;
        player.world.SetVRStructuralOnly(true);checks["structural_view"]=player.world.StructuralOnly && player.world.structure.Find("Vigas_originales_IDS").gameObject.activeInHierarchy;
        Exit();checks["restore_desktop"]=!Active && !player.VRControlled && player.world.StructuralOnly==structuralBefore;
        Enter();while(starting)yield return null;yield return null;
        checks["repeated_enter_exit"]=Active;Exit();
        if(Environment.GetCommandLineArgs().Contains("-campus-vr-network-check"))yield return ValidateNetwork(checks);
        foreach(var check in checks)Debug.Log("CAMPUS_VR_CHECK "+check.Key+"="+check.Value);
        bool passed=checks.Values.All(v=>v);Debug.Log("CAMPUS_VR_CHECK_COMPLETE passed="+passed);
#if UNITY_EDITOR
        if(Application.isBatchMode)UnityEditor.EditorApplication.Exit(passed?0:2);
#endif
    }
    void CaptureBeamEvidence()
    {
        var bar=UnityEngine.Object.FindObjectsByType<StructuralIdentity>(FindObjectsSortMode.None)
            .Where(id=>id.isBar && Vector3.Distance(id.start,id.end)>5 && Mathf.Abs(id.start.y-id.end.y)<.01f)
            .Where(id=>inspector.TryGetEntry(id.key,out var entry) && entry.cases!=null && entry.cases.Length>0 && entry.cases[0].graphs!=null && entry.cases[0].graphs.Length>5)
            .OrderByDescending(id=>{inspector.TryGetEntry(id.key,out var entry);return entry.cases[0].graphs.Skip(4).Take(2).Max(g=>g.values.Max(v=>Mathf.Abs(v)));}).FirstOrDefault();
        if(!bar || !inspector.TryGetEntry(bar.key,out var data))return;
        Vector3 position=player.eye.transform.position;Quaternion rotation=player.eye.transform.rotation;
        bool structural=player.world.StructuralOnly;
        try{
            player.world.SetVRStructuralOnly(true);menu.gameObject.SetActive(false);
            var ex=(bar.end-bar.start).normalized;
            var normal=Vector3.Cross(ex,Vector3.up).normalized;
            var middle=(bar.start+bar.end)*.5f;
            player.eye.transform.position=middle+normal*Mathf.Max(2.5f,Vector3.Distance(bar.start,bar.end)*.85f)+Vector3.up*.3f;
            player.eye.transform.LookAt(middle);
            int bending=data.cases[0].graphs[4].values.Max(v=>Mathf.Abs(v))>=data.cases[0].graphs[5].values.Max(v=>Mathf.Abs(v))?4:5;
            beamDiagram.Show(bar,data.cases[0],bending,player.eye.transform.position);
            CapturePreview("CampusBeamMoment.png");
            beamDiagram.Show(bar,data.cases[0],6,player.eye.transform.position);
            CapturePreview("CampusBeamDeformation.png");
        }finally{
            player.world.SetVRStructuralOnly(structural);menu.gameObject.SetActive(true);
            player.eye.transform.position=position;player.eye.transform.rotation=rotation;Refresh();
        }
    }
    IEnumerator ValidateNetwork(Dictionary<string,bool> checks){
        string path=Environment.GetCommandLineArgs().First(a=>a.StartsWith("--backend-config=")).Substring("--backend-config=".Length);
        var config=JsonUtility.FromJson<BackendConfig>(System.IO.File.ReadAllText(path));network.Server=config.server;network.Token=config.token;
        bool accepted=false;
        yield return network.Submit(new AnalysisNetworkClient.Input{modelHash=AnalysisNetworkClient.BaseHash,changes=new[]{new AnalysisNetworkClient.Edit{kind="Viga",id=207,changeLoad=true,q=2}}},
            (folder,manifest)=>{accepted=System.IO.File.Exists(System.IO.Path.Combine(folder,"inspeccion_estructural.json")) && manifest.modelHash!=AnalysisNetworkClient.BaseHash;});
        checks["http_opensees_new_verified_revision"]=accepted;
        Debug.Log("HONORS_NETWORK_ANALYSIS "+network.Status);
        accepted=false;
        yield return network.Submit(new AnalysisNetworkClient.Input{modelHash=AnalysisNetworkClient.BaseHash,operation="capacity",elementTag=1,barsPerFace=5,diameter_m=.032},
            (folder,manifest)=>{var result=JsonUtility.FromJson<ReinforcementPanel.Snapshot>(System.IO.File.ReadAllText(System.IO.Path.Combine(folder,"capacity.json")));accepted=result.elementTag==1 && result.curves.Length==2 && !result.globalStiffnessChanged;});
        checks["http_reinforcement_regeneration"]=accepted;
        Debug.Log("HONORS_NETWORK_CAPACITY "+network.Status);
    }
    void CapturePreview(string name="CampusCardboardPreview.png")
    {
        // Render explicitly: a batch editor has no Game view to capture.
        var target=RenderTexture.GetTemporary(1280,900,24);
        var previous=player.eye.targetTexture;var active=RenderTexture.active;
        var pixels=new Texture2D(1280,900,TextureFormat.RGB24,false);
        try{
            player.eye.targetTexture=target;player.eye.Render();RenderTexture.active=target;
            pixels.ReadPixels(new Rect(0,0,1280,900),0,0);pixels.Apply();
            string path=System.IO.Path.Combine(Application.temporaryCachePath,name);
            System.IO.File.WriteAllBytes(path,pixels.EncodeToPNG());Debug.Log("CAMPUS_VR_PREVIEW_IMAGE "+path);
        }finally{player.eye.targetTexture=previous;RenderTexture.active=active;RenderTexture.ReleaseTemporary(target);Destroy(pixels);}
    }
}
