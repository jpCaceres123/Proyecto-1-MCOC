package cl.mcoc.edificio.ar;

import com.google.ar.core.*;
import org.json.*;
import java.util.*;

/** Metric common registration. Surveyed marker poses are NEVER inferred from photos. */
public final class SectorRegistration {
    public final String modelHash;
    public final float[] origin;
    public final Set<Integer> elements=new LinkedHashSet<>();
    public final Map<Integer,Pose> markers=new HashMap<>();
    public final List<JSONObject> checks=new ArrayList<>();
    private Anchor anchor;
    private Pose pending;
    private int stable;
    public int accepted,rejected;
    public String state="CALIBRATING";
    public SectorRegistration(JSONObject j,StructuralData data)throws Exception{
        if(j.getInt("schema")!=1 || !j.getBoolean("surveyed"))throw new IllegalArgumentException("Se requiere levantamiento medido");
        modelHash=j.getString("modelHash");if(!modelHash.equals(data.modelHash))throw new IllegalArgumentException("Modelo de calibración incompatible");
        origin=StructuralData.floats(j.getJSONArray("origin"));if(origin.length!=3)throw new IllegalArgumentException("Origen XYZ");
        JSONArray ids=j.getJSONArray("elements");for(int k=0;k<ids.length();k++){
            int id=ids.getInt(k);StructuralData.Member m=data.members.get(id);
            if(m==null)throw new IllegalArgumentException("ID de barra desconocido "+id);
            for(float[] p:new float[][]{m.start,m.end})if(distance(p,origin)>8)throw new IllegalArgumentException("Sector excede 8 m del anclaje");
            elements.add(id);
        }
        JSONArray rows=j.getJSONArray("markers");if(rows.length()<3)throw new IllegalArgumentException("Se requieren tres marcadores medidos");
        for(int k=0;k<rows.length();k++){
            JSONObject row=rows.getJSONObject(k);int id=row.getInt("elementTag");
            if(!elements.contains(id)||markers.containsKey(id))throw new IllegalArgumentException("Marcador duplicado o fuera del sector");
            if(Math.abs(row.getDouble("width_m")-.2)>.0001)throw new IllegalArgumentException("Ancho de imagen distinto de 0.20 m");
            float[] p=StructuralData.floats(row.getJSONArray("position")),q=StructuralData.floats(row.getJSONArray("quaternion"));
            if(p.length!=3||q.length!=4)throw new IllegalArgumentException("Pose inválida");
            float norm=0;for(float v:q)norm+=v*v;if(Math.abs(norm-1)>.001)throw new IllegalArgumentException("Quaternion no unitario");
            for(int a=0;a<3;a++)p[a]-=origin[a];
            for(Pose previous:markers.values())if(distance(previous.getTranslation(),p)<.20f)throw new IllegalArgumentException("Marcadores solapados o coincidentes");
            markers.put(id,new Pose(p,q));
        }
    }
    public static float distance(float[] a,float[] b){float s=0;for(int k=0;k<3;k++)s+=(a[k]-b[k])*(a[k]-b[k]);return (float)Math.sqrt(s);}
    static float angle(Pose a,Pose b){float[] x=a.getRotationQuaternion(),y=b.getRotationQuaternion();float dot=0;for(int k=0;k<4;k++)dot+=x[k]*y[k];return 2*(float)Math.acos(Math.min(1,Math.abs(dot)));}
    public Pose pose(){return anchor!=null&&anchor.getTrackingState()==TrackingState.TRACKING?anchor.getPose():null;}
    public void reset(){if(anchor!=null)anchor.detach();anchor=null;pending=null;stable=0;state="CALIBRATING";}
    public void update(Session session,Collection<AugmentedImage> images){
        ArrayList<Pose> candidates=new ArrayList<>();accepted=0;rejected=0;
        for(AugmentedImage image:images){
            if(image.getTrackingState()!=TrackingState.TRACKING||image.getTrackingMethod()!=AugmentedImage.TrackingMethod.FULL_TRACKING)continue;
            int id=Integer.parseInt(image.getName().replace("element_",""));Pose local=markers.get(id);if(local==null)continue;
            candidates.add(image.getCenterPose().compose(local.inverse()));
        }
        if(candidates.isEmpty()){state=pose()!=null?"ANCHORED_LAST_POSE":"TRACKING_LOST";return;}
        // Select the consensus medoid, never depend on enumeration order of trackables.
        Pose reference=null;int best=0;
        for(Pose p:candidates){int count=0;for(Pose q:candidates)if(distance(p.getTranslation(),q.getTranslation())<=.1f&&angle(p,q)<=.08727f)count++;
            if(count>best){best=count;reference=p;}}
        if(best<2 && anchor==null){state="NEED_TWO_CONSISTENT_MARKERS";return;}
        float[] t=new float[3],q=new float[4];float[] rq=reference.getRotationQuaternion();
        for(Pose p:candidates){if(distance(p.getTranslation(),reference.getTranslation())>.1f||angle(p,reference)>.08727f){rejected++;continue;}
            accepted++;float[] pt=p.getTranslation(),pq=p.getRotationQuaternion();float dot=0;for(int k=0;k<4;k++)dot+=pq[k]*rq[k];
            for(int k=0;k<3;k++)t[k]+=pt[k];for(int k=0;k<4;k++)q[k]+=pq[k]*(dot<0?-1:1);}
        for(int k=0;k<3;k++)t[k]/=accepted;float norm=0;for(float v:q)norm+=v*v;for(int k=0;k<4;k++)q[k]/=(float)Math.sqrt(norm);
        Pose fused=new Pose(t,q);
        if(anchor!=null){state=pose()==null?"TRACKING_LOST":distance(anchor.getPose().getTranslation(),t)>.1f||angle(anchor.getPose(),fused)>.08727f?"DISAGREEMENT_REANCHOR_REQUIRED":"ANCHORED";return;}
        if(pending!=null && distance(pending.getTranslation(),t)<.03f && angle(pending,fused)<.035f)stable++;else stable=0;
        pending=fused;state="CALIBRATING_"+stable;
        if(stable>=15){anchor=session.createAnchor(fused);state="ANCHORED";}
    }
    public float[] local(float[] p){return new float[]{p[0]-origin[0],p[1]-origin[1],p[2]-origin[2]};}
    public void measure(float[] modelPoint,float[] observed,String label,long elapsed)throws Exception{
        Pose p=pose();if(p==null)throw new IllegalStateException("Sin anclaje válido");
        for(Pose marker:markers.values())if(distance(marker.getTranslation(),local(modelPoint))<.25f)throw new IllegalArgumentException("Usa un control independiente, no el centro del marcador");
        float[] expected=p.transformPoint(local(modelPoint));
        checks.add(new JSONObject().put("label",label).put("elapsed_ms",elapsed).put("expected",new JSONArray(expected))
            .put("observed",new JSONArray(observed)).put("error_m",distance(expected,observed)));
    }
    public JSONObject report()throws Exception{
        double sum=0,max=0;for(JSONObject c:checks){double e=c.getDouble("error_m");sum+=e*e;max=Math.max(max,e);}
        return new JSONObject().put("modelHash",modelHash).put("units","m").put("state",state).put("checks",new JSONArray(checks))
            .put("rms_m",checks.isEmpty()?JSONObject.NULL:Math.sqrt(sum/checks.size())).put("max_m",checks.isEmpty()?JSONObject.NULL:max)
            .put("physicalAcceptance",checks.isEmpty()?"PENDING":"MEASURED_NOT_AUTOMATICALLY_CERTIFIED").put("persistence","marker_relocalization_not_persistent_anchor");
    }
}
