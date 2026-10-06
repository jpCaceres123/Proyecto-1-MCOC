"""Editable nominal uniaxial SECTION capacity. Does not change global EI."""
import argparse
import copy
import hashlib
import json
from pathlib import Path
import numpy as np
from capacidad import fibers, ultimate, resultant


def generate(config, count, diameter, samples=161):
    if isinstance(count,bool) or not isinstance(count,int) or not 2<=count<=12:
        raise ValueError('2..12 bars per face')
    if not np.isfinite(diameter) or not .008<=diameter<=.04:
        raise ValueError('Diameter 0.008..0.040 m')
    c=copy.deepcopy(config);c['barras_por_cara']=count;c['diametro_m']=diameter;c['diametros_por_cara_m']=[diameter]*count
    f=fibers(c);curves=[]
    for axis,column,depth in (('Mz',0,c['h_m']),('My',1,c['b_m'])):
        # Rotate section coordinates, not loads; material and bar areas are identical.
        cf=copy.deepcopy(c);ff=f.copy()
        if column==1:ff[:,[0,1]]=ff[:,[1,0]];cf['b_m'],cf['h_m']=cf['h_m'],cf['b_m']
        rows=[ultimate(ff,float(x),cf) for x in np.geomspace(depth*1e-4,depth*20,samples)]
        compression=resultant(ff,-cf['eps_c0'],0,cf);tension=resultant(ff,cf['fy_MPa']/cf['Es_MPa']*1.01,0,cf)
        # Include exact uniform peak compression/tension states, separately labelled.
        rows=[dict(P_kN=tension[0],M_kNm=0,state='pure_tension')]+rows+[dict(P_kN=compression[0],M_kNm=0,state='pure_peak_compression')]
        curves.append(dict(axis=axis,points=rows))
    body=dict(schema=1,units=dict(P='kN',M='kN*m'),section=c,curves=curves,
              concreteArea_m2=float(f[f[:,3]==1,2].sum()),steelArea_m2=float(f[f[:,3]==2,2].sum()),
              scope='nominal uniaxial section; sampled material-limit path, not biaxial/member/code verification',
              globalStiffnessChanged=False,
              reinforcementSource=f'Hipótesis académica editada: {4*count-4} barras Ø{diameter*1000:g} mm; recubrimiento al centro {c["recubrimiento_al_centro_barra_m"]*1000:g} mm',
              baseReinforcementSource=config.get('fuente_armadura','academic assumption'))
    body['capacityRevision']=hashlib.sha256(json.dumps(body,sort_keys=True,allow_nan=False).encode()).hexdigest()
    return body


if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--parameters',type=Path,required=True);p.add_argument('--output',type=Path,required=True)
    p.add_argument('--bars',type=int,required=True);p.add_argument('--diameter',type=float,required=True);a=p.parse_args()
    result=generate(json.loads(a.parameters.read_text(encoding='utf-8'))['columna'],a.bars,a.diameter)
    a.output.write_text(json.dumps(result,indent=2,allow_nan=False),encoding='utf-8')
