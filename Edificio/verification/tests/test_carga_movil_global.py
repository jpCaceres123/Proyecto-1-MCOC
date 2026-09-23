"""Independent split-element benchmarks for force + eccentric point couple."""
import json
import sys
import unittest
from pathlib import Path
import numpy as np
import openseespy.opensees as ops
ROOT=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(ROOT/'analysis/load_cases'))
from carga_movil_global import equivalent,valid,transfer

class AllSlabsTests(unittest.TestCase):
    def test_four_edges_receive_conservative_weights(self):
        data=json.loads((ROOT/'visualization/unity/UnityVisualization/Assets/Resources/carga_movil.json').read_text())
        bars={b['id']:b for b in data['bars']};xyz=np.array([n['xyz'] for n in data['nodes']])
        panels=[p for p in data['panels'] if p['rule']=='four_edges']
        self.assertGreater(len(panels),100)
        for p in panels:
            for xi,eta in ((.5,.5),(.17,.83),(.01,.99)):
                x=p['xmin']+xi*(p['xmax']-p['xmin']);y=p['ymin']+eta*(p['ymax']-p['ymin'])
                if not valid(p,x,y):continue
                parts=transfer(p,x,y,40,bars,xyz)
                self.assertEqual(len(parts),4)
                np.testing.assert_allclose([t['P'] for t in parts],
                    [20*(1-eta),20*eta,20*(1-xi),20*xi],atol=1e-9)
                applied=sum((np.cross(t['q'],[0,0,-t['P']])+t['moment'] for t in parts),np.zeros(3))
                np.testing.assert_allclose(applied,np.cross([x,y,p['z']],[0,0,-40]),atol=1e-8)
    def test_eccentric_point_force_and_couple_against_split_beam(self):
        xyz=np.array([[0.,0.,0.],[6.,0.,0.]])
        b=dict(i=0,j=1,x=[1,0,0],y=[0,1,0],z=[0,0,1])
        E,I,L,P=25e6,.004,6.,20.
        for fixed in (False,True):
            for s in (.0,.217,.5,.739,1.):
                a=L*s; moment=np.array([12.,8.,0.])
                local,loads=equivalent(b,xyz,s,P,moment)
                responses=[]
                for split in (False,True):
                    coords=sorted(set([0.,L]+([a,3.] if split else [])))
                    ops.wipe();ops.model('basic','-ndm',3,'-ndf',6)
                    for n,x in enumerate(coords,1):ops.node(n,x,0.,0.)
                    ops.fix(1,1,1,1,1,1,1)
                    if fixed:ops.fix(len(coords),1,1,1,1,1,1)
                    ops.geomTransf('Linear',1,0,0,1)
                    for n in range(1,len(coords)):ops.element('elasticBeamColumn',n,n,n+1,.2,E,E/2.4,.008,I,I,1)
                    ops.timeSeries('Linear',1);ops.pattern('Plain',1,1)
                    if split:ops.load(coords.index(a)+1,0.,0.,-P,*moment)
                    else:
                        ops.load(1,*loads[0]);ops.load(len(coords),*loads[1])
                    ops.constraints('Penalty',1e14,1e14);ops.numberer('RCM');ops.system('BandGeneral')
                    ops.algorithm('Linear');ops.integrator('LoadControl',1);ops.analysis('Static');self.assertEqual(ops.analyze(1),0)
                    if split:responses.append(ops.nodeDisp(coords.index(3.)+1,3))
                    else:
                        f=np.array(ops.eleResponse(1,'localForce'))-local;ui=np.array(ops.nodeDisp(1));x=3.;arm=max(0,x-a)
                        responses.append(ui[2]-ui[4]*x+(f[4]*x*x/2+f[2]*x**3/6-P*arm**3/6+moment[1]*arm*arm/2)/(E*I))
                self.assertAlmostEqual(*responses,delta=2e-9)

    def test_all_panel_coverage_and_void_rejection(self):
        data=json.loads((ROOT/'visualization/unity/UnityVisualization/Assets/Resources/carga_movil.json').read_text())
        model=json.loads((ROOT/'results/modelo_3d_manual.json').read_text())
        self.assertEqual({p['id'] for p in data['panels']},{p['id'] for p in model['slabs']})
        bars={b['id']:b for b in data['bars']};xyz=np.array([n['xyz'] for n in data['nodes']])
        for p in data['panels']:
            self.assertFalse(valid(p,p['xmin']-1,p['ymin']))
            for h in p['voids']:
                x=(h['xmin']+h['xmax'])/2;y=(h['ymin']+h['ymax'])/2
                self.assertFalse(valid(p,x,y))
                with self.assertRaises(ValueError):transfer(p,x,y,1,bars,xyz)

    def test_cantilever_transfers_nonzero_couple_and_zero_load(self):
        data=json.loads((ROOT/'visualization/unity/UnityVisualization/Assets/Resources/carga_movil.json').read_text())
        bars={b['id']:b for b in data['bars']};xyz=np.array([n['xyz'] for n in data['nodes']])
        for p in data['panels']:
            if p['status']!='EXPLICIT_CANTILEVER':continue
            x=(p['xmin']+p['xmax'])/2;y=(p['ymin']+p['ymax'])/2
            result=transfer(p,x,y,10,bars,xyz)
            self.assertGreater(sum(np.linalg.norm(t['moment']) for t in result),0)
            for t in transfer(p,x,y,0,bars,xyz):
                self.assertEqual(t['P'],0);np.testing.assert_equal(t['moment'],np.zeros(3))

if __name__=='__main__':unittest.main()
