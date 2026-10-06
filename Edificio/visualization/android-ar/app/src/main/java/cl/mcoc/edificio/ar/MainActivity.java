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
    volatile SectorRegistration sector;
    volatile boolean showDeformed,showAreas,showCapacity;
    final LinkedHashSet<Integer> compared=new LinkedHashSet<>();
    private long sectorStarted;
    private String baseModelHash;
    final AnalysisBackend backend=new AnalysisBackend();
    volatile org.json.JSONObject capacitySnapshot;
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
    private FrameLayout sceneRoot;
    private final ArrayList<TextView> sceneLabels=new ArrayList<>();
    private long lastLabelUpdate;

    @Override public void onCreate(Bundle state){
        super.onCreate(state);getWindow().addFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON);
        FrameLayout root=new FrameLayout(this); root.setBackgroundColor(Color.rgb(16,26,41));setContentView(root);
        sceneRoot=root;
        root.setOnApplyWindowInsetsListener((v,insets)->{v.setPadding(insets.getSystemWindowInsetLeft(),insets.getSystemWindowInsetTop(),insets.getSystemWindowInsetRight(),insets.getSystemWindowInsetBottom());return insets;});
        surface=new GLSurfaceView(this);surface.setEGLContextClientVersion(2);surface.setPreserveEGLContextOnPause(true);
        renderer=new ARRenderer(this);surface.setRenderer(renderer);root.addView(surface,new FrameLayout.LayoutParams(-1,-1));
        for(int k=0;k<4;k++){TextView label=text("",11,Color.WHITE);label.setBackgroundColor(0xC0101A29);label.setVisibility(View.GONE);root.addView(label,new FrameLayout.LayoutParams(-2,-2));sceneLabels.add(label);}
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
        LinearLayout advanced=row();advanced.addView(button("Sector JSON",()->{Intent i=new Intent(Intent.ACTION_OPEN_DOCUMENT);i.setType("application/json");i.addCategory(Intent.CATEGORY_OPENABLE);startActivityForResult(i,31);}),new LinearLayout.LayoutParams(0,dp(40),1));
        advanced.addView(button("Añadir/quitar",()->{if(selected==null)return;synchronized(compared){if(!compared.remove(selected.id)){if(compared.size()>=4){setStatus("Máximo cuatro barras comparadas");return;}compared.add(selected.id);}}setStatus("Comparando "+compared);}),new LinearLayout.LayoutParams(0,dp(40),1));
        advanced.addView(button("Deformada",()->{showDeformed=!showDeformed;setStatus(showDeformed?"Hermite nodal ×100; no flecha interior exacta":"Deformada oculta");}),new LinearLayout.LayoutParams(0,dp(40),1));controls.addView(advanced);
        LinearLayout evidence=row();evidence.addView(button("Áreas",()->{showAreas=!showAreas;}),new LinearLayout.LayoutParams(0,dp(40),1));
        evidence.addView(button("P–Mz",()->{showCapacity=!showCapacity;setStatus("Capacidad nominal uniaxial Mz; no comprobación biaxial");}),new LinearLayout.LayoutParams(0,dp(40),1));
        evidence.addView(button("Medir error",this::measureSector),new LinearLayout.LayoutParams(0,dp(40),1));evidence.addView(button("Exportar",this::exportSector),new LinearLayout.LayoutParams(0,dp(40),1));controls.addView(evidence);
        controls.addView(button("Salir del sector medido",this::leaveSector));
        LinearLayout live=row();live.addView(button("PC Wi-Fi",this::configureBackend),new LinearLayout.LayoutParams(0,dp(40),1));live.addView(button("Q +2 / calcular",()->remoteCalculation(false)),new LinearLayout.LayoutParams(0,dp(40),1));live.addView(button("Refuerzo Ø32",()->remoteCalculation(true)),new LinearLayout.LayoutParams(0,dp(40),1));live.addView(button("Cancelar",()->backend.cancel=true),new LinearLayout.LayoutParams(0,dp(40),1));controls.addView(live);
        new Thread(()->{try{data=new StructuralData(getAssets());baseModelHash=data.modelHash;runOnUiThread(()->{restoreSector();hash.setText(data.members.size()+" elementos · modelo "+data.modelHash.substring(0,12));setStatus("Preparando AR · marcadores 1, 241 y 246");if(foreground)resumeAR();});}catch(Exception e){runOnUiThread(()->setStatus("Error de datos: "+e.getMessage()));}},"structural-data").start();
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
        if(m.pm.length>0){float p=r.at(0,station),mz=r.at(5,station);PMChartView chart=new PMChartView(this);chart.show(m.pm,p,Math.abs(mz));box.addView(chart,new LinearLayout.LayoutParams(-1,dp(235)));box.addView(text("Punto amarillo: P y |Mz| del mismo eje de la curva nominal. No verifica demanda biaxial ni capacidad de miembro.",11,0xFF526A7F));}
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
        runOnUiThread(()->{if(!scanning)return;StructuralData.Member m=data.members.get(id);if(m==null)return;if(sector==null||selected==null)selected=m;setStatus(tracking+" · "+m.label());refresh();});
    }
    void cameraStatus(String message){long now=android.os.SystemClock.elapsedRealtime();if(now-lastUiUpdate<1000)return;lastUiUpdate=now;runOnUiThread(()->{if(scanning)setStatus(message);});}
    @Override protected void onActivityResult(int request,int result,Intent intent){super.onActivityResult(request,result,intent);
        if(request!=31||result!=RESULT_OK||intent==null)return;
        try(InputStream in=getContentResolver().openInputStream(intent.getData())){ByteArrayOutputStream bytes=new ByteArrayOutputStream();byte[] buf=new byte[8192];int n;while((n=in.read(buf))!=-1){bytes.write(buf,0,n);if(bytes.size()>1000000)throw new IOException("Formulario demasiado grande");}
            String json=bytes.toString("UTF-8");SectorRegistration next=new SectorRegistration(new org.json.JSONObject(json),data);
            synchronized(sessionLock){if(sector!=null)sector.reset();renderer.reset();sector=next;}
            try(FileOutputStream out=openFileOutput("sector.json",MODE_PRIVATE)){out.write(json.getBytes(java.nio.charset.StandardCharsets.UTF_8));}
            sectorStarted=android.os.SystemClock.elapsedRealtime();scale=1;normalOffset=0;scaleButton.setText("Sector métrico 1:1");scanning=true;useSectorMarkers();setStatus("Sector medido cargado · muestra dos marcadores");
        }catch(Exception e){setStatus("Sector rechazado: "+e.getMessage());}}
    private void measureSector(){if(sector==null){setStatus("Importa primero un sector medido");return;}
        EditText input=new EditText(this);input.setHint("Etiqueta, X, Y, Z del punto de control independiente [m]");
        new AlertDialog.Builder(this).setTitle("Error de alineamiento").setMessage("Apunta el centro de la cámara al punto físico independiente. Se usará un hit ARCore; no un error estimado desde el marcador.").setView(input).setPositiveButton("Medir",(d,w)->{
            try{String[] a=input.getText().toString().split(",");if(a.length!=4)throw new IllegalArgumentException("Etiqueta,X,Y,Z");float[] p={Float.parseFloat(a[1]),Float.parseFloat(a[2]),Float.parseFloat(a[3])};for(float v:p)if(!Float.isFinite(v))throw new IllegalArgumentException("Coordenada no finita");renderer.requestMeasurement(p,a[0],android.os.SystemClock.elapsedRealtime()-sectorStarted);}
            catch(Exception e){setStatus(e.getMessage());}}).setNegativeButton("Cancelar",null).show();}
    private void exportSector(){try{if(sector==null)throw new IllegalStateException("Sin sector");File file=new File(getExternalFilesDir(null),"sector-errors-"+System.currentTimeMillis()+".json");synchronized(sessionLock){try(FileOutputStream out=new FileOutputStream(file)){out.write(sector.report().toString(2).getBytes(java.nio.charset.StandardCharsets.UTF_8));}}setStatus("Reporte: "+file.getAbsolutePath());}catch(Exception e){setStatus(e.getMessage());}}
    private void restoreSector(){try{File f=new File(getFilesDir(),"sector.json");if(!f.exists())return;ByteArrayOutputStream bytes=new ByteArrayOutputStream();try(InputStream in=new FileInputStream(f)){byte[] b=new byte[8192];int n;while((n=in.read(b))!=-1)bytes.write(b,0,n);}sector=new SectorRegistration(new org.json.JSONObject(bytes.toString("UTF-8")),data);sectorStarted=android.os.SystemClock.elapsedRealtime();scale=1;normalOffset=0;scaleButton.setText("Sector métrico 1:1");activeIds.clear();activeIds.addAll(sector.markers.keySet());}catch(Exception e){sector=null;setStatus("Calibración anterior incompatible; importa nuevo sector");}}
    private void useSectorMarkers(){surface.onPause();synchronized(sessionLock){if(session!=null){session.close();session=null;}activeIds.clear();activeIds.addAll(sector.markers.keySet());}surface.onResume();resumeAR();}
    private void leaveSector(){synchronized(sessionLock){if(sector!=null)sector.reset();sector=null;renderer.reset();}deleteFile("sector.json");synchronized(compared){compared.clear();}for(TextView label:sceneLabels)label.setVisibility(View.GONE);scale=.1f;normalOffset=0;scaleButton.setText("Maqueta 1:10");setStatus("Modo marcador individual · calibración guardada eliminada");}
    private void configureBackend(){LinearLayout box=column();EditText url=new EditText(this),token=new EditText(this);url.setText(backend.server);token.setHint("Token del PC");token.setInputType(android.text.InputType.TYPE_CLASS_TEXT|android.text.InputType.TYPE_TEXT_VARIATION_PASSWORD);box.addView(url);box.addView(token);
        new AlertDialog.Builder(this).setTitle("Backend PC · misma Wi-Fi").setView(box).setPositiveButton("Aplicar",(d,w)->{if(backend.busy){setStatus("Espera o cancela el trabajo antes de cambiar conexión");return;}backend.server=url.getText().toString().replaceAll("/+$","");backend.token=token.getText().toString();}).setNegativeButton("Cerrar",null).show();}
    private void remoteCalculation(boolean capacity){if(selected==null){setStatus("Selecciona una barra");return;}if(capacity&&(!selected.type.equals("COLUMN")||!data.modelHash.equals(baseModelHash))){setStatus("Capacidad: columna HA del modelo base");return;}
        final int tag=selected.id;backend.tagType=selected.isColumn()?"Columna":"Viga";backend.submit(baseModelHash,tag,capacity,new File(getFilesDir(),"AnalysisJobs"),new AnalysisBackend.Listener(){public void status(String s){runOnUiThread(()->setStatus(s));}public void accepted(StructuralData next,org.json.JSONObject result){runOnUiThread(()->{synchronized(sessionLock){if(next!=null){if(sector!=null)sector.reset();sector=null;renderer.reset();data=next;selected=data.members.get(tag);capacitySnapshot=null;hash.setText("Variante · modelo "+data.modelHash.substring(0,12));}else capacitySnapshot=result;}refresh();});}});}
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
        if(sector!=null){String[] labels=new String[sector.elements.size()];int[] ids=new int[labels.length];int k=0;for(int id:sector.elements){ids[k]=id;labels[k++]=data.members.get(id).label();}
            new AlertDialog.Builder(this).setTitle("Seleccionar en sector · después Añadir/quitar").setItems(labels,(d,w)->{selected=data.members.get(ids[w]);scanning=true;refresh();}).setNegativeButton("Cerrar",null).show();return;}
        if(data==null)return;LinearLayout content=column();EditText search=new EditText(this);search.setHint("Buscar ID, viga o columna");content.addView(search);
        ListView list=new ListView(this);content.addView(list,new LinearLayout.LayoutParams(-1,dp(350)));
        ArrayList<String> labels=new ArrayList<>();for(StructuralData.Member m:data.members.values())labels.add(m.id+" · "+(m.isColumn()?"Columna":"Viga")+" · "+String.format(Locale.US,"%.2f m",m.length));
        ArrayAdapter<String> adapter=new ArrayAdapter<>(this,android.R.layout.simple_list_item_1,labels);list.setAdapter(adapter);
        search.addTextChangedListener(new android.text.TextWatcher(){public void beforeTextChanged(CharSequence s,int a,int c,int f){}public void onTextChanged(CharSequence s,int a,int b,int c){adapter.getFilter().filter(s);}public void afterTextChanged(android.text.Editable s){}});
        AlertDialog dialog=new AlertDialog.Builder(this).setTitle("Catálogo · "+data.members.size()+" elementos").setView(content).setNegativeButton("Cerrar",null).create();
        list.setOnItemClickListener((p,v,pos,id)->{String label=adapter.getItem(pos);int tag=Integer.parseInt(label.split(" · ")[0]);scanning=false;renderer.reset();selected=data.members.get(tag);setStatus("Catálogo · selección manual · sin detección AR");refresh();dialog.dismiss();});dialog.show();
    }
    void labels(List<String> messages,List<float[]> positions){long now=android.os.SystemClock.elapsedRealtime();if(now-lastLabelUpdate<150)return;lastLabelUpdate=now;
        runOnUiThread(()->{for(int k=0;k<sceneLabels.size();k++){TextView label=sceneLabels.get(k);if(k>=messages.size()){label.setVisibility(View.GONE);continue;}float[] p=positions.get(k);label.setText(messages.get(k));label.setX(p[0]);label.setY(p[1]);label.setVisibility(View.VISIBLE);}});}
    private void help(){new AlertDialog.Builder(this).setTitle("Identificación y esfuerzos").setMessage("1. Imprime el marcador completo a 20 × 20 cm, sin ajustar escala.\n2. Colócalo fijo, con ARRIBA vertical, centrado en el punto medio del elemento. En vigas, el lado derecho apunta de i a j; en columnas, ARRIBA apunta de i a j.\n3. Activa su ID en Escanear IDs y apunta con buena luz. La aplicación crea un anclaje; Reanclar permite corregirlo.\n4. Cambia caso, esfuerzo y estación. N/V se expresan en kN; T/M en kN·m. Los signos corresponden a los ejes locales OpenSees.\n5. Maqueta 1:10 reduce la geometría; Escala real 1:1 permite alinear el eje en obra. La altura del diagrama siempre está normalizada a 0,35 m.\n\nLa cámara identifica una imagen, no calcula esfuerzos a partir de la apariencia del hormigón. Los resultados se calcularon previamente; no se reanaliza en el teléfono. Las imágenes deben representar el ID físico correcto.\n\nLa colocación del marcador y su offset se deben calibrar en terreno. La imagen en la cara exterior dibuja el eje sobre esa cara, con un desfase respecto al eje del modelo. Ver guía para el ajuste de profundidad.\n\nModelo: "+(data==null?"cargando":data.modelHash)).setPositiveButton("Entendido",null).show();}
}
