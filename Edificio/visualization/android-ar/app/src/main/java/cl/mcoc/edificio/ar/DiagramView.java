package cl.mcoc.edificio.ar;

import android.content.Context;
import android.graphics.*;
import android.view.View;
import java.util.Locale;

/** Signed physical values; plot height is normalized independently of the member scale. */
public final class DiagramView extends View {
    private final Paint paint = new Paint(Paint.ANTI_ALIAS_FLAG);
    private final Path path = new Path();
    private StructuralData.Response response;
    private int component;
    private float station, length;
    public DiagramView(Context context){super(context);setContentDescription("Diagrama firmado de esfuerzos a lo largo del elemento");}
    public void show(StructuralData.Response r,int c,float s,float l){response=r;component=c;station=s;length=l;invalidate();}
    @Override protected void onDraw(Canvas canvas){
        super.onDraw(canvas); float w=getWidth(),h=getHeight(),left=dp(40),right=w-dp(14),top=dp(17),bottom=h-dp(22),mid=(top+bottom)/2;
        paint.setColor(Color.rgb(36,53,73));paint.setStrokeWidth(dp(1));
        canvas.drawLine(left,mid,right,mid,paint);canvas.drawLine(left,top,left,bottom,paint);
        if(response==null)return;
        float peak=response.maxAbs(component),den=Math.max(peak,0.000001f),amp=(bottom-top)*0.43f;
        path.reset();
        for(int k=0;k<response.s.length;k++){
            float x=left+(right-left)*response.s[k],y=mid-response.values[component][k]/den*amp;
            if(k==0)path.moveTo(x,y);else path.lineTo(x,y);
        }
        paint.setColor(Color.rgb(69,224,192));paint.setStrokeWidth(dp(2.4f));paint.setStyle(Paint.Style.STROKE);canvas.drawPath(path,paint);paint.setStyle(Paint.Style.FILL);
        float x=left+(right-left)*station,y=mid-response.at(component,station)/den*amp;
        paint.setColor(Color.rgb(255,201,103));canvas.drawCircle(x,y,dp(4),paint);paint.setStrokeWidth(dp(1));canvas.drawLine(x,top,x,bottom,paint);
        paint.setColor(Color.rgb(168,188,207));paint.setTextSize(dp(10));
        canvas.drawText("+",dp(18),top+dp(8),paint);canvas.drawText("0",dp(18),mid+dp(4),paint);canvas.drawText("−",dp(18),bottom,paint);
        canvas.drawText("i · 0 m",left,h-dp(5),paint);String end=String.format(Locale.US,"j · %.2f m",length);canvas.drawText(end,right-paint.measureText(end),h-dp(5),paint);
        String max=String.format(Locale.US,"máx |%s| %.2f %s",StructuralData.COMPONENTS[component],peak,component<3?"kN":"kN·m");canvas.drawText(max,left,dp(12),paint);
    }
    private float dp(float n){return n*getResources().getDisplayMetrics().density;}
}
