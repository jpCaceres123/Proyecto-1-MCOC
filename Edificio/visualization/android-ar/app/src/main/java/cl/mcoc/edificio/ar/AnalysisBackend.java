package cl.mcoc.edificio.ar;

import org.json.*;
import java.io.*;
import java.net.*;
import java.nio.charset.StandardCharsets;
import java.security.MessageDigest;
import java.util.*;

/** LAN only; downloads and verifies a whole snapshot before replacing visible data. */
public final class AnalysisBackend {
    public String server="http://127.0.0.1:8765",token="";
    public volatile boolean busy,cancel;
    private volatile String job;
    private int generation;
    public interface Listener {void status(String message);void accepted(StructuralData data,JSONObject capacity);}
    public void submit(String baseHash,int tag,boolean capacity,File storage,Listener listener){
        if(busy){listener.status("Cálculo en curso");return;}busy=true;cancel=false;int current=++generation;final String kind=tagType;
        new Thread(()->{try{
            URI uri=URI.create(server);if(!"http".equals(uri.getScheme())||!uri.getHost().matches("[0-9.]+")||token.length()<24)throw new IOException("IP privada http y token >=24 caracteres");
            InetAddress address=InetAddress.getByName(uri.getHost());if(!(address.isSiteLocalAddress()||address.isLoopbackAddress()))throw new IOException("Solo Wi-Fi local");
            JSONObject health=json("GET","/health",null);if(health.getInt("contractVersion")!=1||!baseHash.equals(health.getString("modelHash")))throw new IOException("Modelo base del servidor distinto");
            JSONObject request=new JSONObject().put("contractVersion",1).put("requestId",UUID.randomUUID().toString()).put("modelHash",baseHash).put("operation",capacity?"capacity":"analysis");
            if(capacity)request.put("elementTag",tag).put("barsPerFace",5).put("diameter_m",.032).put("changes",new JSONArray());
            else request.put("changes",new JSONArray().put(new JSONObject().put("kind",kind).put("id",tag).put("changeLoad",true).put("q",2)));
            JSONObject state=json("POST","/jobs",request);job=state.getString("id");long started=System.currentTimeMillis();
            while(Arrays.asList("pending","running").contains(state.getString("status"))){
                if(cancel||System.currentTimeMillis()-started>630000){json("DELETE","/jobs/"+job,null);throw new IOException("Solicitud cancelada");}
                listener.status("OpenSees · "+state.getString("status"));Thread.sleep(500);state=json("GET","/jobs/"+job,null);
            }
            if(!"completed".equals(state.getString("status")))throw new IOException(state.optString("error",state.getString("status")));
            JSONObject manifest=json("GET","/jobs/"+job+"/delivery",null);if(manifest.getInt("contractVersion")!=1)throw new IOException("Contrato incompatible");
            File dir=new File(storage,job);if(!dir.mkdirs())throw new IOException("Carpeta de entrega ya existe");JSONArray files=manifest.getJSONArray("files");Map<String,byte[]> downloaded=new HashMap<>();
            for(int k=0;k<files.length();k++){JSONObject file=files.getJSONObject(k);String name=file.getString("name");if(!Arrays.asList("structural_data.json","overlay_geometry.json","capacity.json").contains(name))continue;
                byte[] bytes=http("GET","/jobs/"+job+"/files/"+name,null);StringBuilder hash=new StringBuilder();for(byte b:MessageDigest.getInstance("SHA-256").digest(bytes))hash.append(String.format("%02x",b&255));
                if(!hash.toString().equals(file.getString("sha256")))throw new IOException("Hash de descarga incorrecto");downloaded.put(name,bytes);
                try(FileOutputStream out=new FileOutputStream(new File(dir,name))){out.write(bytes);}
            }
            if(cancel||current!=generation)throw new IOException("Respuesta antigua descartada");
            if(capacity){JSONObject c=new JSONObject(new String(require(downloaded,"capacity.json"),StandardCharsets.UTF_8));if(c.getInt("elementTag")!=tag||!c.getString("modelHash").equals(baseHash))throw new IOException("Capacidad incompatible");listener.accepted(null,c);}
            else{StructuralData data=new StructuralData(new JSONObject(new String(require(downloaded,"structural_data.json"),StandardCharsets.UTF_8)));data.setOverlay(new JSONObject(new String(require(downloaded,"overlay_geometry.json"),StandardCharsets.UTF_8)));if(!data.modelHash.equals(manifest.getString("modelHash")))throw new IOException("Identidad de resultados incompatible");listener.accepted(data,null);}
            listener.status("Revisión recibida y validada");
        }catch(Exception e){listener.status("Backend: "+e.getMessage()+" · resultados anteriores conservados");}finally{busy=false;}} ,"analysis-backend").start();
    }
    public String tagType="Viga";
    static byte[] require(Map<String,byte[]> d,String n)throws IOException{if(!d.containsKey(n))throw new IOException("Entrega incompleta "+n);return d.get(n);}
    JSONObject json(String method,String path,JSONObject payload)throws Exception{return new JSONObject(new String(http(method,path,payload),StandardCharsets.UTF_8));}
    byte[] http(String method,String path,JSONObject payload)throws Exception{
        HttpURLConnection connection=(HttpURLConnection)new URL(server+path).openConnection();connection.setInstanceFollowRedirects(false);connection.setRequestMethod(method);connection.setConnectTimeout(15000);connection.setReadTimeout(90000);connection.setRequestProperty("Authorization","Bearer "+token);
        try{if(payload!=null){connection.setDoOutput(true);connection.setRequestProperty("Content-Type","application/json");try(OutputStream out=connection.getOutputStream()){out.write(payload.toString().getBytes(StandardCharsets.UTF_8));}}
            if(connection.getResponseCode()<200||connection.getResponseCode()>=300)throw new IOException("HTTP "+connection.getResponseCode());
            try(InputStream in=connection.getInputStream()){ByteArrayOutputStream out=new ByteArrayOutputStream();byte[] b=new byte[8192];int n;while((n=in.read(b))!=-1){out.write(b,0,n);if(out.size()>150000000)throw new IOException("Archivo demasiado grande");}return out.toByteArray();}
        }finally{connection.disconnect();}
    }
}
