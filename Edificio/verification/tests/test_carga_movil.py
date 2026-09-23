"""Independent analytical beam benchmark and generated SQ4 contract checks."""
import json
import hashlib
import gzip
from pathlib import Path
import sys
import unittest
import numpy as np
import openseespy.opensees as ops

ROOT=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(ROOT/'analysis/load_cases'))
from carga_movil import weights, POSITIONS


class MovingLoadTests(unittest.TestCase):
    def solve_beam(self,a,split=False,fixed=False):
        L,E,I,P=6.,25e6,.004,20.
        ops.wipe();ops.model('basic','-ndm',3,'-ndf',6)
        coords=sorted(set([0.,L]+([a,L/2] if split else [])))
        for n,x in enumerate(coords,1):ops.node(n,x,0.,0.)
        ops.fix(1,1,1,1,1,1,int(fixed));ops.fix(len(coords),1,1,1,1,1,int(fixed))
        ops.geomTransf('Linear',1,0,0,1)
        for n in range(1,len(coords)):ops.element('elasticBeamColumn',n,n,n+1,.2,E,E/2.4,.008,I,I,1)
        ops.timeSeries('Linear',1);ops.pattern('Plain',1,1)
        if split:ops.load(coords.index(a)+1,0,-P,0,0,0,0)
        else:ops.eleLoad('-ele',1,'-type','-beamPoint',-P,0,a/L,0)
        ops.constraints('Penalty',1e14,1e14);ops.numberer('RCM');ops.system('BandGeneral');ops.algorithm('Linear')
        ops.integrator('LoadControl',1);ops.analysis('Static');self.assertEqual(ops.analyze(1),0)
        return dict(f=np.array(ops.eleResponse(1,'localForce')),
                    u=np.array(ops.nodeDisp(1)),mid=ops.nodeDisp(coords.index(L/2)+1,2) if split else None,
                    E=E,I=I,P=P,L=L)

    def test_point_load_field_against_independent_subdivision(self):
        for fixed in (False,True):
            for a in (1.302,3.,4.734):
                one=self.solve_beam(a,fixed=fixed);split=self.solve_beam(a,True,fixed)
                f,u=one['f'],one['u'];x=3.;P=one['P'];EI=one['E']*one['I']
                # Exact curvature integration used by the viewer's loaded beam.
                displacement=u[1]+u[5]*x+(-f[5]*x*x/2+f[1]*x**3/6-P*max(0,x-a)**3/6)/EI
                self.assertAlmostEqual(displacement,split['mid'],delta=1e-10)
                if a==3.:
                    expected=-P*one['L']**3/(192*EI if fixed else 48*EI)
                    self.assertAlmostEqual(displacement,expected,delta=1e-10)

    def test_cubic_response_exact_between_bases(self):
        samples=[self.solve_beam(float(s)*6,fixed=True)['f'] for s in POSITIONS]
        for s in (.001,.217,.739,.999):
            explicit=self.solve_beam(s*6,fixed=True)['f']
            np.testing.assert_allclose(weights(s)@np.array(samples),explicit,atol=1e-10)

    def test_generated_contract_and_audit(self):
        audit=json.loads((ROOT/'results/verificacion_carga_movil.json').read_text())
        self.assertTrue(all(c['estado']=='OK' for c in audit['checks']))
        data=json.loads((ROOT/'visualization/unity/UnityVisualization/Assets/Resources/carga_movil.json').read_text())
        self.assertEqual(data['modelHash'],hashlib.sha256((ROOT/'results/modelo_3d_manual.json').read_bytes()).hexdigest())
        self.assertEqual(len(data['panels']),audit['panels'])
        self.assertEqual(data['schema'],3)
        self.assertEqual(len(data['panels']),652)
        bases=set(data['basisNodes']);bars={b['id']:b for b in data['bars']}
        for panel in data['panels']:
            for tag in panel['receivers']:
                self.assertIn(bars[tag]['i'],bases);self.assertIn(bars[tag]['j'],bases)
            self.assertGreater(panel['xmax'],panel['xmin']);self.assertGreater(panel['ymax'],panel['ymin'])
        count=len(data['nodes'])*6+len(data['bars'])*12+6
        for n in bases:
            path=ROOT/f'visualization/unity/UnityVisualization/Assets/Resources/SQ4Nodes/n{data["nodes"][n]["id"]}.bytes'
            array=np.frombuffer(gzip.decompress(path.read_bytes()),dtype='<f8')
            self.assertEqual(array.size,count*3);self.assertTrue(np.isfinite(array).all())

    def test_shared_panel_edge_has_single_receiver(self):
        data=json.loads((ROOT/'visualization/unity/UnityVisualization/Assets/Resources/carga_movil.json').read_text())
        transitions=0
        for lower in data['panels']:
            for upper in data['panels']:
                if (lower['rule']==upper['rule']=='four_edges' and lower['z']==upper['z'] and
                    abs(lower['ymax']-upper['ymin'])<1e-9 and lower['xmin']==upper['xmin'] and lower['xmax']==upper['xmax'] and
                    len(lower['groups'][1]['ids'])==len(upper['groups'][0]['ids'])==1):
                    self.assertEqual(lower['groups'][1]['ids'],upper['groups'][0]['ids'])
                    transitions+=1
        self.assertGreaterEqual(transitions,15)


if __name__=='__main__':unittest.main()
