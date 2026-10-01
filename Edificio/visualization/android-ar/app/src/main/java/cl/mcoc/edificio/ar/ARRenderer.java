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
                if(resetPending){if(anchor!=null)try{anchor.detach();}catch(Exception ignored){}anchor=null;activeImage=null;activeId=-1;resetPending=false;}
                session.setCameraTextureName(cameraTexture);
                session.setDisplayGeometry(activity.getWindowManager().getDefaultDisplay().getRotation(),width,height);
                Frame frame=session.update();if(frame.getTimestamp()==0)return;
                drawCamera(frame);
                Camera camera=frame.getCamera();
                if(camera.getTrackingState()!=TrackingState.TRACKING){activity.cameraStatus("Seguimiento pausado · "+camera.getTrackingFailureReason()+" · mueve el teléfono lentamente");return;}
                if(!activity.scanning)return;
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
