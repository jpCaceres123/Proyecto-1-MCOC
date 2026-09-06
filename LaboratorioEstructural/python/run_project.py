"""Run from any working directory: python run_project.py [--overrides file.json]."""
import argparse, json, os, shutil, hashlib, sys
from datetime import datetime, timezone
from pathlib import Path
import numpy as np
from model import ROOT, read_tables, load_config, build_geometry
from solver import load_cases, run_case, combine_loads, verify_superposition
from capacity import calculate_capacities

def write_json(path,obj):
    path.parent.mkdir(parents=True,exist_ok=True)
    temporary=path.with_suffix('.tmp')
    temporary.write_text(json.dumps(obj,ensure_ascii=False,allow_nan=False,separators=(',',':')),encoding='utf8')
    os.replace(temporary,path)

def run(overrides=None):
    c=load_config(overrides);source=(ROOT/c['workbook']).resolve()
    tables=read_tables(source);model=build_geometry(c,tables)
    loads,totals=load_cases(model,tables,c)
    base={key:run_case(model,c,loads[key],key) for key in ('G','Q','EX','EY')}
    factors={'G':1.2,'Q':.5,'EX':.8,'EY':-.3}
    explicit=run_case(model,c,combine_loads(loads,factors),'COMB_CHECK')
    check=verify_superposition(base,explicit,factors)
    # Check diaphragm kinematics numerically without introducing additional constraints.
    diaphragm_error=0.
    nm={n['id']:n for n in model['nodes']}
    for case in base.values():
        result={n['id']:n['u'] for n in case['nodes']}
        for d in model['diaphragms']:
            ref=d['nodes'][0];a=nm[ref];ua=result[ref]
            for tag in d['nodes']:
                b=nm[tag];ub=result[tag]
                err=max(abs(ub[0]-ua[0]+ua[5]*(b['y']-a['y'])),abs(ub[1]-ua[1]-ua[5]*(b['x']-a['x'])),abs(ub[5]-ua[5]))
                diaphragm_error=max(diaphragm_error,err)
    if diaphragm_error>1e-8:raise AssertionError('Diaphragm compatibility')
    capacities=calculate_capacities(c,model)
    notes=[
        'Modelo académico lineal con rigideces brutas; no es verificación normativa del edificio.',
        'Muros: barras verticales en el centroide por piso y brazos de rigidez finita sin masa. Revisar convención del curso.',
        'Junta libre de 0,10 m entre caras de muros; se recortaron conexiones del Excel que la atravesaban.',
        'Losas gráficas sin elementos finitos; G/Q provienen de CasosCargaViga y el peso propio de barras/muros se suma una vez.',
        '473 filas de carga proceden de missing_area_to_nearest_beam: esa asignación heredada requiere revisión de áreas tributarias.',
        'Perfiles triangular/trapezoidal reconstruidos y normalizados al total tabulado por fila; se integran con cuadratura por tramos. No se conservan picos incompatibles con ese total.',
        'Sismo idealizado: V=C·W y reparto W_i·z_i; C=0,10 editable. Sin comprobación normativa de separación sísmica.',
        'Capacidades con armaduras didácticas editables, sin factores de reducción; no representan capacidad real hasta verificar el armado.',
        'Diagramas de fuerzas locales de las cargas puntuales equivalentes. Deformada cúbica por desplazamientos nodales; no incluye solución particular de carga entre nodos.',
        'Nodos auxiliares de losas excluidos del dominio; bases de muros a z=0 empotradas. Todas las bases de columnas están empotradas en sus seis GDL, incluidos los arranques elevados. Los tres pilares del eje I (X=40 m) arrancan en Z=3,96 m.'
    ]
    validation=dict(superposition=check,combination=factors,diaphragmError=diaphragm_error,
        totals=totals,sourceSha256=hashlib.sha256(source.read_bytes()).hexdigest(),
        counts=dict(nodes=len(model['nodes']),elements=len(model['elements']),diaphragms=len(model['diaphragms'])),
        checksPassed=True,software='OpenSeesPy',academicAssumptions=True)
    bundle=dict(schemaVersion=1,generated=datetime.now(timezone.utc).isoformat(),units='kN-m-s',
        title='Laboratorio estructural LT1 + LT2',gap=c['gap_m'],model=model,cases=list(base.values()),
        capacities=capacities,loadRows=tables['CasosCargaViga'],validation=validation,notes=notes,
        config=c,sourceWorkbook=str(source),pythonExecutable=sys.executable,projectRoot=str(ROOT))
    out=ROOT/'resultados';out.mkdir(exist_ok=True)
    write_json(out/'proyecto.json',bundle)
    write_json(out/'validacion.json',validation)
    write_json(out/'cambios_geometria.json',model['changes'])
    write_json(out/'config_utilizada.json',c)
    write_json(out/'apoyos_columnas.json',[r for r in model['changes'] if r['action']=='fix_column_base'])
    streaming=ROOT/'Unity'/'Assets'/'StreamingAssets'
    write_json(streaming/'proyecto.json',bundle)
    # An already built application can reload the same verified result.
    for dest in (ROOT/'Aplicacion').glob('*_Data/StreamingAssets') if (ROOT/'Aplicacion').exists() else []:
        write_json(dest/'proyecto.json',bundle)
    report=['# Resultados del laboratorio','',f'Generado: {bundle["generated"]}',
        f'OpenSees: {len(model["nodes"])} nodos analíticos, {len(model["elements"])} elementos, diez diafragmas independientes.','',
        '| Caso | Máx. traslación nodal (mm) | Error relativo fuerzas | Error relativo momentos |',
        '|---|---:|---:|---:|']
    for k,r in base.items():report.append(f'| {k} | {r["maxDisplacement_m"]*1000:.4f} | {r["forceResidual"]:.3e} | {r["momentResidual"]:.3e} |')
    report+=['','Superposición contrastada con una corrida explícita: `1,2 G + 0,5 Q + 0,8 EX − 0,3 EY`.',
        f'Error relativo desplazamientos: {check["displacements"]["relative"]:.3e}. Error relativo esfuerzos: {check["forces"]["relative"]:.3e}.',
        f'Error máximo de compatibilidad de diafragmas: {diaphragm_error:.3e}.',
        '',f'Gravedad de pisos: {totals["G_floor"]:.3f} kN. Peso propio de miembros: {totals["G_members"]:.3f} kN. Carga viva: {totals["Q_floor"]:.3f} kN.',
        '', '## Alcance e hipótesis','']+['- '+n for n in notes]
    report+=['','## Secciones de fibras','']
    for cap in capacities:report.append(f'- {cap["name"]}: {len(cap["pm"])} puntos P-M y {len(cap["mphi"])} puntos M-phi. Error frente a compresión uniforme independiente: {cap["checkCompression"]["relative"]:.3e}.')
    report+=['','El comparador de bloque rectangular Whitney usa otra ley constitutiva y se presenta como referencia independiente, no como igualdad esperada con Concrete01. La demanda-capacidad mostrada es axial-flexural uniaxial; excluye corte, inestabilidad, adherencia, pandeo de barras y flexión biaxial.']
    (out/'INFORME.md').write_text('\n'.join(report),encoding='utf8')
    plot_results(bundle,out)
    print('RESULTADOS:',out,flush=True)
    return bundle

def plot_results(bundle,out):
    import matplotlib
    matplotlib.use('Agg')
    import matplotlib.pyplot as plt
    from mpl_toolkits.mplot3d.art3d import Line3DCollection
    m=bundle['model'];nodes={n['id']:n for n in m['nodes']}
    fig=plt.figure(figsize=(14,8));ax=fig.add_subplot(111,projection='3d')
    for group,color in [('LT1','#147d92'),('LT2','#c87522')]:
        segments=[]
        for e in m['elements']:
            if e['building']!=group or e['kind']=='RIGID_ARM':continue
            segments.append([[nodes[t][k] for k in ('x','y','z')] for t in (e['i'],e['j'])])
        ax.add_collection3d(Line3DCollection(segments,colors=color,linewidths=.7,label=group))
    ax.set(xlim=(-34,53),ylim=(-6,19),zlim=(0,21),xlabel='X (m)',ylabel='Y (m)',zlabel='Z (m)')
    ax.set_box_aspect((87,25,21));ax.view_init(22,-63);ax.legend();ax.set_title('Modelo analítico: pórticos y muros equivalentes · junta abierta de 10 cm')
    fig.tight_layout();fig.savefig(out/'modelo_3d.png',dpi=160);plt.close(fig)
    fig,axs=plt.subplots(2,2,figsize=(12,8))
    for row,cap in enumerate(bundle['capacities']):
        for field,label,style in [('pm','Fibras OpenSees','-'),('whitney','Bloque Whitney','--')]:
            pts=cap[field];axs[row,0].plot([p['M'] for p in pts],[p['P'] for p in pts],style,label=label)
        axs[row,0].set(xlabel='M nominal (kN m)',ylabel='P compresión (kN)',title=cap['name']);axs[row,0].legend()
        pts=cap['mphi'];axs[row,1].plot([p['curvature'] for p in pts],[p['moment'] for p in pts]);axs[row,1].set(xlabel='Curvatura (1/m)',ylabel='M (kN m)',title='M-phi · axial constante = 10% compresión máxima')
    for ax in axs.flat:ax.grid(alpha=.2)
    fig.suptitle('Armaduras didácticas — reemplazar por armado real antes de interpretar capacidad del edificio')
    fig.tight_layout();fig.savefig(out/'secciones_fibras.png',dpi=150);plt.close(fig)

if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--overrides');args=parser.parse_args()
    run(args.overrides)
