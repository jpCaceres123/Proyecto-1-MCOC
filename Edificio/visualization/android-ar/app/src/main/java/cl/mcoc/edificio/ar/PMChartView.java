package cl.mcoc.edificio.ar;

import android.content.Context;
import android.graphics.*;
import android.view.View;
import java.util.Locale;

/** Reference uniaxial P-M envelope and the selected station demand. */
public final class PMChartView extends View {
    private final Paint paint=new Paint(Paint.ANTI_ALIAS_FLAG);private final Path path=new Path();
    private StructuralData.PMPoint[] curve=new StructuralData.PMPoint[0];private float demandP,demandM;
    public PMChartView(Context context){super(context);setContentDescription("Curva P-M y punto de demanda");}
    public void show(StructuralData.PMPoint[] points,float p,float m){curve=points;demandP=p;demandM=Math.abs(m);invalidate();}
    @Override protected void onDraw(Canvas canvas){super.onDraw(canvas);if(curve.length<2)return;
        float left=dp(50),right=getWidth()-dp(15),top=dp(18),bottom=getHeight()-dp(32),maxM=1,maxP=1,minP=0;
        for(StructuralData.PMPoint p:curve){maxM=Math.max(maxM,p.m);maxP=Math.max(maxP,p.p);minP=Math.min(minP,p.p);}maxM=Math.max(maxM,demandM);maxP=Math.max(maxP,demandP);minP=Math.min(minP,demandP);
        paint.setColor(0xFF30465F);paint.setStrokeWidth(dp(1));float zero=bottom-(0-minP)/(maxP-minP)*(bottom-top);canvas.drawLine(left,zero,right,zero,paint);canvas.drawLine(left,top,left,bottom,paint);
        path.reset();for(int side=-1;side<=1;side+=2){for(int k=side<0?curve.length-1:0;side<0?k>=0:k<curve.length;k+=side<0?-1:1){StructuralData.PMPoint p=curve[k];float x=left+(side*p.m+maxM)/(2*maxM)*(right-left),y=bottom-(p.p-minP)/(maxP-minP)*(bottom-top);if(path.isEmpty())path.moveTo(x,y);else path.lineTo(x,y);}}
        paint.setStyle(Paint.Style.STROKE);paint.setColor(0xFF45E0C0);paint.setStrokeWidth(dp(2.4f));canvas.drawPath(path,paint);paint.setStyle(Paint.Style.FILL);
        float x=left+(demandM+maxM)/(2*maxM)*(right-left),y=bottom-(demandP-minP)/(maxP-minP)*(bottom-top);paint.setColor(0xFFFFC967);canvas.drawCircle(x,y,dp(5),paint);
        paint.setTextSize(dp(10));paint.setColor(0xFFB1C4D3);canvas.drawText("P [kN]",dp(4),top+dp(8),paint);canvas.drawText("M [kN·m]",right-dp(48),getHeight()-dp(7),paint);canvas.drawText(String.format(Locale.US,"Demanda: P %+.1f · |M| %.1f",demandP,demandM),left,dp(12),paint);
    }
    private float dp(float n){return n*getResources().getDisplayMetrics().density;}
}
