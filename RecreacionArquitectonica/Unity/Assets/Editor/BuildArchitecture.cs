using System;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;

public static class BuildArchitecture {
    static Transform root,site,part;
    static Material orange,ochre,concrete,white,metal,dark,roof,grass,bark,leaf,paving,asphalt;
    static Material[] glass;
    static int objects;
    static Material Mat(string name,Color color,float metallic=0,float smooth=.25f){
        string path="Assets/Materiales/"+name+".mat";
        Material m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(!m){m=new Material(Shader.Find("Standard"));AssetDatabase.CreateAsset(m,path);}
        m.color=color;m.SetFloat("_Metallic",metallic);m.SetFloat("_Glossiness",smooth);return m;
    }
    static GameObject Box(string name,Vector3 p,Vector3 s,Material m,Transform parent=null){
        var o=GameObject.CreatePrimitive(PrimitiveType.Cube);o.name=name;o.transform.SetParent(parent?parent:part,false);o.transform.position=p;o.transform.localScale=s;o.GetComponent<Renderer>().sharedMaterial=m;objects++;return o;
    }
    static Transform Group(string name,Transform parent=null){var o=new GameObject(name);o.transform.SetParent(parent?parent:root,false);return o.transform;}
    static void Bar(string name,Vector3 a,Vector3 b,float width,Material mat,Transform parent=null){var o=Box(name,(a+b)/2,new Vector3(width,Vector3.Distance(a,b),width),mat,parent);o.transform.rotation=Quaternion.FromToRotation(Vector3.up,b-a);}
    static GameObject Cylinder(string name,Vector3 p,float radius,float height,Material mat,Transform parent=null){
        var o=GameObject.CreatePrimitive(PrimitiveType.Cylinder);o.name=name;o.transform.SetParent(parent?parent:part,false);o.transform.position=p;o.transform.localScale=new Vector3(radius*2,height/2,radius*2);o.GetComponent<Renderer>().sharedMaterial=mat;objects++;return o;
    }
    static Material Glass(int seed){return glass[Math.Abs(seed)%glass.Length];}
    static void Curtain(float x0,float x1,float y0,float y1,float z,int seed){
        float cell=1.48f;int cols=Mathf.CeilToInt((x1-x0)/cell);float dx=(x1-x0)/cols;
        int rows=Mathf.CeilToInt((y1-y0)/1.98f);float dy=(y1-y0)/rows;
        for(int i=0;i<cols;i++)for(int j=0;j<rows;j++){
            Box("Paño de vidrio",new Vector3(x0+(i+.5f)*dx,y0+(j+.5f)*dy,z),new Vector3(dx-.055f,dy-.055f,.07f),Glass(seed+i*13+j*7));
            // Subtle reflected sky bands make each pane readable without photographic projection.
        }
        for(int i=0;i<=cols;i++)Box("Montante vertical",new Vector3(x0+i*dx,(y0+y1)/2,z-.065f),new Vector3(.055f,y1-y0,.12f),metal);
        for(int j=0;j<=rows;j++)Box("Travesaño",new Vector3((x0+x1)/2,y0+j*dy,z-.065f),new Vector3(x1-x0,.055f,.12f),metal);
    }
    static void SideCurtain(float x,float z0,float z1,float y0,float y1){
        int count=Mathf.CeilToInt((z1-z0)/1.5f),rows=Mathf.CeilToInt((y1-y0)/1.98f);float dz=(z1-z0)/count,dy=(y1-y0)/rows;
        for(int i=0;i<count;i++)for(int j=0;j<rows;j++)Box("Vidrio lateral",new Vector3(x,y0+(j+.5f)*dy,z0+(i+.5f)*dz),new Vector3(.07f,dy-.05f,dz-.05f),Glass(i*7+j));
        for(int i=0;i<=count;i++)Box("Montante lateral",new Vector3(x,(y0+y1)/2,z0+i*dz),new Vector3(.12f,y1-y0,.05f),metal);
        for(int j=0;j<=rows;j++)Box("Travesaño lateral",new Vector3(x,y0+j*dy,(z0+z1)/2),new Vector3(.12f,.05f,z1-z0),metal);
    }
    static void PanelSlope(string name,Vector3 a,Vector3 b,float height,float thickness,Material mat){
        // Vertical panel with a sloped bottom and top (extruded parallelogram).
        Vector3 t=Vector3.forward*thickness/2;
        Vector3[] v={a-t,b-t,b+Vector3.up*height-t,a+Vector3.up*height-t,a+t,b+t,b+Vector3.up*height+t,a+Vector3.up*height+t};
        int[] tris={0,2,1,0,3,2,4,5,6,4,6,7,0,1,5,0,5,4,3,7,6,3,6,2,0,4,7,0,7,3,1,2,6,1,6,5};
        var mesh=new Mesh{name=name};mesh.vertices=v;mesh.triangles=tris;mesh.RecalculateNormals();mesh.RecalculateBounds();
        string path="Assets/Geometria/Panel_"+objects+".asset";AssetDatabase.CreateAsset(mesh,path);
        var o=new GameObject(name);o.transform.SetParent(part,false);o.AddComponent<MeshFilter>().sharedMesh=mesh;o.AddComponent<MeshRenderer>().sharedMaterial=mat;objects++;
    }
    static void StairX(float x0,float x1,float y0,float y1,float z,float width){
        int n=Mathf.CeilToInt(Math.Abs(y1-y0)/.18f);float dx=(x1-x0)/n;
        for(int i=0;i<n;i++){float y=Mathf.Lerp(y0,y1,(i+1f)/n);Box("Peldaño exterior",new Vector3(x0+(i+.5f)*dx,y-.1f,z),new Vector3(Math.Abs(dx)+.01f,.2f,width),concrete);}
        PanelSlope("Zanca de hormigón",new Vector3(x0,y0-.2f,z),new Vector3(x1,y1-.2f,z),.2f,width,concrete);
        PanelSlope("Antepecho naranja escalera",new Vector3(x0,y0,z-width/2),new Vector3(x1,y1,z-width/2),1.02f,.09f,orange);
        Bar("Pasamanos exterior",new Vector3(x0,y0+1.06f,z-width/2),new Vector3(x1,y1+1.06f,z-width/2),.065f,metal);
        for(int i=0;i<=Mathf.Abs(x1-x0);i+=2){float u=i/Math.Abs(x1-x0);Bar("Junta panel escalera",new Vector3(Mathf.Lerp(x0,x1,u),Mathf.Lerp(y0,y1,u),z-width/2-.05f),new Vector3(Mathf.Lerp(x0,x1,u),Mathf.Lerp(y0,y1,u)+1.02f,z-width/2-.05f),.018f,dark);}
    }
    static void Landing(float x0,float x1,float y,float z,float width){
        Box("Descanso exterior",new Vector3((x0+x1)/2,y-.12f,z),new Vector3(x1-x0,.24f,width),concrete);
        Box("Antepecho descanso",new Vector3((x0+x1)/2,y+.51f,z-width/2),new Vector3(x1-x0,1.02f,.09f),orange);
        Box("Pasamanos descanso",new Vector3((x0+x1)/2,y+1.06f,z-width/2),new Vector3(x1-x0,.065f,.065f),metal);
        for(float x=x0;x<x1;x+=1.25f)Box("Junta panel",new Vector3(x,y+.51f,z-width/2-.05f),new Vector3(.018f,1.02f,.025f),dark);
    }
    static void Table(float x,float y,float z,bool umbrella=false){
        Cylinder("Mesa de terraza",new Vector3(x,y+.75f,z),.44f,.055f,dark);
        Cylinder("Pie mesa",new Vector3(x,y+.375f,z),.045f,.75f,metal);
        for(int i=0;i<4;i++){
            float a=i*Mathf.PI/2;Vector3 p=new Vector3(x+Mathf.Cos(a)*.8f,y,z+Mathf.Sin(a)*.8f);
            Box("Asiento",p+Vector3.up*.44f,new Vector3(.39f,.045f,.39f),metal);
            Box("Respaldo",p+new Vector3(Mathf.Cos(a)*.18f,.72f,Mathf.Sin(a)*.18f),new Vector3(.36f,.48f,.055f),metal).transform.rotation=Quaternion.Euler(0,90-i*90,0);
            for(int j=0;j<4;j++)Box("Pata silla",p+new Vector3(j%2==0?-.15f:.15f,.22f,j<2?-.15f:.15f),new Vector3(.026f,.44f,.026f),metal);
        }
        if(umbrella){Cylinder("Mástil quitasol",new Vector3(x,y+1.25f,z),.03f,2.5f,metal);var o=Cylinder("Quitasol",new Vector3(x,y+2.55f,z),1.4f,.09f,roof);}
    }
    static void Tree(float x,float z,float h,int seed){
        Cylinder("Tronco",new Vector3(x,h*.28f-1,z),.16f,h*.58f,bark,site);
        for(int i=0;i<4;i++){
            var o=GameObject.CreatePrimitive(PrimitiveType.Sphere);o.name="Copa de árbol";o.transform.SetParent(site,false);o.transform.position=new Vector3(x+Mathf.Sin(seed+i*4)*h*.15f,h*.65f-1+i*.2f,z+Mathf.Cos(seed+i)*h*.14f);o.transform.localScale=new Vector3(h*.6f,h*.55f,h*.58f);o.GetComponent<Renderer>().sharedMaterial=leaf;objects++;
        }
    }
    static void Materials(){
        orange=Mat("Terracota naranja",new Color(.66f,.20f,.085f));ochre=Mat("Revestimiento ocre",new Color(.55f,.35f,.17f));
        concrete=Mat("Hormigón claro",new Color(.66f,.68f,.66f));white=Mat("Remates blancos",new Color(.83f,.85f,.83f),.2f,.45f);
        metal=Mat("Perfilería antracita",new Color(.10f,.14f,.16f),.65f,.6f);dark=Mat("Juntas y sombras",new Color(.055f,.065f,.066f));
        roof=Mat("Cubierta zinc",new Color(.62f,.67f,.70f),.65f,.45f);grass=Mat("Césped",new Color(.22f,.31f,.125f));bark=Mat("Corteza",new Color(.23f,.18f,.12f));leaf=Mat("Follaje",new Color(.14f,.24f,.10f));
        paving=Mat("Pavimento",new Color(.49f,.48f,.43f));asphalt=Mat("Asfalto",new Color(.20f,.215f,.22f));
        glass=new Material[9];for(int i=0;i<glass.Length;i++){float k=(i-4)*.009f;glass[i]=Mat("Vidrio "+i,new Color(.16f+k,.25f+k,.32f+k),.65f,.92f);}
    }
    static void GlassBuilding(){
        part=Group("LT1 - Fachada acristalada y extremo en voladizo");
        // Reference structural grid: 50 m long, 16.15 m wide, five 3.96 m levels.
        for(int level=0;level<=5;level++){
            float y=level*3.96f;
            Box("Losa nivel "+level,new Vector3(20,y-.15f,8.075f),new Vector3(40,.3f,16.15f),concrete);
            if(level>=4)Box("Losa voladizo extremo",new Vector3(45,y-.15f,8.075f),new Vector3(10,.3f,16.15f),concrete);
        }
        Box("Interior en sombra",new Vector3(20,9.9f,8),new Vector3(39.5f,19.5f,15.3f),dark);
        Curtain(0,12,0,19.5f,-.1f,1);Curtain(15,40,3.96f,19.5f,-.1f,4);Curtain(15,29,0,3.96f,-.1f,2);
        SideCurtain(.035f,0,16.15f,0,19.5f);
        for(int l=0;l<5;l++){
            float y=l*3.96f;
            Box("Marco portal hormigón",new Vector3(13.5f,y+1.98f,.03f),new Vector3(3,3.96f,.32f),concrete);
            Box("Puerta acristalada",new Vector3(13.5f,y+1.43f,-.2f),new Vector3(1.7f,2.85f,.1f),Glass(3));
            for(float x=12.63f;x<=14.4f;x+=.87f)Box("Marco puerta",new Vector3(x,y+1.43f,-.28f),new Vector3(.065f,2.85f,.08f),white);
        }
        // Projecting glazed box seen in both front photos.
        Box("Volumen saliente - losa",new Vector3(25.5f,11.82f,-1.85f),new Vector3(11,.3f,3.7f),white);
        Box("Volumen saliente - cubierta",new Vector3(25.5f,19.72f,-1.85f),new Vector3(11.4f,.22f,4.1f),white);
        Curtain(20,31,11.98f,19.58f,-3.75f,6);SideCurtain(20,-3.75f,0,11.98f,19.58f);SideCurtain(31,-3.75f,0,11.98f,19.58f);
        for(float x=20;x<=31;x+=1)Box("Junta cubierta mirador",new Vector3(x,19.85f,-1.85f),new Vector3(.025f,.035f,4.1f),roof);
        // Orange end: upper volume over tall piers, with a clear cantilever.
        for(float x=40;x<=48.1f;x+=2.7f){
            Box("Machón terracota superior",new Vector3(x+.8f,17.77f,-.22f),new Vector3(1.6f,3.96f,.7f),orange);
            if(x<47)Box("Ventana entre machones",new Vector3(x+2.15f,17.77f,.12f),new Vector3(1.05f,3.65f,.08f),Glass(3));
        }
        for(float z=1;z<15;z+=3){Box("Aleta lateral superior",new Vector3(50.1f,17.77f,z),new Vector3(.7f,3.96f,1.65f),orange);Box("Ventana lateral superior",new Vector3(49.95f,17.77f,z+1.9f),new Vector3(.08f,3.65f,1.2f),Glass(5));}
        Box("Sofito blanco del voladizo",new Vector3(45,15.65f,8),new Vector3(10.7f,.38f,16.65f),white);
        for(float z=1;z<=14;z+=6.5f)Box("Pilar alto naranja",new Vector3(41,9.8f,z),new Vector3(1.5f,11.68f,1.35f),orange);
        Box("Acceso bajo voladizo",new Vector3(35,5.1f,-.15f),new Vector3(9.7f,2.2f,.13f),Glass(4));
        // Photo-based zig-zag circulation. Landings align with the reference storeys.
        Box("Conexión puerta escalera",new Vector3(13.5f,15.72f,-2.6f),new Vector3(3,.24f,5.2f),concrete);
        Landing(12,15,15.84f,-5.2f,2.4f);StairX(15,23,15.84f,11.88f,-5.2f,2.4f);
        Landing(23,33,11.88f,-5.2f,2.4f);StairX(33,41,11.88f,7.92f,-5.2f,2.4f);
        Landing(39,43,7.92f,-6.8f,5.6f);StairX(41,31,7.92f,3.96f,-8.5f,3.8f);
        Landing(31,49,3.96f,-10,7.5f);StairX(31,22,3.96f,0,-10,4.5f);
        Box("Cierre naranja plataforma",new Vector3(40,1.8f,-13.75f),new Vector3(18,3.6f,.18f),orange);
        for(float x=32;x<49;x+=5)Box("Apoyo terraza",new Vector3(x,1.85f,-5.5f),new Vector3(.4f,3.7f,.4f),concrete);
        for(int i=0;i<4;i++)for(int j=0;j<2;j++)Table(9+i*3.8f,.03f,-7-j*3.5f,(i+j)%3==0);
    }
    static void OchreFacade(){
        part=Group("LT2 acristalado y fachada ocre continua al lado contrario");
        // West block is separated from LT1 by the structural 10 cm joint.
        Box("Cuerpo LT2",new Vector3(-16.075f,9.8f,8.075f),new Vector3(31.95f,19.6f,16.15f),dark);
        Curtain(-32.05f,-.1f,0,19.5f,-.13f,3);
        for(int l=1;l<=5;l++)Box("Banda LT2 fachada vidrio",new Vector3(-16.075f,l*3.96f-.07f,-.18f),new Vector3(31.95f,.09f,.15f),metal);
        for(int block=0;block<2;block++){
            int side=0;float z=16.28f;float from=block==0?-32.05f:0,to=block==0?-.1f:50;
            int bays=Mathf.RoundToInt((to-from)/2.7f);float dx=(to-from)/bays;
            for(int i=0;i<bays;i++){
                float x=from+i*dx;
                for(int l=0;l<5;l++){
                    Box("Panel ocre",new Vector3(x+dx*.30f,l*3.96f+1.96f,z),new Vector3(dx*.60f,3.9f,.38f),ochre);
                    Box("Ventana vertical",new Vector3(x+dx*.80f,l*3.96f+1.96f,z+(side==0?-.18f:.18f)),new Vector3(dx*.36f,3.75f,.08f),Glass(i+l));
                    for(int j=1;j<3;j++)Box("Travesaño ventana",new Vector3(x+dx*.80f,l*3.96f+j*1.32f,z+(side==0?.04f:-.04f)),new Vector3(dx*.39f,.065f,.09f),white);
                }
            }
            for(int l=1;l<=5;l++)Box("Banda horizontal blanca",new Vector3((from+to)/2,l*3.96f-.07f,z),new Vector3(to-from,.12f,.7f),white);
        }
        for(float z=0;z<16;z+=2.65f){Box("Panel testero ocre",new Vector3(-32.2f,9.9f,z+.8f),new Vector3(.4f,19.7f,1.5f),ochre);Box("Ventana testero",new Vector3(-32.08f,9.9f,z+2.1f),new Vector3(.1f,19.5f,.85f),Glass(2));}
        for(int l=1;l<=5;l++)Box("Banda testero",new Vector3(-32.25f,l*3.96f-.07f,8.07f),new Vector3(.6f,.12f,16.4f),white);
        Box("Junta de dilatación 10 cm",new Vector3(-.05f,9.9f,16.2f),new Vector3(.1f,19.8f,.055f),dark);
        Box("Terraza lado cancha",new Vector3(2,3.81f,21.2f),new Vector3(45,.3f,9.6f),concrete);
        Box("Basamento terraza",new Vector3(2,1.8f,25.9f),new Vector3(45,3.6f,.3f),ochre);
        for(float x=-18;x<=23;x+=2.2f){Bar("Poste baranda",new Vector3(x,3.96f,25.85f),new Vector3(x,4.98f,25.85f),.045f,metal);}
        Bar("Pasamanos terraza",new Vector3(-20.5f,4.98f,25.85f),new Vector3(24.5f,4.98f,25.85f),.07f,metal);
        for(int i=0;i<9;i++)for(int j=0;j<2;j++)Table(-17+i*4.5f,3.96f,19+j*3.8f);
        StairX(24.5f,34,3.96f,0,23.3f,4.4f);
    }
    static void Roof(){
        part=Group("Cubierta metálica y equipos");
        for(int block=0;block<2;block++){
            float x0=block==0?-32.4f:0,x1=block==0?-.1f:50.5f;
            Box("Cubierta",new Vector3((x0+x1)/2,19.95f,8.075f),new Vector3(x1-x0,.22f,16.9f),roof);
            for(float x=x0;x<x1;x+=.45f)Box("Nervadura zinc",new Vector3(x,20.1f,8.075f),new Vector3(.026f,.07f,16.6f),white);
            for(int s=0;s<2;s++)Box("Pretil longitudinal",new Vector3((x0+x1)/2,20.24f,s==0?-.35f:16.5f),new Vector3(x1-x0,.5f,.16f),white);
            for(float x=x0;x<=x1;x+=x1-x0)Box("Pretil testero",new Vector3(x,20.24f,8.075f),new Vector3(.16f,.5f,16.9f),white);
        }
        Box("Caja técnica de cubierta",new Vector3(7,21.4f,9.5f),new Vector3(3.3f,2.8f,3.8f),white);
        for(int i=0;i<8;i++){
            float x=-17+i*4.4f;Box("Unidad climatización",new Vector3(x,20.65f,10.5f),new Vector3(2.2f,1.1f,1.6f),roof);
            Cylinder("Ventilador superior",new Vector3(x,21.22f,10.5f),.55f,.08f,metal);
            for(int j=0;j<5;j++)Box("Rejilla unidad",new Vector3(x,20.3f+j*.16f,9.68f),new Vector3(1.9f,.035f,.04f),metal);
        }
        for(int j=0;j<11;j++)Box("Lama pantalla equipos",new Vector3(1,20.35f+j*.13f,8.2f),new Vector3(40,.07f,.14f),roof);
        Box("Conducto técnico",new Vector3(1,20.45f,12.7f),new Vector3(39,.65f,.75f),white);
    }
    static void Site(){
        part=site=Group("Entorno aproximado");
        Box("Terreno",new Vector3(5,-.7f,10),new Vector3(2000,1,2000),grass);
        Box("Plaza de acceso",new Vector3(9,-.08f,-8),new Vector3(99,.16f,25),paving);
        Box("Sendero perimetral",new Vector3(7,-.08f,23.5f),new Vector3(91,.16f,16),paving);
        Box("Cota elevada bajo voladizo",new Vector3(45,1.88f,7),new Vector3(10,3.96f,20),concrete);
        PanelSlope("Pendiente lateral aproximada",new Vector3(50,0,7),new Vector3(65,-3.96f,7),3.96f,20,paving);
        Box("Estacionamiento",new Vector3(7,-.13f,-29),new Vector3(112,.1f,15),asphalt);
        for(int i=0;i<28;i++)Box("Línea estacionamiento",new Vector3(-43+i*3.6f,-.067f,-27),new Vector3(.09f,.02f,5.6f),white);
        // A field supplies scale and the context visible in the references.
        for(float z=39;z<=85;z+=46)Box("Línea cancha",new Vector3(7,-.17f,z),new Vector3(85,.035f,.11f),white);
        for(float x=-35.5f;x<=49.5f;x+=85)Box("Línea cancha",new Vector3(x,-.17f,62),new Vector3(.11f,.035f,46),white);
        Box("Línea central",new Vector3(7,-.17f,62),new Vector3(.11f,.035f,46),white);
        for(int s=0;s<2;s++){
            float x=s==0?-35.5f:49.5f;
            for(float z=58.4f;z<=65.7f;z+=7.3f)Bar("Poste arco",new Vector3(x,-.2f,z),new Vector3(x,2.24f,z),.11f,white);
            Bar("Travesaño arco",new Vector3(x,2.24f,58.4f),new Vector3(x,2.24f,65.7f),.11f,white);
        }
        for(int i=0;i<13;i++){Tree(-49+i*9,-43,5+i%4,i);Tree(-49+i*9,94,6+i%4,i+20);}
        for(int i=0;i<5;i++){Tree(-47,10+i*14,6+i%2,i+40);Tree(66,10+i*14,7+i%3,i+50);}
        for(int i=0;i<8;i++){
            float x=-28+i*9;
            Box("Carrocería vehículo",new Vector3(x,.65f,-27),new Vector3(1.85f,1.15f,4),i%3==0?orange:white);
            Box("Cabina vehículo",new Vector3(x,1.38f,-27.25f),new Vector3(1.62f,.65f,2.15f),Glass(4));
            for(int s=0;s<2;s++)for(int t=0;t<2;t++){var o=Cylinder("Rueda",new Vector3(x+(s==0?-.9f:.9f),.37f,-27+(t==0?-1.25f:1.25f)),.34f,.18f,dark);o.transform.rotation=Quaternion.Euler(0,0,90);}
        }
    }
    public static void Build(){
        foreach(string p in new[]{"Assets/Scenes","Assets/Materiales","Assets/Geometria","Assets/Prefabs"})Directory.CreateDirectory(p);
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);objects=0;
        root=new GameObject("Recreación arquitectónica").transform;Materials();GlassBuilding();OchreFacade();Roof();Site();
        RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.64f,.75f,.9f);RenderSettings.ambientEquatorColor=new Color(.46f,.51f,.56f);RenderSettings.ambientGroundColor=new Color(.25f,.23f,.19f);RenderSettings.ambientIntensity=1;
        var sky=Mat("Cielo",Color.white);sky.shader=Shader.Find("Skybox/Procedural");sky.SetFloat("_SunSize",.025f);sky.SetFloat("_AtmosphereThickness",.8f);sky.SetColor("_SkyTint",new Color(.55f,.62f,.72f));sky.SetColor("_GroundColor",new Color(.42f,.43f,.4f));sky.SetFloat("_Exposure",1.15f);RenderSettings.skybox=sky;
        var sun=new GameObject("Sol").AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=1.3f;sun.color=new Color(1,.94f,.85f);sun.transform.rotation=Quaternion.Euler(43,-35,0);sun.shadows=LightShadows.Soft;sun.shadowBias=.025f;RenderSettings.sun=sun;
        RenderSettings.fog=true;RenderSettings.fogMode=FogMode.ExponentialSquared;RenderSettings.fogDensity=.0018f;RenderSettings.fogColor=new Color(.68f,.76f,.81f);
        var fill=new GameObject("Luz ambiente de apoyo").AddComponent<Light>();fill.type=LightType.Directional;fill.intensity=.45f;fill.color=new Color(.76f,.85f,1);fill.transform.rotation=Quaternion.Euler(35,145,0);
        var probe=new GameObject("Reflejos del entorno").AddComponent<ReflectionProbe>();probe.transform.position=new Vector3(12,12,-18);probe.size=new Vector3(220,85,200);probe.mode=ReflectionProbeMode.Realtime;probe.refreshMode=ReflectionProbeRefreshMode.OnAwake;probe.resolution=512;probe.clearFlags=ReflectionProbeClearFlags.Skybox;probe.intensity=1.25f;probe.boxProjection=false;
        var camera=new GameObject("Cámara de recorrido").AddComponent<Camera>();camera.tag="MainCamera";camera.fieldOfView=48;camera.nearClipPlane=.1f;camera.farClipPlane=450;camera.allowHDR=true;camera.backgroundColor=new Color(.58f,.72f,.85f);camera.clearFlags=CameraClearFlags.Skybox;
        var app=new GameObject("Controles de visita").AddComponent<ArchitectureViewer>();app.view=camera;app.architecture=root;app.landscape=site;app.SetView(0);
        QualitySettings.SetQualityLevel(5,true);QualitySettings.shadows=ShadowQuality.All;QualitySettings.shadowResolution=ShadowResolution.VeryHigh;QualitySettings.shadowDistance=180;QualitySettings.antiAliasing=4;QualitySettings.pixelLightCount=4;
        PlayerSettings.companyName="Estudio académico";PlayerSettings.productName="Recreación arquitectónica Ingeniería";PlayerSettings.colorSpace=ColorSpace.Linear;PlayerSettings.defaultScreenWidth=1600;PlayerSettings.defaultScreenHeight=1000;PlayerSettings.fullScreenMode=FullScreenMode.Windowed;PlayerSettings.runInBackground=true;
        var graphics=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset")[0]);var shaders=graphics.FindProperty("m_AlwaysIncludedShaders");
        foreach(string name in new[]{"Standard","Skybox/Procedural"}){int i=shaders.arraySize;shaders.InsertArrayElementAtIndex(i);shaders.GetArrayElementAtIndex(i).objectReferenceValue=Shader.Find(name);}graphics.ApplyModifiedProperties();
        PrefabUtility.SaveAsPrefabAsset(root.gameObject,"Assets/Prefabs/Edificio.prefab");
        EditorSceneManager.SaveScene(scene,"Assets/Scenes/Recreacion.unity");EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene("Assets/Scenes/Recreacion.unity",true)};AssetDatabase.SaveAssets();
        string target=Path.GetFullPath(Path.Combine(Application.dataPath,"../../Aplicacion/RecreacionArquitectonica.exe"));Directory.CreateDirectory(Path.GetDirectoryName(target));
        var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{"Assets/Scenes/Recreacion.unity"},locationPathName=target,target=BuildTarget.StandaloneWindows64,options=BuildOptions.None});
        File.WriteAllText(Path.Combine(Path.GetDirectoryName(target),"compilacion.txt"),report.summary.result+"\nErrors: "+report.summary.totalErrors+"\nObjetos: "+objects);
        if(report.summary.result!=BuildResult.Succeeded)throw new Exception("Build failed");Debug.Log("ARCHITECTURE_BUILD_OK");
    }
}
