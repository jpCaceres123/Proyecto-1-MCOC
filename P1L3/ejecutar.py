"""Ejecutar: python P1L3/ejecutar.py [--parametros archivo.json]."""
from pathlib import Path
import argparse
import csv
import hashlib
import importlib.metadata
import json
import platform
import shutil
import sys
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
import numpy as np
import casos
import capacidad
from exportar_reparto_losas import export_repartition
from exportar_aceleraciones import export_accelerations

ROOT=Path(__file__).resolve().parent


def rows(path):
    with path.open(encoding='utf-8-sig') as f:
        return list(csv.DictReader(f))


def table(headers, data):
    return '\n'.join(['| '+' | '.join(headers)+' |',
                      '| '+' | '.join(['---']*len(headers))+' |']+
                     ['| '+' | '.join(str(v) for v in row)+' |' for row in data])


def export_unity(cfg, global_results, capacity_results, out):
    """Exporta CSV simples que Unity puede leer con TextAsset/Resources."""
    resources=ROOT.parent/'P1L2'/'UnityVisualization'/'Assets'/'Resources'
    resources.mkdir(parents=True,exist_ok=True)
    model=json.loads(casos.base.MODEL.read_text(encoding='utf-8'))
    visible_nodes={int(n['id']) for n in model['nodes']}
    displacement_rows=[]
    floor_rows=[]
    member_rows=[]
    for case in ('G','Q','EX','EY','R','EXG','EXQ','EYG','EYQ'):
        local=json.loads((out/f'{case}_fuerzas_locales.json').read_text(encoding='utf-8'))
        for tag, values in local.items():
            if len(values) != 12:
                raise ValueError(f'Barra {tag}: se esperaban 12 fuerzas locales')
            member_rows.append(dict(caso=case,elemento=int(tag),**dict(zip(
                ('Ni_kN','Vyi_kN','Vzi_kN','Ti_kNm','Myi_kNm','Mzi_kNm',
                 'Nj_kN','Vyj_kN','Vzj_kN','Tj_kNm','Myj_kNm','Mzj_kNm'),values))))
        result=np.load(out/f'{case}.npz')
        for tag,disp in zip(result['node_tags'],result['u']):
            if int(tag) in visible_nodes:
                displacement_rows.append(dict(caso=case,nodo=int(tag),
                    ux_m=disp[0],uy_m=disp[1],uz_m=disp[2],rx_rad=disp[3],ry_rad=disp[4],rz_rad=disp[5]))
        response=rows(out/f'{case}_pisos.csv')
        source={(f['bloque'],round(float(f['z_m']),6)):f for f in global_results['floors']}
        for r in response:
            key=(r['bloque'],round(float(r['z_m']),6)); f=source[key]
            force=f['F_kN'] if case in ('EX','EY') else 0.0
            floor_rows.append(dict(caso=case,bloque=r['bloque'],z_m=r['z_m'],
                cm_x_m=f['CM_x_m'],cm_y_m=f['CM_y_m'],fuerza_kN=force,
                ux_cm_m=r['ux_CM_m'],uy_cm_m=r['uy_CM_m'],rz_rad=r['giro_z_rad'],
                ux_master_m=r['ux_master_m'],uy_master_m=r['uy_master_m'],
                master_x_m=r['master_x_m'],master_y_m=r['master_y_m']))
    casos.dump_csv(resources/'semana3_desplazamientos.csv',displacement_rows)
    casos.dump_csv(resources/'semana3_pisos.csv',floor_rows)
    casos.dump_csv(resources/'semana3_masa_componentes.csv',[
        {k:f[k] for k in ('bloque','z_m','G_kN','Q_kN','Gx_kNm','Gy_kNm','Qx_kNm','Qy_kNm','a_g')}
        for f in global_results['floors']])
    export_accelerations(out/'masas_y_sismo.csv',resources/'semana3_aceleraciones.csv',cfg['g_m_s2'])
    casos.dump_csv(resources/'semana3_esfuerzos_locales.csv',member_rows)
    # Peso propio geométrico por losa; G/Q coinciden con los receptores del análisis.
    # Incluir vigas y muros evita omitir las cargas transferidas a bordes de muro.
    transfers={s['id']:[] for s in model['slabs']}
    for kind in ('beam','wall'):
        for row in model[f'{kind}_load_cases']:
            transfers[row['slab_id']].append(row)
    slab_rows=[]
    for slab in model['slabs']:
        area=slab['area_m2']  # Área neta: los vacíos ya están descontados.
        mass=area*slab['thickness_m']*slab['density_kg_m3']
        weight=mass*9.80665/1000  # Misma gravedad usada por el generador de losas.
        loads=transfers[slab['id']]
        dead=sum(row['dead_load_kN'] for row in loads)
        live=sum(row['tributary_area_m2']*(row['q_SC_kN_m2'] if cfg['q_Q_kN_m2'] is None
                 else cfg['q_Q_kN_m2']) for row in loads)
        slab_rows.append(dict(losa_id=slab['id'],area_neta_m2=area,espesor_m=slab['thickness_m'],
            densidad_kg_m3=slab['density_kg_m3'],masa_propia_kg=mass,peso_propio_kN=weight,
            G_losa_kN=dead,adicional_G_kN=dead-weight,Q_losa_kN=live))
    casos.dump_csv(resources/'semana3_pesos_losas.csv',slab_rows)
    export_repartition(model,cfg,resources/'semana3_reparto_losas.csv')
    # Curvas constitutivas monotónicas de los materiales de la sección Fiber.
    # No representan una historia de fibras del edificio global elástico.
    cc=cfg['columna']
    concrete_eps=np.linspace(-cc['eps_cu'],0,181)
    concrete_eps=np.unique(np.append(concrete_eps,-cc['eps_c0']))
    yield_eps=cc['fy_MPa']/cc['Es_MPa']
    steel_eps=np.unique(np.concatenate((np.linspace(-2*yield_eps,2*yield_eps,181),[-yield_eps,0,yield_eps])))
    def constitutive(eps,mat):
        return [dict(strain=float(e),stress_MPa=float(s/1000))
                for e,s in zip(eps,capacidad.stress(eps,mat,cc))]
    # Asignar la referencia por las dimensiones declaradas, no por las inercias
    # aproximadas del análisis global. My/Mz comparten esta sección cuadrada simétrica.
    geometry=json.loads(casos.verification.GEOMETRY.read_text(encoding='utf-8'))
    dimensions=geometry['column_section_m']
    section_matches=(abs(cc['b_m']-cc['h_m'])<1e-9
        and abs(dimensions[0]-cc['b_m'])<1e-9 and abs(dimensions[1]-cc['h_m'])<1e-9
        and abs(model['section_columns']['A_m2']-cc['b_m']*cc['h_m'])<1e-9)
    graphs=dict(b_m=cc['b_m'],h_m=cc['h_m'],fc_MPa=cc['fc_MPa'],fy_MPa=cc['fy_MPa'],
        members=[dict(id=e['id'],type=e['type'],has_capacity=e['type']=='COLUMN' and section_matches)
                 for e in model['elements'] if e['type']!='WALL'],
        pm=[dict(p=float(p['P_kN']),m=float(p['M_kNm'])) for p in capacity_results['puntos']],
        concrete=constitutive(concrete_eps,1),steel=constitutive(steel_eps,2))
    (resources/'semana3_graficos_seccion.json').write_text(json.dumps(graphs,indent=2),encoding='utf-8')
    for source,target in ((out/'fibras.csv','semana3_fibras.csv'),
                          (out/'momento_curvatura.csv','semana3_momento_curvatura.csv'),
                          (out/'PM_puntos.csv','semana3_pm.csv')):
        shutil.copy2(source,resources/target)
    meta=[dict(clave='aceleracion_fraccion_g',valor=cfg['aceleracion_fraccion_g'],unidad='g'),
          dict(clave='fraccion_Q_masa',valor=cfg['fraccion_Q_masa'],unidad=''),
          dict(clave='ponderador_G_masa',valor=cfg.get('ponderador_G_masa',1.),unidad=''),
          dict(clave='g_m_s2',valor=cfg['g_m_s2'],unidad=''),
          dict(clave='corte_total_kN',valor=sum(f['F_kN'] for f in global_results['floors']),unidad='kN'),
          dict(clave='combinacion_G',valor=cfg['combinacion']['G'],unidad=''),
          dict(clave='combinacion_Q',valor=cfg['combinacion']['Q'],unidad=''),
          dict(clave='combinacion_EX',valor=cfg['combinacion']['EX'],unidad=''),
          dict(clave='combinacion_EY',valor=cfg['combinacion']['EY'],unidad=''),
          dict(clave='fc_MPa',valor=cfg['columna']['fc_MPa'],unidad='MPa'),
          dict(clave='fy_MPa',valor=cfg['columna']['fy_MPa'],unidad='MPa'),
          dict(clave='As_mm2',valor=capacity_results['As_m2']*1e6,unidad='mm2')]
    casos.dump_csv(resources/'semana3_resumen.csv',meta)


def report(cfg,g,c,out):
    fig,axes=plt.subplots(1,3,figsize=(14,5),layout='constrained')
    for case,ax,dof in (('EX',axes[0],'ux_CM_m'),('EY',axes[1],'uy_CM_m')):
        data=rows(out/f'{case}_pisos.csv')
        for block in ('LT1','LT2'):
            selected=[r for r in data if r['bloque']==block]
            ax.plot([float(r[dof])*1000 for r in selected],[float(r['z_m']) for r in selected],'o-',label=block)
        ax.set(title=f'{case}: desplazamiento del CM',xlabel='Desplazamiento [mm]',ylabel='Cota Z [m]')
        ax.legend(); ax.grid(alpha=.25)
    for case in ('EX','EY'):
        data=rows(out/f'{case}_pisos.csv')
        for block in ('LT1','LT2'):
            selected=[r for r in data if r['bloque']==block]
            axes[2].plot([float(r['giro_z_rad'])*1000 for r in selected],[float(r['z_m']) for r in selected],'o-',label=f'{case} {block}')
    axes[2].set(title='Torsion de piso',xlabel='Giro Z [mrad]',ylabel='Cota Z [m]')
    axes[2].legend(); axes[2].grid(alpha=.25)
    fig.savefig(out/'respuesta_sismica.png',dpi=170); plt.close(fig)
    qt=table(['Cota [m]','Área origen [m²]','Q origen [kN]','Q transferida [kN]','Error [kN]'],
             [[f'{float(r[k]):.6f}' for k in ('z_m','area_origen_m2','Q_origen_kN','Q_transferida_kN','diferencia_kN')] for r in rows(out/'conservacion_Q.csv')])
    ft=table(['Bloque','Z [m]','Masa [t]','CM X [m]','CM Y [m]','F [kN]'],
             [[f['bloque']]+[f'{f[k]:.3f}' for k in ('z_m','masa_t','CM_x_m','CM_y_m','F_kN')] for f in g['floors']])
    st=table(['Respuesta','Muestra','Superpuesta','Explícita','Error relativo máximo'],
             [[r['respuesta'],r['identificador']]+[f'{float(r[k]):.9g}' for k in ('superpuesta','explicita','error_relativo_max')] for r in rows(out/'comparacion_superposicion.csv')])
    pt=table(['P [kN] (+ compresión)','M máximo [kN·m]','φ al máximo [1/m]'],
             [[f'{r[k]:.6f}' for k in ('P_kN','M_kNm','phi_1_m')] for r in c['puntos']])
    vt=table(['Control','Error','Tolerancia','Estado'],
             [[r['control'],f'{r["error"]:.3e}',f'{r["tolerancia"]:.3e}',r['estado']] for r in g['checks']])
    total=sum(f['F_kN'] for f in g['floors'])
    cc=cfg['columna']
    face_diameters=cc.get('diametros_por_cara_m',[cc['diametro_m']]*cc['barras_por_cara'])
    diameter_counts={}
    for diameter in face_diameters:
        diameter_counts[diameter]=diameter_counts.get(diameter,0)+2
    for diameter in face_diameters[1:-1]:
        diameter_counts[diameter]=diameter_counts.get(diameter,0)+2
    reinforcement_description=' + '.join(
        f'{count} barras Ø{diameter*1000:.0f} mm'
        for diameter,count in sorted(diameter_counts.items()))
    state='OK' if c['estado']=='OK' and all(r['estado']=='OK' for r in g['checks']) else 'REVISAR'
    text=f'''# Semana 3 — carga viva, sismo, superposición y capacidad HA

**Estado de controles numéricos: {state}.** El modelo conserva la geometría y las
restricciones de Semana 2. La armadura de los pilares se tomó del detalle de
pilar 2 P.70x70 entregado: {reinforcement_description} y estribos Ø12@10 cm.

## Alcance y parámetros

Unidades: kN, m, s; masas en toneladas (kN·s²/m), momentos en kN·m y giros en radianes.
Se reutilizan barras `elasticBeamColumn`, muros `ShellMITC4`, ejes locales y
diafragmas independientes LT1/LT2. No se introducen P–Delta ni materiales
no lineales en el edificio: esa linealidad permite superponer sus respuestas.
La Fiber Section no lineal se analiza por separado.

Cuatro `elasticBeamColumn` de acero SHS 300×300×20 conectan las puntas alineadas
de los voladizos: dos junto a F (`X=10,00` y `17,49 m`) entre Z=7,92 y
11,88 m; G y H entre Z=15,84 y 19,80 m.
Se adoptan E=200 GPa y ν=0,30. Son elementos elásticos con uniones rígidas en
los nodos de punta; no se comprueba aquí pandeo local, global ni conexiones.

La aceleración adoptada es {cfg['aceleracion_fraccion_g']:.0%} de g y la fracción de Q en
la masa es {cfg['fraccion_Q_masa']:.0%}, según la indicación recibida para este laboratorio.
Se adopta aceleración uniforme por defecto. `aceleracion_por_piso_g` permite
sobrescribirla por número de piso (por ejemplo, `{{"2": 0.15}}` aplica 0,15 g
al segundo nivel elevado). Se usa el mismo perfil en EX y EY como casos independientes.
El enunciado no prescribe una distribución triangular con la altura.
Es un patrón académico editable;
este cálculo no constituye una aplicación completa de NCh433 ni incluye R,
espectro, suelo, importancia o combinaciones normativas.

## A. Carga viva

`q_Q_kN_m2 = null` conserva las intensidades por zona de Semana 2. Un número
en ese campo aplica una intensidad uniforme a las mismas áreas cargadas.
Con zonas distintas se verifica Σ(q_Q,j A_j); con intensidad uniforme, q_Q A.
Se contrastan las cargas transferidas con las áreas de las zonas originales
mediante el verificador geométrico de Semana 2, independientemente de la suma
de receptores. La tolerancia es 0,002 kN por piso por redondeo del contrato.

{qt}

`transferencia_Q.csv` identifica cada losa, receptor (viga o muro), área e intensidad.
Las resultantes de losa se reparten entre los nodos extremos del receptor,
tal como en Semana 2. Se conserva la fuerza total, pero no se reproduce el
diagrama de flexión local de una viga bajo carga distribuida: los esfuerzos
gravitacionales deben interpretarse dentro de esta idealización.

## B. Casos EX y EY

Para cada bloque y piso: W_i={cfg.get('ponderador_G_masa',1.)}G_i+{cfg['fraccion_Q_masa']}Q_i, m_i=W_i/g,
a_i=α_i g y F_i=m_i a_i. El valor por defecto es α={cfg['aceleracion_fraccion_g']}.
La rutina `sismo.py` recalcula masa, centro de masa y fuerza para cada piso.
`sismo_por_piso.csv` presenta los totales por nivel; `masas_y_sismo.csv`
los separa por bloque. `auditoria_masas_piso.csv` permite reconstruir el peso
y los primeros momentos a partir de cada aporte nodal. Se rechazan nodos
contados en dos pisos o pesos elevados que no pertenecen a ningún piso.
G contiene la carga permanente de losa/terminaciones, peso propio de muros y,
como adición documentada respecto de Semana 2, peso propio de vigas y columnas.
Para HA se usa γ={cfg['peso_especifico_HA_kN_m3']:.6f} kN/m³; las cuatro columnas
SHS 300×300×20 usan ρ=7850 kg/m³. Se usan sus volúmenes brutos,
sin descontar intersecciones entre elementos. Las barras aportan la mitad a
cada extremo; los paneles de muro, un cuarto a cada nodo. El peso ubicado en
la base Z=0 ({g['base_weight_excluded_kN']:.3f} kN incluyendo la fracción de Q)
no recibe aceleración de piso. No se usan las masas arbitrarias heredadas.

El CM es el centro de la masa discretizada de cada diafragma. Esta aproximación
usa las resultantes nodales de Semana 2; no calcula el centroide exacto de todos
los polígonos de carga. En el nodo maestro se aplica F más el par de transporte:
EX: Mz=−Fx(yCM−ym); EY: Mz=Fy(xCM−xm). Equivale estáticamente a cargar el CM.
No se añade excentricidad accidental. Los giros calculados se exportan por piso.

{ft}

Carga lateral total en EX y en EY: **{total:.3f} kN**.
Corte de apoyos en EX: **{-g['equilibrium']['EX']['supports'][0]:.3f} kN**;
en EY: **{-g['equilibrium']['EY']['supports'][1]:.3f} kN**.
El corte se define como la suma de reacciones externas de todos los apoyos,
incluidos los situados sobre Z=0. No es un corte exclusivo de la sección Z=0.

![Desplazamientos y giros](results/respuesta_sismica.png)

**Cambio solicitado en el voladizo del eje J:** se liberaron sus tres nodos
inferiores en X=50,00 m, Z=15,84 m (Y=0,00; 7,25; 16,15 m).
Se mantienen las columnas y conexiones del voladizo; se retiran las seis
restricciones externas de cada nodo. Las respuestas se recalculan con estos apoyos.

**Condición heredada que requiere contraste con planos:** hay empotramientos en
las cotas {g['support_heights_m']} m. Los diafragmas incluyen nodos apoyados:
esto explica desplazamientos muy pequeños de algunos pisos y puede inhibir
la torsión. Los vínculos `equalDOF` de muros conectan nodos incluso con separación
geométrica; no equivalen a un brazo rígido con todas sus relaciones de giro.
Se conservan para no alterar silenciosamente el modelo recibido. Pasar los
controles de equilibrio y superposición no valida estas condiciones físicas.

Se emplea `Penalty` con α={cfg['penalty']:.1e}. En nodos que también participan en
restricciones multipunto, `nodeReaction` es el residuo Ku−P y no representa
por sí solo la reacción externa. Para cada DOF apoyado se obtiene R=−αu;
se verifica su equilibrio con las fuerzas aplicadas y sensibilidad con α×10.
`*.npz` distingue `nodal_residual` de `reaction` en los apoyos.

## C. Superposición

Combinación de demostración: **R={cfg['combinacion']['G']}G + {cfg['combinacion']['Q']}Q
+ {cfg['combinacion']['EX']}EX + ({cfg['combinacion']['EY']})EY**.
Cada caso parte de `wipe()` y de la misma rigidez. Se eliminan los patrones
automáticos G+Q antes de cargar. R se resuelve nuevamente con la suma explícita
de cargas; no se obtiene del resultado superpuesto para efectuar la comparación.

{st}

La comparación abarca todos los DOF, todos los apoyos y todas las componentes
de fuerzas nodales resistentes de barras y shells, con tags ordenados.
La tabla muestra una componente de máxima magnitud de cada familia; el error
relativo usa la norma máxima de toda la familia. Fuerzas `eleForce` en ejes
globales: componentes traslacionales en kN y rotacionales en kN·m.
En desplazamientos: DOF 1–3 en m; 4–6 en rad. Los archivos completos permiten
reconstruir cualquier combinación posterior, dentro de la hipótesis lineal.

## D. Columna de hormigón armado

Sección {cc['b_m']:.2f} × {cc['h_m']:.2f} m, compatible con A=0,49 m² del modelo.
**Datos tomados del detalle de pilares:** f'c={cc['fc_MPa']} MPa,
fy={cc['fy_MPa']} MPa, Es={cc['Es_MPa']} MPa;
{4*cc['barras_por_cara']-4} barras longitudinales ({reinforcement_description});
distancia cara–centro de barra {cc['recubrimiento_al_centro_barra_m']*1000:.0f} mm;
estribos Ø{cc['estribo_diametro_m']*1000:.0f} @ {cc['estribo_spacing_m']*100:.0f} cm.
As={c['As_m2']*1e6:.1f} mm²; cuantía={c['rho']:.3%}.

`Concrete01`: compresión negativa, pico −f'c a −{cc['eps_c0']}, resistencia
residual nula a −{cc['eps_cu']}, sin tracción. Para mantener la hipótesis
académica de esta curva, los estribos conocidos se reportan pero no se modela
confinamiento constitutivo adicional.
`Steel01`: elastoplástico perfecto, b=0. La discretización parte de una malla
{cc['fibras_por_lado']} × {cc['fibras_por_lado']}; descuenta el área ocupada por
las barras de las celdas vecinas e incorpora fibras de acero separadas.
Total de fibras activas: {c['n_fibras']}. Error Ac+As−Ag: {c['error_area_m2']:.2e} m².

La sección se instancia en OpenSees como `Fiber` y `zeroLengthSection`.
Se aplica P constante y luego curvatura creciente con `DisplacementControl`.
La curva se detiene al cruzar εc=−{cc['eps_cu']} en la cara extrema o
|εs|={cc['eps_s_lim']}; el último paso puede exceder levemente el límite y
se excluye al elegir la capacidad. Se controla equilibrio axial en cada paso.

![Discretización, M–φ y P–M](results/capacidad_HA.png)

{pt}

Los tres puntos de flexocompresión son los máximos M de las curvas OpenSees
antes de alcanzar el límite de deformación. El punto M=0 es el pico de
compresión uniforme de las mismas fibras, a ε=−{cc['eps_c0']}; por ello no es
la fórmula normativa 0,85f'cAc+fyAs. La línea discontinua solo conecta cuatro
muestras, no define una envolvente completa ni permite diseñar por interpolación.

Interpretación: la compresión moderada aumenta el momento máximo respecto de
P=0, pero reduce la curvatura que puede alcanzarse. Las ramas descendentes
muestran degradación del hormigón. Al acercarse a compresión pura la capacidad
de momento tiende a cero. Son capacidades nominales del modelo de sección:
no incluyen factores de reducción, confinamiento, pandeo de barras, cortante,
esbeltez de columna ni comprobación demanda/capacidad del edificio.

Como comprobación adicional se integra la envolvente de materiales por
compatibilidad de deformaciones (`PM_compatibilidad_envolvente_material.csv`).
Sus valores al límite de deformación no deben confundirse con los picos de
M–φ: Concrete01 considera descarga/recarga en la historia de precarga y flexión.
Al duplicar las divisiones de la malla, el cambio máximo de ese cálculo es
{c['error_malla_relativo']:.3%}. Al refinar malla y paso de curvatura, el cambio
máximo de los picos OpenSees es {c['error_pico_refinado_relativo']:.3%}.
Error axial máximo: {c['error_axial_kN']:.3e} kN. Estado HA: **{c['estado']}**.

## Controles automáticos

{vt}

El comando termina con código distinto de cero si cualquier control resulta
REVISAR. Los supuestos físicos pendientes se mantienen visibles aunque los
controles numéricos estén OK. `manifest.json` registra hashes de entradas y
versiones del entorno para poder identificar la corrida.

## Integración con Unity

El visor de Semana 3 se integró en el proyecto existente
`P1L2/UnityVisualization`; no se creó una segunda escena. El panel izquierdo
mantiene la inspección de geometría y áreas tributarias de Semana 2. El panel
derecho permite seleccionar G, Q, EX, EY o R, ajustar la escala de la deformada,
mostrar fuerzas laterales y centros de masa y consultar dentro de Unity la
discretización Fiber, las curvas M–φ y los primeros puntos P–M.

En `Ponderadores de masa sísmica` se editan αG y αQ (valores iniciales
`ponderador_G_masa` y `fraccion_Q_masa`). `Aplicar masa` actualiza masas,
centros de masa, fuerzas, deformadas y esfuerzos EX/EY; también reconstruye R
con sus λ ya aplicados. No modifica las cargas gravitacionales G/Q.
Las bases EXG/EXQ/EYG/EYQ son corridas OpenSees independientes; sus respuestas
se suman gracias a la rigidez estática lineal. Se contrastan desplazamientos,
reacciones y fuerzas internas con corridas explícitas de ponderadores distintos.
Se conserva la aceleración especificada: cambia F, no a. Los valores de Unity
duran la sesión de Play; para cambiar los valores iniciales, editar parámetros
y regenerar. No se admiten ponderadores negativos ni pisos de masa nula.

`P1L3/ejecutar.py` exporta cada corrida a
`P1L2/UnityVisualization/Assets/Resources/semana3_*.csv`. Las líneas coloreadas
son la estructura deformada y se superponen a la geometría original. Los
archivos de respuesta completos siguen disponibles en `P1L3/results/`.

Abrir `P1L2/UnityVisualization/Assets/Main.unity`. Para reconstruir el ejecutable,
usar `Build > Edificio Viewer > Construir EXE`. El resultado queda en
`P1L2/UnityVisualization/Build/EdificioViewer.exe`.

## Reproducción y archivos

Desde la raíz del proyecto, ejecutar:

```powershell
python -m pip install -r P1L3/requirements.txt
python P1L3/ejecutar.py
```

Editar `P1L3/parametros.json` y volver a ejecutar para cambiar q_Q, aceleración,
fracción de Q, combinación o sección. También se admite `--parametros archivo.json`.
Los resultados se regeneran en `P1L3/results/`, este informe en
`P1L3/INFORME.md` y los recursos del visor dentro del Unity existente de P1L2.
Se necesita P1L2 al lado de P1L3. El análisis numérico puede ejecutarse sin
abrir Unity; para visualizar cambios hay que volver a abrir o reconstruir el visor.

Para la demostración: mostrar primero conservación de Q; luego masas, CM,
fuerzas y giros; comparar R con la suma de casos; por último explicar la malla
de fibras, los materiales y los cuatro puntos P–M. Antes de presentar como
modelo validado del edificio, contrastar apoyos y armadura con los planos.

## Referencia de implementación

La configuración `Fiber`/`zeroLengthSection` y el control de curvatura siguen
el procedimiento documentado en [OpenSeesPy: Moment Curvature Analysis](https://openseespydoc.readthedocs.io/en/latest/src/MomentCurvature.html).
La geometría, las cargas y las restricciones provienen de los archivos locales
de P1L2 identificados en el manifiesto.
'''
    (ROOT/'INFORME.md').write_text(text,encoding='utf-8')
    return state


def main():
    parser=argparse.ArgumentParser()
    parser.add_argument('--parametros',type=Path,default=ROOT/'parametros.json')
    args=parser.parse_args()
    cfg=json.loads(args.parametros.read_text(encoding='utf-8'))
    out=ROOT/'results'; out.mkdir(exist_ok=True)
    g=casos.run(cfg,out)
    c=capacidad.run(cfg['columna'],out)
    export_unity(cfg,g,c,out)
    sources=[args.parametros,casos.base.MODEL,casos.verification.LOADS,
             casos.verification.GEOMETRY,Path(casos.base.__file__),Path(casos.verification.__file__),
             ROOT/'casos.py',ROOT/'sismo.py',ROOT/'capacidad.py',
             ROOT/'exportar_aceleraciones.py',ROOT/'exportar_reparto_losas.py',Path(__file__)]
    manifest=dict(python=platform.python_version(),
                  versions={p:importlib.metadata.version(p) for p in ('openseespy','numpy','matplotlib')},
                  inputs={str(p.relative_to(ROOT.parent)) if p.is_relative_to(ROOT.parent) else str(p):
                          hashlib.sha256(p.read_bytes()).hexdigest() for p in sources},parametros=cfg)
    (out/'manifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')
    state=report(cfg,g,c,out)
    print(f'Semana 3: {state}. Informe: {ROOT / "INFORME.md"}')
    return 0 if state=='OK' else 1


if __name__=='__main__':
    sys.exit(main())
