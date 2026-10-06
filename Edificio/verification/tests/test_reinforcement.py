import copy
import json
from pathlib import Path
import sys
import unittest
import numpy as np
import openseespy.opensees as ops
ROOT=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(ROOT/'analysis/capacity'))
from capacidad import fibers, resultant
from reinforcement import generate


class ReinforcementTests(unittest.TestCase):
    def setUp(self):self.c=json.loads((ROOT/'data/parameters/parametros.json').read_text())['columna']
    def test_rectangular_bar_coordinates_and_area(self):
        self.c['b_m']=.4;self.c['h_m']=.8
        f=fibers(self.c);bars=f[f[:,3]==2]
        self.assertLessEqual(abs(bars[:,1]).max(),.2-self.c['recubrimiento_al_centro_barra_m']+1e-12)
        self.assertAlmostEqual(f[:,2].sum(),.32,places=10)
    def test_revision_and_material_not_global_stiffness(self):
        a=generate(self.c,5,.028,31);b=generate(self.c,5,.032,31)
        self.assertNotEqual(a['capacityRevision'],b['capacityRevision']);self.assertFalse(b['globalStiffnessChanged'])
        self.assertGreater(b['steelArea_m2'],a['steelArea_m2'])
        self.assertEqual([c['axis'] for c in a['curves']],['Mz','My'])
    def test_invalid_layout(self):
        with self.assertRaises(ValueError):generate(self.c,True,.028)
        self.c['b_m']=.20
        with self.assertRaises(ValueError):generate(self.c,12,.04)
    def test_fiber_refinement(self):
        f40=fibers(self.c,40);f80=fibers(self.c,80)
        np.testing.assert_allclose(resultant(f40,-.001,.003,self.c),resultant(f80,-.001,.003,self.c),rtol=.01,atol=1)
    def test_independent_opensees_section_force(self):
        f=fibers(self.c,30);eps,phi=-.001,.001
        ops.wipe();ops.model('basic','-ndm',2,'-ndf',3);ops.node(1,0,0);ops.node(2,0,0);ops.fix(1,1,1,1)
        ops.uniaxialMaterial('Concrete01',1,-self.c['fc_MPa']*1000,-self.c['eps_c0'],0,-self.c['eps_cu'])
        ops.uniaxialMaterial('Steel01',2,self.c['fy_MPa']*1000,self.c['Es_MPa']*1000,0)
        # Compare about the declared geometric origin, not OpenSees' mesh centroid.
        ops.section('Fiber',1,'-noCentroid')
        for y,z,area,mat in f:ops.fiber(float(y),float(z),float(area),int(mat))
        ops.element('zeroLengthSection',1,1,2,1)
        # Keep one harmless free shear DOF so BandGeneral never receives an empty system.
        ops.uniaxialMaterial('Elastic',3,1);ops.element('zeroLength',2,1,2,'-mat',3,'-dir',2)
        ops.timeSeries('Linear',1);ops.pattern('Plain',1,1);ops.sp(2,1,eps);ops.sp(2,3,phi)
        ops.constraints('Transformation');ops.numberer('Plain');ops.system('BandGeneral');ops.algorithm('Linear');ops.integrator('LoadControl',1);ops.analysis('Static')
        self.assertEqual(ops.analyze(1),0);forces=ops.eleResponse(1,'section','force')
        p,m=resultant(f,eps,phi,self.c);np.testing.assert_allclose([p,m],[-forces[0],forces[1]],rtol=1e-8,atol=1e-6);ops.wipe()


if __name__=='__main__':unittest.main()
