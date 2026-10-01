package cl.mcoc.edificio.ar;

import android.content.res.AssetManager;
import org.json.*;
import java.io.*;
import java.nio.charset.StandardCharsets;
import java.util.*;

/** Immutable snapshot: local OpenSees signs and IDs are never recomputed on the phone. */
public final class StructuralData {
    public static final String[] CASES = {"G", "Q", "EX", "EY", "R"};
    public static final String[] COMPONENTS = {"N", "Vy", "Vz", "T", "My", "Mz"};
    public static final String[] FIELDS = {"n", "vy", "vz", "t", "my", "mz"};
    public final SortedMap<Integer, Member> members = new TreeMap<>();
    public final String modelHash, resultsHash, markerHash;

    public StructuralData(AssetManager assets) throws Exception {
        JSONObject root;
        try (InputStream in = assets.open("structural_data.json")) {
            ByteArrayOutputStream bytes = new ByteArrayOutputStream();
            byte[] buffer = new byte[16384]; int count;
            while ((count = in.read(buffer)) != -1) bytes.write(buffer, 0, count);
            root = new JSONObject(new String(bytes.toByteArray(), StandardCharsets.UTF_8));
        }
        if (root.getInt("schema") != 2) throw new IOException("Versión de datos incompatible");
        modelHash = root.getString("model_sha256"); resultsHash = root.getString("results_sha256");markerHash=root.getString("marker_set_sha256");
        JSONArray rows = root.getJSONArray("members");
        for (int i=0; i<rows.length(); i++) {
            Member m = new Member(rows.getJSONObject(i));
            if (members.put(m.id, m) != null) throw new IOException("ID duplicado: " + m.id);
        }
        if (members.isEmpty()) throw new IOException("No hay vigas ni columnas en la instantánea");
    }
    static float[] floats(JSONArray a) throws JSONException {
        float[] result = new float[a.length()];
        for (int i=0; i<result.length; i++) {
            result[i] = (float)a.getDouble(i);
            if (!Float.isFinite(result[i])) throw new JSONException("Valor no finito");
        }
        return result;
    }
    public static final class Response {
        public final float[] s;
        public final float[] ui, uj, ri, rj;
        public final float[][] values = new float[6][];
        Response(JSONObject json) throws JSONException {
            s = floats(json.getJSONArray("s"));
            ui=floats(json.getJSONArray("ui"));uj=floats(json.getJSONArray("uj"));
            ri=floats(json.getJSONArray("ri"));rj=floats(json.getJSONArray("rj"));
            if (s.length < 2) throw new JSONException("Faltan estaciones");
            for (int i=0; i<6; i++) {
                values[i] = floats(json.getJSONArray(FIELDS[i]));
                if (values[i].length != s.length) throw new JSONException("Estaciones incompatibles");
            }
        }
        public float at(int component, float position) {
            position = Math.max(0, Math.min(1, position));
            for (int i=1; i<s.length; i++) if (position <= s[i]) {
                float t = (position-s[i-1])/(s[i]-s[i-1]);
                return values[component][i-1]*(1-t)+values[component][i]*t;
            }
            return values[component][s.length-1];
        }
        public float maxAbs(int component) {
            float max=0; for(float v:values[component]) max=Math.max(max,Math.abs(v)); return max;
        }
        public float[] displacement(float position, float length, float[] ex) {
            float q=Math.max(0,Math.min(1,position)),h1=1-3*q*q+2*q*q*q,h2=q-2*q*q+q*q*q,h3=3*q*q-2*q*q*q,h4=-q*q+q*q*q;
            float di=dot(ui,ex),dj=dot(uj,ex);float[] result=new float[3];
            float[] cI=cross(ri,ex),cJ=cross(rj,ex);
            for(int k=0;k<3;k++)result[k]=(1-q)*di*ex[k]+q*dj*ex[k]+h1*(ui[k]-di*ex[k])+h3*(uj[k]-dj*ex[k])+length*(h2*cI[k]+h4*cJ[k]);
            return result;
        }
        private static float dot(float[] a,float[] b){return a[0]*b[0]+a[1]*b[1]+a[2]*b[2];}
        private static float[] cross(float[] a,float[] b){return new float[]{a[1]*b[2]-a[2]*b[1],a[2]*b[0]-a[0]*b[2],a[0]*b[1]-a[1]*b[0]};}
    }
    public static final class LoadInfo {
        public final float tributaryArea, slabDead, slabLive, selfWeight;
        public final int[] slabIds; public final Map<String,Float> applied=new HashMap<>();
        LoadInfo(JSONObject json)throws JSONException{
            tributaryArea=(float)json.getDouble("tributary_area_m2");slabDead=(float)json.getDouble("slab_dead_load_kN");
            slabLive=(float)json.getDouble("slab_live_load_kN");selfWeight=(float)json.getDouble("member_self_weight_kN");
            JSONArray ids=json.getJSONArray("associated_slab_ids");slabIds=new int[ids.length()];for(int k=0;k<ids.length();k++)slabIds[k]=ids.getInt(k);
            JSONObject totals=json.getJSONObject("applied_total_kN");for(String c:CASES)applied.put(c,(float)totals.getDouble(c+"_kN"));
        }
    }
    public static final class PMPoint {public final String point;public final float p,m;PMPoint(JSONObject j)throws JSONException{point=j.getString("point");p=(float)j.getDouble("P_kN");m=(float)j.getDouble("M_kNm");}}
    public static final class Member {
        public final int id, i, j;
        public final String type, marker;
        public final float[] start, end, localX, localY, localZ;
        public final float length, width, height, markerWidth, extentY, extentZ;
        public final LoadInfo loadInfo; public final PMPoint[] pm; public final String pmNote;
        public final Map<String, Response> cases = new HashMap<>();
        Member(JSONObject row) throws JSONException {
            id=row.getInt("id"); i=row.getInt("i"); j=row.getInt("j");
            type=row.getString("type"); marker=row.getString("marker");
            start=floats(row.getJSONArray("start")); end=floats(row.getJSONArray("end"));
            localX=floats(row.getJSONArray("local_x")); localY=floats(row.getJSONArray("local_y")); localZ=floats(row.getJSONArray("local_z"));
            length=(float)row.getDouble("length_m"); width=(float)row.getDouble("width_m"); height=(float)row.getDouble("height_m");
            markerWidth=(float)row.getDouble("marker_width_m");
            loadInfo=new LoadInfo(row.getJSONObject("load_info"));pmNote=row.getString("pm_note");
            JSONArray curve=row.getJSONArray("pm_curve");pm=new PMPoint[curve.length()];for(int k=0;k<pm.length;k++)pm[k]=new PMPoint(curve.getJSONObject(k));
            JSONObject section=row.getJSONObject("section");
            if(section.has("outer_width_m")){extentY=extentZ=(float)section.getDouble("outer_width_m");}
            else{
                // Recover rectangular dimensions along LOCAL axes from A/I.
                // Iy integrates z²; Iz integrates y². Do not swap legacy/override conventions.
                double area=section.getDouble("A_m2");
                extentY=(float)Math.sqrt(12*section.getDouble("Iz_m4")/area);
                extentZ=(float)Math.sqrt(12*section.getDouble("Iy_m4")/area);
            }
            for(String c:CASES) cases.put(c, new Response(row.getJSONObject("cases").getJSONObject(c)));
        }
        public boolean isColumn(){return type.contains("COLUMN");}
        public String label(){return (isColumn()?"Columna":"Viga")+" · ID "+id;}
        /** Right-handed rigid mapping: local beam x runs right; column x runs up the printed image. */
        public float[] modelToMarker(float[] point, float scale) {
            float x=0,y=0,z=0;
            for(int k=0;k<3;k++) {
                float d=point[k]-(start[k]+end[k])*0.5f;
                x+=d*localX[k]; y+=d*localY[k]; z+=d*localZ[k];
            }
            return localToMarker(x,y,z,scale);
        }
        public float[] localToMarker(float x,float y,float z,float scale) {
            // Beam: (x,y,z) -> (x,z,-y). Column: (x,y,z) -> (y,-z,-x).
            // Both rotations have determinant +1; ARCore is right handed, like OpenSees.
            return isColumn()?new float[]{y*scale,-z*scale,-x*scale}:new float[]{x*scale,z*scale,-y*scale};
        }
    }
}
