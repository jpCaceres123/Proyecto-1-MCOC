package cl.mcoc.edificio.ar;

import android.Manifest;
import android.app.*;
import android.content.*;
import android.content.pm.PackageManager;
import android.graphics.*;
import android.graphics.drawable.GradientDrawable;
import android.net.Uri;
import android.opengl.GLSurfaceView;
import android.os.Bundle;
import android.provider.Settings;
import android.view.*;
import android.widget.*;
import com.google.ar.core.*;
import java.io.*;
import java.util.*;

public final class MainActivity extends Activity {
    final Object sessionLock=new Object();
    volatile Session session;
    volatile StructuralData data;
    volatile StructuralData.Member selected;
    volatile String loadCase="R";
    volatile int component=4;
    volatile float station=0.5f, scale=0.10f;
    volatile float normalOffset=0;
    volatile boolean scanning=true;
    private GLSurfaceView surface;
    private ARRenderer renderer;
    private TextView status,title,values,position,coordinates,hash,extraSummary;
    private DiagramView plot;
    private Button scaleButton;
    private final ArrayList<Integer> activeIds=new ArrayList<>(Arrays.asList(1,241,246));
    private boolean foreground, installRequested, initializing;
    private volatile boolean destroyed;
    private long lastUiUpdate;
    private String lastTracking="", error="";
    private LinearLayout controls;

    @Override public void onCreate(Bundle state){
        super.onCreate(state);getWindow().addFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON);
        FrameLayout root=new FrameLayout(this); root.setBackgroundColor(Color.rgb(16,26,41));setContentView(root);
        root.setOnApplyWindowInsetsListener((v,insets)->{v.setPadding(insets.getSystemWindowInsetLeft(),insets.getSystemWindowInsetTop(),insets.getSystemWindowInsetRight(),insets.getSystemWindowInsetBottom());return insets;});
        surface=new GLSurfaceView(this);surface.setEGLContextClientVersion(2);surface.setPreserveEGLContextOnPause(true);
        renderer=new ARRenderer(this);surface.setRenderer(renderer);root.addView(surface,new FrameLayout.LayoutParams(-1,-1));
        LinearLayout header=column();header.setPadding(dp(16),dp(10),dp(16),dp(10));header.setBackgroundColor(0xE6101A29);
        TextView brand=text("EDIFICIO  /  AR",17,0xFF45E0C0);brand.setTypeface(null,Typeface.BOLD);header.addView(brand);
        status=text("Cargando resultados estructurales…",13,0xFFE4ECF3);header.addView(status);
        FrameLayout.LayoutParams head=new FrameLayout.LayoutParams(-1,-2,Gravity.TOP);root.addView(header,head);
        ScrollView scroll=new ScrollView(this);scroll.setFillViewport(false);scroll.setBackgroundColor(0xF5101A29);
        controls=column();controls.setPadding(dp(16),dp(10),dp(16),dp(12));scroll.addView(controls);
        FrameLayout.LayoutParams bottom=new FrameLayout.LayoutParams(-1,-2,Gravity.BOTTOM);root.addView(scroll,bottom);
        // Limit the sheet to 56% of the usable display, preserving a camera viewport.
        root.post(()->{ViewGroup.LayoutParams p=scroll.getLayoutParams();p.height=(int)(root.getHeight()*0.56f);scroll.setLayoutParams(p);});
        title=text("Apunta a un marcador",21,Color.WHITE);title.setTypeface(null,Typeface.BOLD);controls.addView(title);
        TextView note=text("Resultados OpenSees precalculados · ejes locales",11,0xFF94AABF);controls.addView(note);
        coordinates=text("",11,0xFFB1C4D3);controls.addView(coordinates);coordinates.setOnClickListener(v->calibrate());
        LinearLayout choose=row();
        choose.addView(spinner(StructuralData.CASES,4,index->{loadCase=StructuralData.CASES[index];refresh();}),new LinearLayout.LayoutParams(0,dp(43),1));
        choose.addView(spinner(StructuralData.COMPONENTS,4,index->{component=index;refresh();}),new LinearLayout.LayoutParams(0,dp(43),1));controls.addView(choose);
        values=text("N   —       Vy   —       Vz   —\nT   —       My   —       Mz   —",13,0xFFE4ECF3);values.setTypeface(Typeface.MONOSPACE);controls.addView(values);
        extraSummary=text("Desplazamiento, área tributaria y carga: esperando elemento",11,0xFFB1C4D3);controls.addView(extraSummary);
        plot=new DiagramView(this);controls.addView(plot,new LinearLayout.LayoutParams(-1,dp(104)));
        position=text("Posición i → j: 50%",12,0xFFFFC967);controls.addView(position);
        SeekBar seek=new SeekBar(this);seek.setMax(100);seek.setProgress(50);seek.setContentDescription("Posición a lo largo del elemento");
        seek.setOnSeekBarChangeListener(new SeekBar.OnSeekBarChangeListener(){public void onProgressChanged(SeekBar bar,int p,boolean user){station=p/100f;refresh();}public void onStartTrackingTouch(SeekBar b){}public void onStopTrackingTouch(SeekBar b){}});
        controls.addView(seek,new LinearLayout.LayoutParams(-1,dp(32)));
        LinearLayout buttons=row();buttons.addView(button("Resultados +",this::details),new LinearLayout.LayoutParams(0,dp(43),1));buttons.addView(button("Escanear IDs",this::chooseMarkers),new LinearLayout.LayoutParams(0,dp(43),1));buttons.addView(button("Catálogo",this::catalog),new LinearLayout.LayoutParams(0,dp(43),1));controls.addView(buttons);
        LinearLayout extras=row();scaleButton=button("Maqueta 1:10",()->{float newScale=scale<1?1:0.1f;normalOffset*=newScale/scale;scale=newScale;scaleButton.setText(scale<1?"Maqueta 1:10":"Escala real 1:1");});extras.addView(scaleButton,new LinearLayout.LayoutParams(0,dp(40),1));extras.addView(button("Reanclar",()->{renderer.reset();scanning=true;setStatus("Apunta otra vez al marcador para crear un anclaje.");}),new LinearLayout.LayoutParams(0,dp(40),1));extras.addView(button("Ayuda",this::help),new LinearLayout.LayoutParams(0,dp(40),0.7f));controls.addView(extras);
        hash=text("",10,0xFF94AABF);controls.addView(hash);
        new Thread(()->{try{data=new StructuralData(getAssets());runOnUiThread(()->{hash.setText(data.members.size()+" elementos · modelo "+data.modelHash.substring(0,12));setStatus("Preparando AR · marcadores 1, 241 y 246");if(foreground)resumeAR();});}catch(Exception e){runOnUiThread(()->setStatus("Error de datos: "+e.getMessage()));}},"structural-data").start();
    }
    private interface Choice{void select(int index);}
    private Spinner spinner(String[] labels,int initial,Choice choice){
        Spinner s=new Spinner(this);ArrayAdapter<String> adapter=new ArrayAdapter<>(this,android.R.layout.simple_spinner_item,labels);
        adapter.setDropDownViewResource(android.R.layout.simple_spinner_dropdown_item);s.setAdapter(adapter);s.setSelection(initial);
        s.setOnItemSelectedListener(new AdapterView.OnItemSelectedListener(){public void onItemSelected(AdapterView<?> p,View v,int pos,long id){if(v instanceof TextView)((TextView)v).setTextColor(Color.WHITE);choice.select(pos);}public void onNothingSelected(AdapterView<?> p){}});return s;
    }
    private TextView text(String t,int sp,int color){TextView view=new TextView(this);view.setText(t);view.setTextSize(sp);view.setTextColor(color);view.setPadding(0,dp(3),0,dp(3));return view;}
    private LinearLayout column(){LinearLayout l=new LinearLayout(this);l.setOrientation(LinearLayout.VERTICAL);return l;}
    private LinearLayout row(){LinearLayout l=new LinearLayout(this);l.setOrientation(LinearLayout.HORIZONTAL);return l;}
    private Button button(String label,Runnable action){Button b=new Button(this);b.setText(label);b.setTextSize(11);b.setAllCaps(false);b.setTextColor(0xFF45E0C0);b.setPadding(dp(3),0,dp(3),0);GradientDrawable bg=new GradientDrawable();bg.setColor(0xFF203247);bg.setCornerRadius(dp(8));bg.setStroke(dp(1),0xFF30465F);b.setBackground(bg);b.setOnClickListener(v->action.run());return b;}
    int dp(float n){return Math.round(n*getResources().getDisplayMetrics().density);}
    void setStatus(String message){status.setText(message);}
    private void refresh(){
        if(selected==null||values==null)return;
        StructuralData.Member m=selected;StructuralData.Response r=m.cases.get(loadCase);title.setText(m.label());
        coordinates.setText(String.format(Locale.US,"i (%s) → j (%s) m · L %.2f m · %.0f × %.0f cm",xyz(m.start),xyz(m.end),m.length,m.width*100,m.height*100));
        values.setText(String.format(Locale.US,"N %+.2f  Vy %+.2f  Vz %+.2f kN\nT %+.2f  My %+.2f  Mz %+.2f kN·m",r.at(0,station),r.at(1,station),r.at(2,station),r.at(3,station),r.at(4,station),r.at(5,station)));
        float[] u=r.displacement(station,m.length,m.localX);float um=(float)Math.sqrt(u[0]*u[0]+u[1]*u[1]+u[2]*u[2]);
        extraSummary.setText(String.format(Locale.US,"|u| %.3f mm · Área tributaria %.3f m² · Carga %s %.2f kN",um*1000,m.loadInfo.tributaryArea,loadCase,m.loadInfo.applied.get(loadCase)));
        position.setText(String.format(Locale.US,"Caso %s · x = %.2f m (%.0f%% i → j) · %s",loadCase,station*m.length,station*100,StructuralData.COMPONENTS[component]));plot.show(r,component,station,m.length);
    }
    private String xyz(float[] p){return String.format(Locale.US,"%.2f, %.2f, %.2f",p[0],p[1],p[2]);}
    private void details(){
        if(selected==null){setStatus("Primero detecta o selecciona un elemento.");return;}
        StructuralData.Member m=selected;StructuralData.Response r=m.cases.get(loadCase);float[] u=r.displacement(station,m.length,m.localX);
        ScrollView scroll=new ScrollView(this);LinearLayout box=column();box.setPadding(dp(18),dp(8),dp(18),dp(18));scroll.addView(box);
        box.addView(text(String.format(Locale.US,"Desplazamiento interpolado · caso %s · x %.2f m",loadCase,station*m.length),16,0xFF101A29));
        box.addView(text(String.format(Locale.US,"Ux %+.3f mm   Uy %+.3f mm   Uz %+.3f mm\n|u| %.3f mm",u[0]*1000,u[1]*1000,u[2]*1000,Math.sqrt(u[0]*u[0]+u[1]*u[1]+u[2]*u[2])*1000),13,0xFF243549));
        box.addView(text("Área tributaria asociada",16,0xFF101A29));
        String slabs=m.loadInfo.slabIds.length==0?"Ninguna losa asignada directamente":Arrays.toString(m.loadInfo.slabIds);
        box.addView(text(String.format(Locale.US,"Área total: %.3f m²\nLosas: %s",m.loadInfo.tributaryArea,slabs),13,0xFF243549));
        box.addView(text("Carga aplicada al elemento",16,0xFF101A29));
        box.addView(text(String.format(Locale.US,"Peso propio barra: %.2f kN\nG de losas: %.2f kN · Q de losas: %.2f kN\nTotal seleccionado %s: %.2f kN",m.loadInfo.selfWeight,m.loadInfo.slabDead,m.loadInfo.slabLive,loadCase,m.loadInfo.applied.get(loadCase)),13,0xFF243549));
        box.addView(text("Los totales son resultantes verticales distribuidas. EX/EY se aplican en nodos de diafragma; por eso su carga directa en esta barra es 0 kN.",11,0xFF526A7F));
        box.addView(text("Curva P-M",16,0xFF101A29));
        box.addView(text(m.pmNote,12,0xFF526A7F));
        if(m.pm.length>0){float p=r.at(0,station),my=r.at(4,station),mz=r.at(5,station);PMChartView chart=new PMChartView(this);chart.show(m.pm,p,(float)Math.sqrt(my*my+mz*mz));box.addView(chart,new LinearLayout.LayoutParams(-1,dp(235)));box.addView(text("Punto amarillo: P y resultante |M| de la estación. Comparación referencial con envolvente uniaxial; no constituye verificación biaxial de capacidad.",11,0xFF526A7F));}
        new AlertDialog.Builder(this).setTitle(m.label()+" · resultados").setView(scroll).setPositiveButton("Cerrar",null).show();
    }
    private void calibrate(){
        EditText input=new EditText(this);input.setText(Float.toString(normalOffset));input.setInputType(android.text.InputType.TYPE_CLASS_NUMBER|android.text.InputType.TYPE_NUMBER_FLAG_DECIMAL|android.text.InputType.TYPE_NUMBER_FLAG_SIGNED);
        String suggestion=selected==null?"":String.format(Locale.US,"\nPara la cara normal al eje local z de este elemento: offset inicial %.3f m a la escala actual.",-selected.extentZ*scale/2);
        AlertDialog d=new AlertDialog.Builder(this).setTitle("Calibrar centro del elemento").setMessage("Distancia normal al marcador en metros AR. Un valor negativo lleva el eje hacia el interior del elemento. Usa la mitad de la profundidad perpendicular a la cara de montaje, con la escala actual. El contorno respeta A/Iy/Iz y sus ejes locales."+suggestion+"\nAjusta según la cara donde fijaste la imagen.").setView(input).setPositiveButton("Aplicar",null).setNegativeButton("Cancelar",null).create();
        d.setOnShowListener(x->d.getButton(AlertDialog.BUTTON_POSITIVE).setOnClickListener(v->{try{float n=Float.parseFloat(input.getText().toString().replace(',','.'));if(!Float.isFinite(n)||Math.abs(n)>2)throw new IllegalArgumentException("Usa un offset entre −2 y +2 m");normalOffset=n;d.dismiss();}catch(Exception e){input.setError(e.getMessage());}}));d.show();
    }
    void detected(int id,String tracking){
        long now=android.os.SystemClock.elapsedRealtime();if(now-lastUiUpdate<300&&tracking.equals(lastTracking)&&selected!=null&&selected.id==id)return;lastUiUpdate=now;lastTracking=tracking;
        runOnUiThread(()->{if(!scanning)return;StructuralData.Member m=data.members.get(id);if(m==null)return;selected=m;setStatus(tracking+" · "+m.label());refresh();});
    }
    void cameraStatus(String message){long now=android.os.SystemClock.elapsedRealtime();if(now-lastUiUpdate<1000)return;lastUiUpdate=now;runOnUiThread(()->{if(scanning)setStatus(message);});}
    @Override protected void onResume(){super.onResume();foreground=true;surface.onResume();if(data!=null)resumeAR();}
    @Override protected void onPause(){foreground=false;surface.onPause();synchronized(sessionLock){if(session!=null)session.pause();}super.onPause();}
    @Override protected void onDestroy(){destroyed=true;synchronized(sessionLock){if(session!=null){session.close();session=null;}}super.onDestroy();}
    private void resumeAR(){
        if(!foreground||initializing)return;
        if(checkSelfPermission(Manifest.permission.CAMERA)!=PackageManager.PERMISSION_GRANTED){requestPermissions(new String[]{Manifest.permission.CAMERA},20);return;}
        try{
            if(session==null){
                if(ArCoreApk.getInstance().requestInstall(this,!installRequested)==ArCoreApk.InstallStatus.INSTALL_REQUESTED){installRequested=true;setStatus("Instala Google Play Services for AR y vuelve a la aplicación.");return;}
                initializeSession();return;
            }
            synchronized(sessionLock){session.resume();}error="";setStatus("Busca un marcador activo · "+activeIds.toString());
        }catch(Exception e){error=e.getClass().getSimpleName()+": "+e.getMessage();setStatus("AR no disponible: "+error+". Puedes consultar el catálogo.");}
    }
    private void initializeSession(){
        initializing=true;setStatus("Preparando "+activeIds.size()+" imágenes de referencia…");
        ArrayList<Integer> ids=new ArrayList<>(activeIds);
        new Thread(()->{
            Session created=null;
            try{
                created=new Session(this);Config config=new Config(created);
                config.setFocusMode(Config.FocusMode.AUTO);config.setPlaneFindingMode(Config.PlaneFindingMode.DISABLED);
                AugmentedImageDatabase db;
                String key=data.modelHash.substring(0,12)+"_"+data.markerHash.substring(0,12)+"_"+ids.toString().replaceAll("[^0-9,]","");
                File cached=new File(getCacheDir(),key+".imgdb");
                if(cached.exists()){try(InputStream in=new FileInputStream(cached)){db=AugmentedImageDatabase.deserialize(created,in);}}
                else{
                    db=new AugmentedImageDatabase(created);
                    for(int id:ids){StructuralData.Member m=data.members.get(id);Bitmap bitmap;
                        try(InputStream in=getAssets().open(m.marker)){bitmap=BitmapFactory.decodeStream(in);}
                        if(bitmap==null)throw new IOException("No se pudo leer marcador "+id);
                        try{db.addImage("element_"+id,bitmap,m.markerWidth);}finally{bitmap.recycle();}
                    }
                    try(OutputStream out=new FileOutputStream(cached)){db.serialize(out);}
                }
                config.setAugmentedImageDatabase(db);created.configure(config);
                Session ready=created;
                runOnUiThread(()->{if(destroyed){ready.close();return;}synchronized(sessionLock){session=ready;}initializing=false;if(foreground)resumeAR();});
            }catch(Exception e){if(created!=null)created.close();runOnUiThread(()->{initializing=false;setStatus("No se pudo iniciar AR: "+e.getMessage()+". Catálogo disponible.");});}
        },"ar-images").start();
    }
    @Override public void onRequestPermissionsResult(int req,String[] permissions,int[] results){super.onRequestPermissionsResult(req,permissions,results);if(req==20){if(results.length>0&&results[0]==PackageManager.PERMISSION_GRANTED)resumeAR();else new AlertDialog.Builder(this).setTitle("Permiso de cámara").setMessage("La detección de marcadores necesita la cámara. El catálogo puede usarse sin este permiso.").setPositiveButton("Ajustes",(d,w)->startActivity(new Intent(Settings.ACTION_APPLICATION_DETAILS_SETTINGS,Uri.parse("package:"+getPackageName())))).setNegativeButton("Catálogo",(d,w)->catalog()).show();}}
    private void chooseMarkers(){
        if(data==null||initializing)return;
        EditText input=new EditText(this);input.setSingleLine(false);input.setText(activeIds.toString().replace("[","").replace("]",""));input.setHint("1, 241, 246");
        AlertDialog dialog=new AlertDialog.Builder(this).setTitle("Marcadores activos").setMessage("IDs separados por coma (máximo 24 por sesión). Usa las imágenes correspondientes del catálogo imprimible; ancho real 20 cm.").setView(input).setPositiveButton("Activar",null).setNegativeButton("Cancelar",null).create();
        dialog.setOnShowListener(d->dialog.getButton(AlertDialog.BUTTON_POSITIVE).setOnClickListener(v->{
            try{LinkedHashSet<Integer> ids=new LinkedHashSet<>();for(String token:input.getText().toString().split(",")){int id=Integer.parseInt(token.trim());if(!data.members.containsKey(id))throw new IllegalArgumentException("No existe la viga/columna "+id);ids.add(id);}
                if(ids.size()<1||ids.size()>24)throw new IllegalArgumentException("Selecciona entre 1 y 24 IDs");
                surface.onPause();renderer.reset();synchronized(sessionLock){if(session!=null){session.pause();session.close();session=null;}}
                activeIds.clear();activeIds.addAll(ids);selected=null;title.setText("Apunta a un marcador");values.setText("Esperando detección de un elemento");extraSummary.setText("Desplazamiento, área tributaria y carga: esperando elemento");coordinates.setText("");plot.show(null,component,station,1);scanning=true;surface.onResume();dialog.dismiss();resumeAR();
            }catch(Exception e){input.setError(e.getMessage());}
        }));dialog.show();
    }
    private void catalog(){
        if(data==null)return;LinearLayout content=column();EditText search=new EditText(this);search.setHint("Buscar ID, viga o columna");content.addView(search);
        ListView list=new ListView(this);content.addView(list,new LinearLayout.LayoutParams(-1,dp(350)));
        ArrayList<String> labels=new ArrayList<>();for(StructuralData.Member m:data.members.values())labels.add(m.id+" · "+(m.isColumn()?"Columna":"Viga")+" · "+String.format(Locale.US,"%.2f m",m.length));
        ArrayAdapter<String> adapter=new ArrayAdapter<>(this,android.R.layout.simple_list_item_1,labels);list.setAdapter(adapter);
        search.addTextChangedListener(new android.text.TextWatcher(){public void beforeTextChanged(CharSequence s,int a,int c,int f){}public void onTextChanged(CharSequence s,int a,int b,int c){adapter.getFilter().filter(s);}public void afterTextChanged(android.text.Editable s){}});
        AlertDialog dialog=new AlertDialog.Builder(this).setTitle("Catálogo · "+data.members.size()+" elementos").setView(content).setNegativeButton("Cerrar",null).create();
        list.setOnItemClickListener((p,v,pos,id)->{String label=adapter.getItem(pos);int tag=Integer.parseInt(label.split(" · ")[0]);scanning=false;renderer.reset();selected=data.members.get(tag);setStatus("Catálogo · selección manual · sin detección AR");refresh();dialog.dismiss();});dialog.show();
    }
    private void help(){new AlertDialog.Builder(this).setTitle("Identificación y esfuerzos").setMessage("1. Imprime el marcador completo a 20 × 20 cm, sin ajustar escala.\n2. Colócalo fijo, con ARRIBA vertical, centrado en el punto medio del elemento. En vigas, el lado derecho apunta de i a j; en columnas, ARRIBA apunta de i a j.\n3. Activa su ID en Escanear IDs y apunta con buena luz. La aplicación crea un anclaje; Reanclar permite corregirlo.\n4. Cambia caso, esfuerzo y estación. N/V se expresan en kN; T/M en kN·m. Los signos corresponden a los ejes locales OpenSees.\n5. Maqueta 1:10 reduce la geometría; Escala real 1:1 permite alinear el eje en obra. La altura del diagrama siempre está normalizada a 0,35 m.\n\nLa cámara identifica una imagen, no calcula esfuerzos a partir de la apariencia del hormigón. Los resultados se calcularon previamente; no se reanaliza en el teléfono. Las imágenes deben representar el ID físico correcto.\n\nLa colocación del marcador y su offset se deben calibrar en terreno. La imagen en la cara exterior dibuja el eje sobre esa cara, con un desfase respecto al eje del modelo. Ver guía para el ajuste de profundidad.\n\nModelo: "+(data==null?"cargando":data.modelHash)).setPositiveButton("Entendido",null).show();}
}
