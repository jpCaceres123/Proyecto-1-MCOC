package cl.mcoc.edificio.ar;

import org.junit.Test;
import static org.junit.Assert.*;
import org.json.*;
import com.google.ar.core.Pose;
import java.nio.file.*;

public class SectorRegistrationTest {
    StructuralData data()throws Exception{return new StructuralData(new JSONObject(new String(Files.readAllBytes(Path.of("src/main/assets/structural_data.json")),java.nio.charset.StandardCharsets.UTF_8)));}
    JSONObject form(StructuralData data)throws Exception{
        JSONArray markers=new JSONArray();for(int id:new int[]{207,211,212}){StructuralData.Member m=data.members.get(id);float[] center=new float[3];for(int k=0;k<3;k++)center[k]=(m.start[k]+m.end[k])/2;markers.put(new JSONObject().put("elementTag",id).put("width_m",.2).put("position",new JSONArray(center)).put("quaternion",new JSONArray(new float[]{0,0,0,1})));}
        return new JSONObject().put("schema",1).put("surveyed",true).put("modelHash",data.modelHash).put("origin",new JSONArray(new float[]{-30,2,3.96f})).put("elements",new JSONArray(new int[]{207,211,212})).put("markers",markers);
    }
    @Test public void acceptsSurveyAndStartsUnanchored()throws Exception{StructuralData d=data();SectorRegistration r=new SectorRegistration(form(d),d);assertNull(r.pose());assertEquals(3,r.elements.size());assertEquals("PENDING",r.report().getString("physicalAcceptance"));}
    @Test public void refusesUnsurveyed()throws Exception{StructuralData d=data();JSONObject f=form(d).put("surveyed",false);assertThrows(IllegalArgumentException.class,()->new SectorRegistration(f,d));}
    @Test public void refusesWrongHash()throws Exception{StructuralData d=data();JSONObject f=form(d).put("modelHash","0".repeat(64));assertThrows(IllegalArgumentException.class,()->new SectorRegistration(f,d));}
    @Test public void quaternionSignIsSameRotation(){assertEquals(0,SectorRegistration.angle(new Pose(new float[3],new float[]{0,0,0,1}),new Pose(new float[3],new float[]{0,0,0,-1})),1e-6);}
    @Test public void resetDoesNotInventMeasures()throws Exception{StructuralData d=data();SectorRegistration r=new SectorRegistration(form(d),d);r.checks.add(new JSONObject().put("error_m",.02));r.reset();assertEquals(JSONObject.NULL,r.report().get("rms_m"));assertThrows(IllegalStateException.class,()->r.measure(new float[3],new float[3],"control",60000));}
}
