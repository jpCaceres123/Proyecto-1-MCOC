"""Seccion Fiber OpenSees y envolvente P-M nominal por compatibilidad.

Hipotesis academica: hormigon no confinado Concrete01 sin traccion, acero
Steel01 elastoplastico perfecto. Sin factores de reduccion ni chequeo normativo.
"""
import json
import math
import numpy as np
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
import openseespy.opensees as ops
from casos import dump_csv


def fibers(c, n=None):
    n = n or c['fibras_por_lado']
    b,h = c['b_m'],c['h_m']
    a = c['recubrimiento_al_centro_barra_m']
    nb = c['barras_por_cara']
    if n < 8 or nb < 2 or not 0 < a < min(b,h)/2:
        raise ValueError('Discretizacion o refuerzo invalido')
    bars = []
    for y in np.linspace(-h/2+a,h/2-a,nb):
        bars.extend([(y,-b/2+a),(y,b/2-a)])
    for z in np.linspace(-b/2+a,b/2-a,nb)[1:-1]:
        bars.extend([(-h/2+a,z),(h/2-a,z)])
    As = math.pi*c['diametro_m']**2/4
    cells = [[-h/2+(iy+0.5)*h/n, -b/2+(iz+0.5)*b/n, b*h/n**2, 1]
             for iy in range(n) for iz in range(n)]
    # Descontar acero del hormigon evita contar dos veces el area ocupada.
    # Si la barra supera el area de una celda, repartir la sustraccion entre
    # las celdas mas cercanas; conserva exactamente Ac + As = Ag.
    for y,z in bars:
        remaining = As
        for i in sorted(range(len(cells)),key=lambda i:(cells[i][0]-y)**2+(cells[i][1]-z)**2):
            take = min(remaining, cells[i][2])
            cells[i][2] -= take
            remaining -= take
            if remaining < 1e-14:
                break
    return np.array([cell for cell in cells if cell[2]>1e-14]+[[y,z,As,2] for y,z in bars])


def stress(eps, mat, c):
    fc,fy,Es = c['fc_MPa']*1000,c['fy_MPa']*1000,c['Es_MPa']*1000
    comp = -eps
    ratio = comp/c['eps_c0']
    concrete = np.where(comp <= 0,0,np.where(comp <= c['eps_c0'],
               -fc*(2*ratio-ratio**2),
               -fc*np.maximum(0,(c['eps_cu']-comp)/(c['eps_cu']-c['eps_c0']))))
    return np.where(mat==1,concrete,np.clip(Es*eps,-fy,fy))


def resultant(f, eps0, phi, c):
    strain = eps0 - phi*f[:,0]
    force = stress(strain,f[:,3],c)*f[:,2]
    return -force.sum(), -(force*f[:,0]).sum()


def ultimate(f, neutral_axis, c):
    # Limites en cara extrema de hormigon y barra extrema traccionada.
    h=c['h_m']; depth=h/2-f[f[:,3]==2,0].min()
    phi=min(c['eps_cu']/neutral_axis,
            c['eps_s_lim']/max(depth-neutral_axis,1e-20))
    eps0=phi*(h/2-neutral_axis)
    p,m=resultant(f,eps0,phi,c)
    return dict(P_kN=float(p), M_kNm=float(m), phi_1_m=float(phi),
                eps0=float(eps0), c_m=float(neutral_axis))


def root_p(f, target, c):
    lo,hi=1e-5,c['h_m']
    # Seleccionar primera raiz desde flexion, antes de rama de compresion pura.
    grid=np.geomspace(lo,hi,800)
    for a,b in zip(grid[:-1],grid[1:]):
        if (ultimate(f,a,c)['P_kN']-target)*(ultimate(f,b,c)['P_kN']-target)<=0:
            lo,hi=a,b
            break
    else:
        raise RuntimeError(f'No se encontro equilibrio P={target}')
    for _ in range(70):
        mid=(lo+hi)/2
        if (ultimate(f,lo,c)['P_kN']-target)*(ultimate(f,mid,c)['P_kN']-target)<=0:
            hi=mid
        else:
            lo=mid
    return ultimate(f,(lo+hi)/2,c)


def moment_curvature(f, P, c):
    ops.wipe()
    ops.model('basic','-ndm',2,'-ndf',3)
    ops.node(1,0,0); ops.node(2,0,0)
    ops.fix(1,1,1,1); ops.fix(2,0,1,0)
    ops.uniaxialMaterial('Concrete01',1,-c['fc_MPa']*1000,-c['eps_c0'],0,-c['eps_cu'])
    ops.uniaxialMaterial('Steel01',2,c['fy_MPa']*1000,c['Es_MPa']*1000,0)
    ops.section('Fiber',1)
    for y,z,area,mat in f:
        ops.fiber(float(y),float(z),float(area),int(mat))
    ops.element('zeroLengthSection',1,1,2,1)
    ops.timeSeries('Linear',1); ops.pattern('Plain',1,1)
    ops.load(2,-P,0,0)
    ops.constraints('Plain'); ops.numberer('Plain'); ops.system('BandGeneral')
    ops.test('NormUnbalance',1e-7,100); ops.algorithm('Newton')
    ops.integrator('LoadControl',0.05); ops.analysis('Static')
    if ops.analyze(20):
        raise RuntimeError('No converge precarga axial de Fiber Section')
    ops.loadConst('-time',0)
    ops.timeSeries('Linear',2); ops.pattern('Plain',2,2); ops.load(2,0,0,1)
    step=c['phi_max_1_m']/c['pasos_curvatura']
    rows=[]
    reason='phi_max alcanzada'
    for i in range(c['pasos_curvatura']+1):
        eps0=ops.nodeDisp(2,1); phi=ops.nodeDisp(2,3)
        strains=eps0-phi*f[:,0]
        # Control en cara extrema, no solamente en centro de fibra.
        ec=min(eps0-phi*c['h_m']/2,eps0+phi*c['h_m']/2)
        es=float(np.max(np.abs(strains[f[:,3]==2])))
        forces=ops.eleResponse(1,'section','force')
        rows.append(dict(P_objetivo_kN=P,phi_1_m=phi,M_kNm=ops.getLoadFactor(2),
                         P_seccion_kN=-forces[0],eps0=eps0,
                         eps_c_extrema=ec,eps_s_max_abs=es))
        if ec <= -c['eps_cu'] or es >= c['eps_s_lim']:
            reason='limite de deformacion de material'
            break
        if i==c['pasos_curvatura']:
            break
        ops.integrator('DisplacementControl',2,3,step)
        if ops.analyze(1):
            reason='no convergencia antes del limite (REVISAR)'
            break
    return rows,reason


def run(c,out):
    f=fibers(c)
    dump_csv(out/'fibras.csv',[dict(y_m=y,z_m=z,area_m2=a,material=int(m)) for y,z,a,m in f])
    Ag=c['b_m']*c['h_m']; As=f[f[:,3]==2,2].sum()
    # Compresion pura: pico de la respuesta uniforme de ESTA ley constitutiva.
    P0,_=resultant(f,-c['eps_c0'],0,c)
    pm=[root_p(f,p,c) for p in (0,0.2*P0,0.4*P0)]
    pm.append(dict(P_kN=float(P0),M_kNm=0.0,phi_1_m=0.0,eps0=-c['eps_c0'],c_m=None))
    dump_csv(out/'PM_compatibilidad_envolvente_material.csv',pm)
    curves=[]
    statuses=[]
    for p in (0,0.2*P0,0.4*P0):
        rows,reason=moment_curvature(f,float(p),c)
        curves.append(rows); statuses.append(reason)
    dump_csv(out/'momento_curvatura.csv',[r for rows in curves for r in rows])
    # Independencia de malla en los tres puntos de flexocompresion.
    fine=fibers(c,2*c['fibras_por_lado'])
    mesh_error=max(abs(root_p(fine,r['P_kN'],c)['M_kNm']-r['M_kNm'])/max(1,abs(r['M_kNm'])) for r in pm[:-1])
    axial_error=max(abs(r['P_objetivo_kN']-r['P_seccion_kN']) for rows in curves for r in rows)
    pm_compatibility=pm
    # Capacidad bajo la historia efectiva OpenSees: maximo M antes del limite.
    # Concrete01 descarga y recarga: su trayectoria no coincide necesariamente
    # con integrar solo la envolvente monotona en el estado final de deformacion.
    pm=[]
    for rows in curves:
        valid=[r for r in rows if r['eps_c_extrema'] >= -c['eps_cu'] and r['eps_s_max_abs'] <= c['eps_s_lim']]
        peak=max(valid,key=lambda r:r['M_kNm'])
        pm.append(dict(P_kN=peak['P_objetivo_kN'],M_kNm=peak['M_kNm'],
                       phi_1_m=peak['phi_1_m'],eps0=peak['eps0']))
    pm.append(dict(P_kN=float(P0),M_kNm=0.0,phi_1_m=0.0,eps0=-c['eps_c0']))
    dump_csv(out/'PM_puntos.csv',pm)
    refined_c={**c,'pasos_curvatura':2*c['pasos_curvatura']}
    peak_error=0.0
    for point in pm[:-1]:
        refined_rows,refined_reason=moment_curvature(fine,point['P_kN'],refined_c)
        if 'REVISAR' in refined_reason:
            raise RuntimeError(refined_reason)
        valid=[r for r in refined_rows if r['eps_c_extrema'] >= -c['eps_cu'] and r['eps_s_max_abs'] <= c['eps_s_lim']]
        peak=max(r['M_kNm'] for r in valid)
        peak_error=max(peak_error,abs(peak-point['M_kNm'])/max(1,abs(peak)))
    fig,axes=plt.subplots(1,3,figsize=(15,4.5),layout='constrained')
    concrete=f[:,3]==1
    axes[0].scatter(f[concrete,1],f[concrete,0],s=2,color='#99a8b8',label='Hormigon')
    axes[0].scatter(f[~concrete,1],f[~concrete,0],s=45,color='#bb3e32',label='Acero')
    axes[0].set(xlabel='z [m]',ylabel='y [m]',title=f'Seccion {c["b_m"]:.2f} x {c["h_m"]:.2f} m')
    axes[0].set_aspect('equal'); axes[0].legend(loc='upper center',bbox_to_anchor=(0.5,-0.16),ncol=2,fontsize=8)
    for rows in curves:
        axes[1].plot([r['phi_1_m'] for r in rows],[r['M_kNm'] for r in rows],label=f'P={rows[0]["P_objetivo_kN"]:.0f} kN')
    axes[1].set(xlabel='Curvatura [1/m]',ylabel='Momento [kN m]',title='M-curvatura | OpenSees Fiber')
    axes[1].legend()
    axes[2].plot([r['M_kNm'] for r in pm],[r['P_kN'] for r in pm],'o--',color='#345a8a')
    for i,r in enumerate(pm): axes[2].annotate(str(i+1),(r['M_kNm'],r['P_kN']),xytext=(6,5),textcoords='offset points')
    axes[2].set(xlabel='Momento [kN m]',ylabel='Compresion P [kN]',title='Primeros puntos P-M nominales')
    for ax in axes: ax.grid(alpha=0.2)
    fig.savefig(out/'capacidad_HA.png',dpi=170); plt.close(fig)
    summary=dict(P0_kN=float(P0),As_m2=float(As),rho=float(As/Ag),n_fibras=len(f),
                 puntos=pm,puntos_envolvente=pm_compatibility,terminacion_curvas=statuses,error_malla_relativo=mesh_error,
                 error_pico_refinado_relativo=peak_error,
                 error_axial_kN=axial_error,error_area_m2=float(abs(f[:,2].sum()-Ag)),
                 estado='OK' if peak_error<0.03 and mesh_error<0.03 and axial_error<1e-4 and all('REVISAR' not in s for s in statuses) else 'REVISAR')
    (out/'resumen_capacidad.json').write_text(json.dumps(summary,indent=2),encoding='utf-8')
    return summary
