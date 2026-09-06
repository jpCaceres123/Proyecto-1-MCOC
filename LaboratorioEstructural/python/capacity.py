"""Didactic RC fiber sections; prescribed strain P-M and constant-P M-curvature.
Reinforcement is an explicit editable assumption, not extracted from rebar drawings.
"""
import math
import numpy as np
import openseespy.opensees as ops

def reinforcement(b,h,cfg,kind):
    cover=cfg['cover_m'];bars=[]
    if kind=='COLUMN':
        count=int(cfg['column_bar_count']);assert count>=4 and count%4==0
        A=math.pi*cfg['column_bar_diameter_m']**2/4
        # Equally spaced around perimeter; corner bars occur exactly once.
        ys=[h/2-cover,-h/2+cover];zs=[b/2-cover,-b/2+cover]
        per=count//4
        corners=[(ys[0],zs[0]),(ys[0],zs[1]),(ys[1],zs[1]),(ys[1],zs[0])]
        for k in range(4):
            a=np.array(corners[k]);d=np.array(corners[(k+1)%4])-a
            for j in range(per):bars.append((*list(a+d*j/per),A))
    else:
        count=24;A=cfg['wall_reinforcement_ratio']*b*h/(2*count)
        for y in np.linspace(-h/2+cover,h/2-cover,count):
            for z in [-b/2+cover,b/2-cover]:bars.append((float(y),float(z),A))
    return bars

def define_section(b,h,cfg,kind):
    fc=cfg['fc_MPa']*1000;fy=cfg['fy_MPa']*1000;Es=cfg['Es_MPa']*1000
    ops.uniaxialMaterial('Concrete01',1,-fc,-.002,0.,-.0035)
    ops.uniaxialMaterial('Steel01',2,fy,Es,.005)
    ops.section('Fiber',1)
    bars=reinforcement(b,h,cfg,kind);nz=8
    ny=min(80,max(12,int(b*h/(2*max(bar[2] for bar in bars)*nz))))
    dy=h/ny;dz=b/nz;areas=np.full((ny,nz),dy*dz)
    for y,z,A in bars:
        iy=min(ny-1,max(0,int((y+h/2)/dy)));iz=min(nz-1,max(0,int((z+b/2)/dz)))
        areas[iy,iz]-=A
    if np.min(areas)<=0:raise ValueError('Fiber mesh too fine relative to bar area')
    for iy in range(ny):
        for iz in range(nz):ops.fiber(-h/2+(iy+.5)*dy,-b/2+(iz+.5)*dz,float(areas[iy,iz]),1)
    for y,z,A in bars:ops.fiber(y,z,A,2)
    return bars

def strain_response(b,h,cfg,kind,epsilon0,kappa):
    ops.wipe();ops.model('basic','-ndm',2,'-ndf',3)
    define_section(b,h,cfg,kind)
    ops.node(1,0.,0.);ops.node(2,0.,0.);ops.fix(1,1,1,1);ops.fix(2,0,1,0)
    ops.element('zeroLengthSection',1,1,2,1)
    # A decoupled dummy spring leaves one free equation for the linear system.
    ops.node(3,0.,0.);ops.fix(3,0,1,1)
    ops.uniaxialMaterial('Elastic',3,1.);ops.element('zeroLength',2,1,3,'-mat',3,'-dir',1)
    ops.timeSeries('Linear',1);ops.pattern('Plain',1,1)
    ops.sp(2,1,float(epsilon0));ops.sp(2,3,float(kappa));ops.load(3,1e-6,0.,0.)
    ops.constraints('Transformation');ops.numberer('Plain');ops.system('BandGeneral')
    ops.test('NormUnbalance',1e-7,30);ops.algorithm('Newton');ops.integrator('LoadControl',.1);ops.analysis('Static')
    if ops.analyze(10):raise RuntimeError('Fiber prescribed-strain analysis failed')
    f=ops.eleResponse(1,'section','force')
    return -float(f[0]),float(f[1])

def moment_curvature(b,h,cfg,kind,P):
    ops.wipe();ops.model('basic','-ndm',2,'-ndf',3);define_section(b,h,cfg,kind)
    ops.node(1,0.,0.);ops.node(2,0.,0.);ops.fix(1,1,1,1);ops.fix(2,0,1,0)
    ops.element('zeroLengthSection',1,1,2,1)
    ops.timeSeries('Linear',1);ops.pattern('Plain',1,1);ops.load(2,-P,0.,0.)
    ops.constraints('Plain');ops.numberer('Plain');ops.system('BandGeneral')
    ops.test('NormUnbalance',1e-6,60);ops.algorithm('Newton');ops.integrator('LoadControl',.1);ops.analysis('Static')
    if ops.analyze(10):raise RuntimeError('Axial preload failed')
    ops.loadConst('-time',0.);ops.timeSeries('Linear',2);ops.pattern('Plain',2,2);ops.load(2,0.,0.,1.)
    dk=.04/h/240;curve=[];reason='curvature_limit'
    ops.integrator('DisplacementControl',2,3,dk)
    for step in range(240):
        if ops.analyze(1):reason='nonconvergence';break
        eps=ops.nodeDisp(2,1);k=ops.nodeDisp(2,3);M=ops.getLoadFactor(2)
        curve.append(dict(curvature=k,moment=M,axial=P))
        if eps-k*h/2 <= -cfg['epsilon_c_limit'] or eps+k*h/2>=cfg['epsilon_s_limit']:
            reason='strain_limit';break
    if len(curve)<4:raise RuntimeError('Insufficient moment-curvature points')
    return curve,reason

def whitney(b,h,cfg,kind):
    fc=cfg['fc_MPa']*1000;fy=cfg['fy_MPa']*1000;Es=cfg['Es_MPa']*1000
    bars=reinforcement(b,h,cfg,kind);beta=max(.65,.85-.05*max(0,cfg['fc_MPa']-28)/7)
    points=[]
    for c in np.geomspace(h*.02,h*100,80):
        a=min(h,beta*c);C=.85*fc*b*a;M=C*(h/2-a/2);P=C
        for y,z,A in bars:
            depth=h/2-y;eps=cfg['epsilon_c_limit']*(1-depth/c);stress=float(np.clip(Es*eps,-fy,fy))
            # Steel displaces stress-block concrete only inside compression block.
            force=A*(stress-(.85*fc if depth<=a else 0))
            P+=force;M+=force*y
        points.append(dict(P=P,M=abs(M)))
    return points

def calculate_capacities(config,model):
    cfg=config['capacity'];out=[]
    representatives=[next(e for e in model['elements'] if e['kind']=='COLUMN'),
                     next(e for e in model['elements'] if e['kind']=='WALL' and e['wall']==2)]
    for e in representatives:
        b,h=e['b'],e['h'];kind=e['kind'];bars=reinforcement(b,h,cfg,kind)
        As=sum(x[2] for x in bars);fc=cfg['fc_MPa']*1000;fy=cfg['fy_MPa']*1000
        hand=(b*h-As)*fc+As*min(fy,cfg['Es_MPa']*1000*.002)
        pc,mc=strain_response(b,h,cfg,kind,-.002,0.)
        relative=abs(pc-hand)/hand
        if relative>.01:raise AssertionError(f'Independent uniform compression check {relative}')
        pm=[]
        for neutral in np.geomspace(h*.025,h*80,70):
            # Compression-dominated states pivot toward uniform peak strain -0.002,
            # instead of following Concrete01's post-peak uniform compression branch.
            extreme=cfg['epsilon_c_limit'] if neutral<=h else .002*neutral/(neutral-(1-.002/cfg['epsilon_c_limit'])*h)
            k=extreme/neutral;eps0=-extreme+k*h/2
            # Limit tensile strain as well as compression strain for the fiber envelope.
            max_tension=eps0+k*h/2
            scale=min(1,cfg['epsilon_s_limit']/max_tension) if max_tension>0 else 1
            P,M=strain_response(b,h,cfg,kind,eps0*scale,k*scale);pm.append(dict(P=P,M=abs(M)))
        pt,mt=strain_response(b,h,cfg,kind,fy/(cfg['Es_MPa']*1000),0.)
        pm.extend([dict(P=pt,M=0.),dict(P=pc,M=0.)]);pm.sort(key=lambda p:p['P'])
        curve,stop=moment_curvature(b,h,cfg,kind,.1*pc)
        out.append(dict(name=f'Columna {b*100:.0f}x{h*100:.0f}' if kind=='COLUMN' else 'Muro 2: flexión en su plano',
            elementId=e['id'],kind=kind,b=b,h=h,steelArea=As,assumption=cfg['assumption'],
            pm=pm,whitney=whitney(b,h,cfg,kind),mphi=curve,stopReason=stop,
            checkCompression=dict(opensees=pc,independent=hand,relative=relative)))
        print(f'Fiber {kind}: {len(pm)} P-M, {len(curve)} M-phi points; compression check {relative:.2e}',flush=True)
    return out
