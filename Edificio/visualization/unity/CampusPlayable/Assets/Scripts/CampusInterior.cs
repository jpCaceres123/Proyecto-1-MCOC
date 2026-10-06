using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Architectural fit-out follows original columns and existing core openings.
// None of these partitions, stairs or circulation finishes change analytical results.
public partial class CampusWorld
{
    const float CorridorFront=7.0f, CorridorBack=9.65f;

    List<float> InteriorColumnAxes(int level)
    {
        float elevation=Mathf.Max(1,level)*Storey;
        var candidates=new List<float>();
        foreach(var r in records){
            if(r[0]!="E" || !r[2].Contains("COLUMN"))continue;
            Vector3 a=nodes[int.Parse(r[3])],b=nodes[int.Parse(r[4])];
            if(Mathf.Min(a.y,b.y)>elevation+.02f || Mathf.Max(a.y,b.y)<elevation-.02f)continue;
            if(Mathf.Abs(a.z-7.25f)<.4f)candidates.Add(a.x);
        }
        var axes=new List<float>();
        foreach(float x in candidates.Distinct().OrderBy(x=>x))
            if(axes.Count==0 || x-axes[axes.Count-1]>.7f)axes.Add(x);
        return axes;
    }

    bool RoomFits(int level,Rect room)
    {
        if(room.width<3.0f)return false;
        foreach(var h in elevatorHoles)
            if(room.Overlaps(Rect.MinMaxRect(h.xMin-.4f,h.yMin-.5f,h.xMax+.4f,h.yMax+.5f)))return false;
        // Keep the terrace-connected cafeteria and its approach on level 1.
        if(level==1 && room.xMax>21 && room.xMin<40 && room.yMin>9)return false;
        for(int i=0;i<=4;i++)for(int j=0;j<=2;j++)
            if(!Inside(level,Mathf.Lerp(room.xMin+.2f,room.xMax-.2f,i/4f),Mathf.Lerp(room.yMin+.2f,room.yMax-.2f,j/2f)))return false;
        return true;
    }

    void Interior(int level)
    {
        float y=level*Storey+Finish;
        Station(new Vector3(-23,y+1.4f,3),FloorNames[level]+" · SALAS Y LABORATORIOS",level);
        var axes=InteriorColumnAxes(level);
        int classrooms=0,laboratories=0;
        for(int i=0;i<axes.Count-1;i++){
            float lo=axes[i],hi=axes[i+1];
            var laboratory=Rect.MinMaxRect(lo,.55f,hi,CorridorFront);
            var classroom=Rect.MinMaxRect(lo,CorridorBack,hi,15.75f);
            if(RoomFits(level,classroom)){FitRoom(level,classroom,false,++classrooms);}
            if(RoomFits(level,laboratory)){FitRoom(level,laboratory,true,++laboratories);}
            float x=(lo+hi)/2;
            if(!Inside(level,x,8.4f))continue;
            Box("Pasillo · banda de circulación",new Vector3(x,y+.006f,8.45f),new Vector3(Mathf.Max(.1f,hi-lo-.9f),.012f,.12f),blue,false);
            Box("Pasillo · luz de conexión",new Vector3(x,y+3.18f,8.45f),new Vector3(Mathf.Min(3,hi-lo-.5f),.04f,.12f),light,false);
            var lamp=new GameObject("Luz del pasillo").AddComponent<Light>();lamp.transform.SetParent(architecture);
            lamp.transform.position=new Vector3(x,y+2.9f,8.4f);lamp.type=LightType.Point;lamp.range=10;lamp.intensity=.65f;lamp.shadows=LightShadows.None;
        }
        // Only the architectural bridge fills the expansion joint along the connecting passage.
        Box("Pasillo de conexión LT1–LT2 · losa de paso",new Vector3(-.4f,y-.06f,8.6f),new Vector3(2,.12f,1.85f),floor);
        if(level==0){
            Box("Entrada LT2 · acceso alineado al pasillo",new Vector3(-32.8f,y-.08f,8.5f),new Vector3(5.6f,.16f,1.8f),floor);
            Box("Entrada LT2 · paseo de aproximación",new Vector3(-35,y-.08f,-3.75f),new Vector3(1.8f,.16f,26.3f),floor);
            CorridorSign(new Vector3(-31.5f,y+2.4f,8.5f),"ENTRADA / PASILLO",90);
        }
        CorridorSign(new Vector3(-3.6f,y+2.65f,8.6f),"PASILLO · LT1 / LT2",90);
        CorridorSign(new Vector3(9,y+2.65f,8.6f),"SALAS / LABORATORIOS",-90);
        Debug.Log("CAMPUS_INTERIOR floor="+level+" classrooms="+classrooms+" laboratories="+laboratories+" column_axes="+string.Join(",",axes));
    }

    void CorridorSign(Vector3 p,string title,float yaw)
    {
        Label(p,title,.105f);
        architecture.GetChild(architecture.childCount-1).rotation=Quaternion.Euler(0,yaw,0);
    }

    void FitRoom(int level,Rect room,bool lab,int number)
    {
        // This bay is an open space, not a laboratory. Preserve other room numbers.
        if(lab && level==2 && number==7)return;
        bool frontRoom=room.yMax<=CorridorFront+.01f;
        float y=level*Storey+Finish,z=frontRoom?room.yMax:room.yMin;
        string title=(lab?"LABORATORIO ":"SALA ")+level+"-"+number;
        float doorCenter=room.xMin+Mathf.Min(1.65f,room.width*.5f),doorHalf=.8f;
        // Side partitions meet the original column axes; doorways open onto the passage.
        foreach(float x in new[]{room.xMin,room.xMax})
            Box(title+" · tabique entre columnas",new Vector3(x,y+1.5f,room.center.y),new Vector3(.12f,3,room.height),white);
        float left=doorCenter-doorHalf-room.xMin,right=room.xMax-doorCenter-doorHalf;
        if(left>.01f)Box(title+" · frente izquierdo",new Vector3(room.xMin+left/2,y+1.5f,z),new Vector3(left,3,.12f),white);
        if(right>.01f)Box(title+" · frente derecho",new Vector3(room.xMax-right/2,y+1.5f,z),new Vector3(right,3,.12f),white);
        Box(title+" · dintel",new Vector3(doorCenter,y+2.75f,z),new Vector3(1.6f,.5f,.12f),white);
        Door(new Vector3(doorCenter-.8f,y,z),1.6f);
        CorridorSign(new Vector3(doorCenter+(frontRoom?.55f:-.55f),y+2.45f,z+(frontRoom?.09f:-.09f)),title,frontRoom?180:0);
        Box(title+" · cielo acústico",new Vector3(room.center.x,y+3.25f,room.center.y),new Vector3(room.width-.35f,.08f,room.height-.25f),white,false);
        Box(title+" · luminaria",new Vector3(room.center.x,y+3.18f,room.center.y),new Vector3(Mathf.Min(4,room.width-1),.04f,.12f),light,false);
        int cols=lab?Mathf.Clamp(Mathf.FloorToInt((room.width-1)/2.4f),1,3):Mathf.Clamp(Mathf.FloorToInt((room.height-1)/1.8f),1,3);
        int rows=lab?2:Mathf.Clamp(Mathf.FloorToInt((room.width-1.5f)/1.8f),1,4);
        for(int row=0;row<rows;row++)for(int col=0;col<cols;col++){
            Vector3 p=lab?new Vector3(room.xMin+.7f+(room.width-1.4f)*(col+.5f)/cols,y,room.yMin+1.55f+row*2.15f):
                new Vector3(room.xMin+1.45f+Mathf.Max(.2f,room.width-2.5f)*(row+.5f)/rows,y,room.yMin+.9f+(room.height-1.8f)*(col+.5f)/cols);
            RoomTable(p,lab?1.8f:1.35f,lab?.9f:.65f,lab?0:270);
            if(lab){
                Box(title+" · equipo de laboratorio",p+new Vector3(0,1.03f,.12f),new Vector3(.55f,.38f,.36f),metal,false);
                Box(title+" · pantalla de equipo",p+new Vector3(0,1.06f,-.07f),new Vector3(.4f,.24f,.02f),blue,false);
            }
        }
        if(!lab)Box(title+" · pizarra en pared transversal",new Vector3(room.xMin+.10f,y+1.75f,room.center.y),new Vector3(.07f,1.15f,2.6f),white,false);
    }

    void RoomTable(Vector3 p,float width,float depth,float yaw)
    {
        var group=new GameObject("Mobiliario orientado hacia la pizarra").transform;
        group.SetParent(architecture);group.position=p;
        int first=architecture.childCount;
        Table(p,width,depth);
        var pieces=Enumerable.Range(first,architecture.childCount-first).Select(i=>architecture.GetChild(i)).ToArray();
        foreach(var piece in pieces)piece.SetParent(group,true);
        group.rotation=Quaternion.Euler(0,yaw,0);
    }

    IEnumerable<Edge> FacadeEdges(int level,Edge edge)
    {
        Vector2 mid=(edge.a+edge.b)*.5f,along=(edge.b-edge.a).normalized;
        Vector2 normal=new Vector2(-along.y,along.x)*.6f;
        float height=Mathf.Max(1,level)*Storey;
        var solidPlan=slabs.Values.Where(s=>Mathf.Abs(s.height-height)<.02f).Select(s=>s.rect).ToArray();
        // Boundaries of slab holes and the expansion joint are not external facades.
        // This leaves stair openings and the whole inter-building passage clear.
        if(solidPlan.Any(r=>r.Contains(mid+normal)) && solidPlan.Any(r=>r.Contains(mid-normal)))yield break;
        bool sideDoor=level==2 && edge.a.x>45 && edge.a.x<46;
        bool groundEntry=level==0 && edge.a.x<-27;
        if((sideDoor || groundEntry) && Mathf.Abs(edge.a.x-edge.b.x)<.01f){
            float lo=Mathf.Min(edge.a.y,edge.b.y),hi=Mathf.Max(edge.a.y,edge.b.y);
            // Exact opening at the corridor centre, clear of the column at Y=7.25.
            if(lo<9.4f && hi>7.6f){
                if(lo<7.6f)yield return new Edge(new Vector2(edge.a.x,lo),new Vector2(edge.a.x,7.6f));
                if(hi>9.4f)yield return new Edge(new Vector2(edge.a.x,9.4f),new Vector2(edge.a.x,hi));
                yield break;
            }
        }
        yield return edge;
    }

    void InteriorStairCore()
    {
        Rect h=elevatorHoles[0];float width=(h.width-.4f)/2;
        float left=h.xMin+.15f+width/2,right=h.xMax-.15f-width/2;
        float front=h.yMax-.18f,back=h.yMin+1.08f;
        for(int level=0;level<=5;level++){
            float y=level*Storey+Finish;
            Box("Escalera interior · descanso de piso "+level,new Vector3(h.center.x,y-.1f,h.yMax+.12f),new Vector3(h.width-.15f,.2f,.95f),concrete);
            CorridorSign(new Vector3(h.center.x-.65f,y+2.65f,h.yMax+.65f),"ESCALERA / "+level,180);
            if(level==5)continue;
            float middle=y+Storey*.5f;
            StairFlight(new Vector3(left,y,front),new Vector3(left,middle,back),"Escalera interior "+level+" · primer tramo",metal,width);
            Box("Escalera interior · descanso intermedio "+level,new Vector3(h.center.x,middle-.12f,h.yMin+.54f),new Vector3(h.width-.15f,.24f,1.12f),concrete);
            StairFlight(new Vector3(right,middle,back),new Vector3(right,y+Storey,front),"Escalera interior "+level+" · segundo tramo",metal,width);
        }
    }

    bool DrawConnectingPortal(string[] r,Vector3 a,Vector3 b,float lo,float hi)
    {
        if(Mathf.Abs(a.x+.575f)>.03f || Mathf.Abs(b.x-a.x)>.01f || Mathf.Min(a.z,b.z)>=9.5f || Mathf.Max(a.z,b.z)<=7.7f)return false;
        Rect face=Rect.MinMaxRect(Mathf.Min(a.z,b.z),lo,Mathf.Max(a.z,b.z),hi);
        foreach(var part in Cut(face,Rect.MinMaxRect(7.7f,lo+Finish,9.5f,lo+Finish+2.8f))){
            var piece=Box("Muro_"+r[1]+" · paso visual LT1–LT2",new Vector3(a.x,part.center.y,part.center.x),new Vector3(F(r[9]),part.height,part.width),concrete,true,structure);
            var id=piece.AddComponent<StructuralIdentity>();id.key="W:"+r[1];id.description="Muro "+r[1]+" · paso arquitectónico entre módulos; contrato analítico conservado";
        }
        return true;
    }
}

