package cl.mcoc.edificio.ar;

import android.opengl.*;
import com.google.ar.core.*;
import java.nio.*;
import java.util.*;
import javax.microedition.khronos.egl.EGLConfig;
import javax.microedition.khronos.opengles.GL10;

/** Camera background plus member/diagram anchored in metric ARCore world space. */
public final class ARRenderer implements GLSurfaceView.Renderer {
    private final MainActivity activity;
    private int cameraTexture,backgroundProgram,lineProgram,width=1,height=1,activeId=-1;
    private Anchor anchor;
    private AugmentedImage activeImage;
    private volatile boolean resetPending;
    private float[] measurePoint;
    private String measureLabel;
    private long measureElapsed;
    public void requestMeasurement(float[] p,String label,long elapsed){synchronized(activity.sessionLock){measurePoint=p;measureLabel=label;measureElapsed=elapsed;}}
    private final FloatBuffer quad=buffer(new float[]{-1,-1,1,-1,-1,1,1,1});
    private final FloatBuffer uv=buffer(new float[8]);
    private final float[] view=new float[16],projection=new float[16],world=new float[16],pv=new float[16],mvp=new float[16];
    ARRenderer(MainActivity activity){this.activity=activity;}
    public void reset(){resetPending=true;}
    @Override public void onSurfaceCreated(GL10 gl,EGLConfig config){
        int[] t=new int[1];GLES20.glGenTextures(1,t,0);cameraTexture=t[0];GLES20.glBindTexture(GLES11Ext.GL_TEXTURE_EXTERNAL_OES,cameraTexture);
        GLES20.glTexParameteri(GLES11Ext.GL_TEXTURE_EXTERNAL_OES,GLES20.GL_TEXTURE_MIN_FILTER,GLES20.GL_LINEAR);
        GLES20.glTexParameteri(GLES11Ext.GL_TEXTURE_EXTERNAL_OES,GLES20.GL_TEXTURE_MAG_FILTER,GLES20.GL_LINEAR);
        GLES20.glTexParameteri(GLES11Ext.GL_TEXTURE_EXTERNAL_OES,GLES20.GL_TEXTURE_WRAP_S,GLES20.GL_CLAMP_TO_EDGE);
        GLES20.glTexParameteri(GLES11Ext.GL_TEXTURE_EXTERNAL_OES,GLES20.GL_TEXTURE_WRAP_T,GLES20.GL_CLAMP_TO_EDGE);
        backgroundProgram=program("attribute vec2 aPosition;attribute vec2 aTex;varying vec2 vTex;void main(){gl_Position=vec4(aPosition,0.,1.);vTex=aTex;}","#extension GL_OES_EGL_image_external : require\nprecision mediump float;uniform samplerExternalOES uCamera;varying vec2 vTex;void main(){gl_FragColor=texture2D(uCamera,vTex);}");
        lineProgram=program("attribute vec3 aPosition;uniform mat4 uMvp;void main(){gl_Position=uMvp*vec4(aPosition,1.);}","precision mediump float;uniform vec4 uColor;void main(){gl_FragColor=uColor;}");
        GLES20.glClearColor(0.06f,0.10f,0.16f,1);
    }
    @Override public void onSurfaceChanged(GL10 gl,int w,int h){width=w;height=h;GLES20.glViewport(0,0,w,h);}
    @Override public void onDrawFrame(GL10 gl){
        GLES20.glClear(GLES20.GL_COLOR_BUFFER_BIT|GLES20.GL_DEPTH_BUFFER_BIT);
        synchronized(activity.sessionLock){
            Session session=activity.session;if(session==null)return;
            try{
                if(resetPending){if(anchor!=null)try{anchor.detach();}catch(Exception ignored){}anchor=null;activeImage=null;activeId=-1;if(activity.sector!=null)activity.sector.reset();resetPending=false;}
                session.setCameraTextureName(cameraTexture);
                session.setDisplayGeometry(activity.getWindowManager().getDefaultDisplay().getRotation(),width,height);
                Frame frame=session.update();if(frame.getTimestamp()==0)return;
                drawCamera(frame);
                Camera camera=frame.getCamera();
                if(camera.getTrackingState()!=TrackingState.TRACKING){activity.labels(Collections.emptyList(),Collections.emptyList());activity.cameraStatus("Seguimiento pausado · "+camera.getTrackingFailureReason()+" · mueve el teléfono lentamente");return;}
                if(!activity.scanning)return;
                if(activity.sector!=null){drawSector(session,frame,camera);return;}
                AugmentedImage candidate=null;
                for(AugmentedImage image:session.getAllTrackables(AugmentedImage.class)){
                    if(image.getTrackingState()==TrackingState.TRACKING&&image.getTrackingMethod()==AugmentedImage.TrackingMethod.FULL_TRACKING){candidate=image;if(image==activeImage)break;}
                }
                if(candidate!=null&&candidate!=activeImage){
                    if(anchor!=null)anchor.detach();
                    activeId=Integer.parseInt(candidate.getName().replace("element_",""));activeImage=candidate;anchor=candidate.createAnchor(candidate.getCenterPose());
                }
                if(anchor==null){activity.cameraStatus("Busca un marcador activo · buena luz y toda la imagen visible");return;}
                if(anchor.getTrackingState()!=TrackingState.TRACKING){activity.cameraStatus("Anclaje pausado · vuelve a mostrar el marcador");return;}
                StructuralData.Member member=activity.data.members.get(activeId);if(member==null)return;
                activity.detected(activeId,candidate==activeImage?"Imagen detectada · anclaje activo":"Anclaje activo · última pose conocida");
                camera.getViewMatrix(view,0);camera.getProjectionMatrix(projection,0,0.05f,100f);anchor.getPose().toMatrix(world,0);
                Matrix.multiplyMM(pv,0,projection,0,view,0);Matrix.multiplyMM(mvp,0,pv,0,world,0);
                GLES20.glEnable(GLES20.GL_DEPTH_TEST);GLES20.glDepthMask(true);drawMember(member);GLES20.glDisable(GLES20.GL_DEPTH_TEST);
            }catch(com.google.ar.core.exceptions.SessionPausedException ignored){
            }catch(Exception e){activity.cameraStatus("AR: "+e.getClass().getSimpleName()+" · "+e.getMessage());}
        }
    }
    private void drawCamera(Frame frame){
        quad.position(0);uv.position(0);frame.transformCoordinates2d(Coordinates2d.OPENGL_NORMALIZED_DEVICE_COORDINATES,quad,Coordinates2d.TEXTURE_NORMALIZED,uv);uv.position(0);quad.position(0);
        GLES20.glDisable(GLES20.GL_DEPTH_TEST);GLES20.glDepthMask(false);GLES20.glUseProgram(backgroundProgram);
        int pos=GLES20.glGetAttribLocation(backgroundProgram,"aPosition"),tex=GLES20.glGetAttribLocation(backgroundProgram,"aTex");
        GLES20.glEnableVertexAttribArray(pos);GLES20.glVertexAttribPointer(pos,2,GLES20.GL_FLOAT,false,0,quad);
        GLES20.glEnableVertexAttribArray(tex);GLES20.glVertexAttribPointer(tex,2,GLES20.GL_FLOAT,false,0,uv);
        GLES20.glActiveTexture(GLES20.GL_TEXTURE0);GLES20.glBindTexture(GLES11Ext.GL_TEXTURE_EXTERNAL_OES,cameraTexture);GLES20.glUniform1i(GLES20.glGetUniformLocation(backgroundProgram,"uCamera"),0);
        GLES20.glDrawArrays(GLES20.GL_TRIANGLE_STRIP,0,4);GLES20.glDisableVertexAttribArray(pos);GLES20.glDisableVertexAttribArray(tex);
    }
    private void drawSector(Session session,Frame frame,Camera camera)throws Exception{
        SectorRegistration sector=activity.sector;sector.update(session,session.getAllTrackables(AugmentedImage.class));
        activity.cameraStatus("Sector · "+sector.state+" · aceptados "+sector.accepted+" · rechazados "+sector.rejected);
        Pose pose=sector.pose();if(pose==null||sector.state.startsWith("DISAGREEMENT")){activity.labels(Collections.emptyList(),Collections.emptyList());return;}
        if(measurePoint!=null){List<HitResult> hits=frame.hitTest(width/2f,height/2f);
            HitResult valid=null;for(HitResult hit:hits)if(hit.getTrackable() instanceof Plane&&((Plane)hit.getTrackable()).isPoseInPolygon(hit.getHitPose()) || hit.getTrackable() instanceof Point){valid=hit;break;}
            if(valid==null)activity.cameraStatus("Medición no disponible: apunta a una superficie detectable");
            else{sector.measure(measurePoint,valid.getHitPose().getTranslation(),measureLabel,measureElapsed);activity.cameraStatus("Punto independiente medido; exporta el reporte");}measurePoint=null;}
        camera.getViewMatrix(view,0);camera.getProjectionMatrix(projection,0,.05f,100);pose.toMatrix(world,0);
        Matrix.multiplyMM(pv,0,projection,0,view,0);Matrix.multiplyMM(mvp,0,pv,0,world,0);
        GLES20.glEnable(GLES20.GL_BLEND);GLES20.glBlendFunc(GLES20.GL_SRC_ALPHA,GLES20.GL_ONE_MINUS_SRC_ALPHA);
        Set<Integer> chosen=new LinkedHashSet<>();synchronized(activity.compared){chosen.addAll(activity.compared);}
        if(chosen.isEmpty() && activity.selected!=null)chosen.add(activity.selected.id);
        List<String> labels=new ArrayList<>();List<float[]> labelPositions=new ArrayList<>();
        for(int id:sector.elements){StructuralData.Member m=activity.data.members.get(id);float[] a=sector.local(m.start),b=sector.local(m.end);
            lines(new float[]{a[0],a[1],a[2],b[0],b[1],b[2]},.27f,.88f,.75f,chosen.contains(id)?1:.35f,chosen.contains(id)?4:2);
            if(!chosen.contains(id))continue;
            StructuralData.Response r=m.cases.get(activity.loadCase);if(r==null)continue;
            float[] clip=new float[4];float[] labelPoint={(a[0]+b[0])/2,(a[1]+b[1])/2,(a[2]+b[2])/2,1};Matrix.multiplyMV(clip,0,mvp,0,labelPoint,0);
            if(clip[3]>0){float x=(clip[0]/clip[3]+1)*width/2,y=(1-clip[1]/clip[3])*height/2;if(x>=0&&x<width&&y>=0&&y<height){labels.add("ID "+id+" · "+activity.loadCase+" · "+StructuralData.COMPONENTS[activity.component]+" "+String.format(Locale.US,"%+.2f",r.at(activity.component,activity.station))+(activity.component<3?" kN":" kN·m")+(activity.showDeformed?"\nHermite nodal ×100":"")+(activity.showAreas?"\nÁrea "+String.format(Locale.US,"%.2f",m.loadInfo.tributaryArea)+" m²":""));labelPositions.add(new float[]{x,y});}}
            ArrayList<Float> vertices=new ArrayList<>();float maximum=Math.max(1,r.maxAbs(activity.component));
            for(int k=0;k<r.s.length;k++){float s=r.s[k];float[] p=new float[3];for(int v=0;v<3;v++)p[v]=a[v]+(b[v]-a[v])*s+.35f*m.localY[v]*r.values[activity.component][k]/maximum;add(vertices,p);}
            polyline(array(vertices),.45f,.70f,1,1,3);
            if(activity.showDeformed){vertices.clear();for(int k=0;k<=40;k++){float s=k/40f;float[] u=r.displacement(s,m.length,m.localX),p=new float[3];for(int v=0;v<3;v++)p[v]=a[v]+(b[v]-a[v])*s+100*u[v];add(vertices,p);}polyline(array(vertices),1,.25f,.70f,1,3);}
            if(activity.showCapacity && m.pm.length>0)drawCapacity(m,r,a,b);
        }
        activity.labels(labels,labelPositions);
        if(activity.showAreas)activity.data.drawAreas(this,sector,chosen);
        if(activity.showDeformed)activity.data.drawWalls(this,sector,activity.loadCase);
        GLES20.glDisable(GLES20.GL_BLEND);
    }
    void drawCapacity(StructuralData.Member m,StructuralData.Response r,float[] a,float[] b)throws Exception{
        org.json.JSONObject snapshot=activity.capacitySnapshot;ArrayList<float[]> current=new ArrayList<>(),prior=new ArrayList<>();
        if(snapshot!=null&&snapshot.getInt("elementTag")==m.id&&snapshot.getString("modelHash").equals(activity.data.modelHash)){
            for(String field:new String[]{"curves","previous"}){org.json.JSONArray curves=snapshot.getJSONArray(field);for(int k=0;k<curves.length();k++){org.json.JSONObject curve=curves.getJSONObject(k);if(!curve.getString("axis").equals("Mz"))continue;org.json.JSONArray points=curve.getJSONArray("points");for(int j=0;j<points.length();j++){org.json.JSONObject p=points.getJSONObject(j);(field.equals("curves")?current:prior).add(new float[]{(float)p.getDouble("P_kN"),(float)p.getDouble("M_kNm")});}}}
        }else for(StructuralData.PMPoint p:m.pm)current.add(new float[]{p.p,p.m});
        ArrayList<float[]> all=new ArrayList<>(current);all.addAll(prior);float maxP=1,maxM=1;for(float[] p:all){maxP=Math.max(maxP,Math.abs(p[0]));maxM=Math.max(maxM,Math.abs(p[1]));}
        for(ArrayList<float[]> curve:Arrays.asList(prior,current)){ArrayList<Float> v=new ArrayList<>();for(float[] p:curve)add(v,capacityPoint(m,a,b,p[0],p[1],maxP,maxM));polyline(array(v),curve==prior?.6f:.1f,curve==prior?.6f:.95f,curve==prior?.6f:.85f,1,3);}
        float[] p=capacityPoint(m,a,b,r.at(0,activity.station),r.at(5,activity.station),maxP,maxM);
        lines(new float[]{p[0]-.04f,p[1],p[2],p[0]+.04f,p[1],p[2],p[0],p[1]-.04f,p[2],p[0],p[1]+.04f,p[2]},1,.8f,.2f,1,4);
    }
    float[] capacityPoint(StructuralData.Member m,float[] a,float[] b,float p,float moment,float maxP,float maxM){float[] point=new float[3];for(int v=0;v<3;v++)point[v]=a[v]+(b[v]-a[v])*.5f+m.localY[v]*(.5f+Math.abs(moment)/maxM*.5f)+m.localX[v]*p/maxP*.5f;return point;}
    void polyline(float[] v,float r,float g,float b,float alpha,float width){ArrayList<Float> list=new ArrayList<>();for(int k=3;k<v.length;k+=3){add(list,new float[]{v[k-3],v[k-2],v[k-1]});add(list,new float[]{v[k],v[k+1],v[k+2]});}lines(array(list),r,g,b,alpha,width);}
    void polygon(float[] v){GLES20.glUseProgram(lineProgram);int pos=GLES20.glGetAttribLocation(lineProgram,"aPosition");GLES20.glUniformMatrix4fv(GLES20.glGetUniformLocation(lineProgram,"uMvp"),1,false,mvp,0);GLES20.glUniform4f(GLES20.glGetUniformLocation(lineProgram,"uColor"),.2f,.85f,.75f,.22f);GLES20.glEnableVertexAttribArray(pos);GLES20.glVertexAttribPointer(pos,3,GLES20.GL_FLOAT,false,0,buffer(v));GLES20.glDrawArrays(GLES20.GL_TRIANGLE_FAN,0,v.length/3);GLES20.glDisableVertexAttribArray(pos);}
    private void drawMember(StructuralData.Member m){
        float scale=activity.scale,half=m.length/2f,normal=activity.normalOffset;
        float[] start=m.modelToMarker(m.start,scale),end=m.modelToMarker(m.end,scale);
        start[1]+=normal;end[1]+=normal;lines(new float[]{start[0],start[1],start[2],end[0],end[1],end[2]},0.27f,0.88f,0.75f,1,4);
        float[][] corners=new float[8][];
        for(int k=0;k<8;k++){corners[k]=m.localToMarker((k&4)==0?-half:half,(k&2)==0?-m.extentY/2:m.extentY/2,(k&1)==0?-m.extentZ/2:m.extentZ/2,scale);corners[k][1]+=normal;}
        ArrayList<Float> wire=new ArrayList<>();for(int k=0;k<8;k++)for(int bit:new int[]{1,2,4})if((k&bit)==0){add(wire,corners[k]);add(wire,corners[k|bit]);}
        lines(array(wire),0.27f,0.88f,0.75f,1,2);
        // Image outline is always 20 cm, independent of model preview scale.
        float a=m.markerWidth/2;
        lines(new float[]{-a,0.004f,-a,a,0.004f,-a,a,0.004f,-a,a,0.004f,a,a,0.004f,a,-a,0.004f,a,-a,0.004f,a,-a,0.004f,-a},1,0.79f,0.4f,1,2);
        StructuralData.Response response=m.cases.get(activity.loadCase);int component=activity.component;float maximum=Math.max(response.maxAbs(component),0.000001f);
        ArrayList<Float> diagram=new ArrayList<>();
        for(int k=1;k<response.s.length;k++){
            float[] p=m.localToMarker((response.s[k-1]-0.5f)*m.length,0,0,scale),q=m.localToMarker((response.s[k]-0.5f)*m.length,0,0,scale);
            // Diagram is offset in the image plane, so it remains legible from the camera.
            int axis=m.isColumn()?0:2;float sign=m.isColumn()?1:-1;
            p[axis]+=sign*0.35f*response.values[component][k-1]/maximum;q[axis]+=sign*0.35f*response.values[component][k]/maximum;p[1]+=normal+0.03f;q[1]+=normal+0.03f;add(diagram,p);add(diagram,q);
        }
        lines(array(diagram),0.45f,0.70f,1,1,3);
        float s=activity.station;float[] mark=m.localToMarker((s-0.5f)*m.length,0,0,scale);mark[1]+=normal+0.02f;
        lines(new float[]{mark[0]-0.04f,mark[1],mark[2],mark[0]+0.04f,mark[1],mark[2],mark[0],mark[1],mark[2]-0.04f,mark[0],mark[1],mark[2]+0.04f},1,0.79f,0.4f,1,4);
    }
    private void lines(float[] vertices,float r,float g,float b,float alpha,float width){
        GLES20.glUseProgram(lineProgram);int pos=GLES20.glGetAttribLocation(lineProgram,"aPosition");
        GLES20.glUniformMatrix4fv(GLES20.glGetUniformLocation(lineProgram,"uMvp"),1,false,mvp,0);GLES20.glUniform4f(GLES20.glGetUniformLocation(lineProgram,"uColor"),r,g,b,alpha);
        GLES20.glEnableVertexAttribArray(pos);GLES20.glVertexAttribPointer(pos,3,GLES20.GL_FLOAT,false,0,buffer(vertices));GLES20.glLineWidth(width);GLES20.glDrawArrays(GLES20.GL_LINES,0,vertices.length/3);GLES20.glDisableVertexAttribArray(pos);
    }
    private static void add(List<Float> out,float[] p){for(float v:p)out.add(v);}
    private static float[] array(List<Float> list){float[] a=new float[list.size()];for(int i=0;i<a.length;i++)a[i]=list.get(i);return a;}
    private static FloatBuffer buffer(float[] values){FloatBuffer b=ByteBuffer.allocateDirect(values.length*4).order(ByteOrder.nativeOrder()).asFloatBuffer();b.put(values).position(0);return b;}
    private static int shader(int type,String source){int s=GLES20.glCreateShader(type);GLES20.glShaderSource(s,source);GLES20.glCompileShader(s);int[] ok=new int[1];GLES20.glGetShaderiv(s,GLES20.GL_COMPILE_STATUS,ok,0);if(ok[0]==0)throw new IllegalStateException(GLES20.glGetShaderInfoLog(s));return s;}
    private static int program(String vertex,String fragment){int p=GLES20.glCreateProgram();GLES20.glAttachShader(p,shader(GLES20.GL_VERTEX_SHADER,vertex));GLES20.glAttachShader(p,shader(GLES20.GL_FRAGMENT_SHADER,fragment));GLES20.glLinkProgram(p);int[] ok=new int[1];GLES20.glGetProgramiv(p,GLES20.GL_LINK_STATUS,ok,0);if(ok[0]==0)throw new IllegalStateException(GLES20.glGetProgramInfoLog(p));return p;}
}
