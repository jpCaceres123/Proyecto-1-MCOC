"""Comprobaciones independientes del cálculo pseudoestático con ejemplos manuales."""
import unittest
from sismo import calcular_pisos


class SismoTests(unittest.TestCase):
    def setUp(self):
        self.cfg=dict(g_m_s2=10.,fraccion_Q_masa=.5,aceleracion_fraccion_g=.2)
        self.coords={1:(0.,0.,3.),2:(4.,2.,3.),3:(0.,0.,6.),4:(0.,0.,0.)}
        self.diaph=[dict(node_ids=[1,2],master_node=1,z_m=3.,subbuilding='A'),
                    dict(node_ids=[3],master_node=3,z_m=6.,subbuilding='A')]
        self.G={1:100.,2:200.,3:100.,4:50.}
        self.Q={1:0.,2:200.,3:0.,4:0.}

    def test_manual_mass_cm_force_and_moments(self):
        floors,audit,base=calcular_pisos(self.diaph,self.coords,self.G,self.Q,self.cfg)
        first=floors[0]
        self.assertEqual(first['masa_t'],40.)
        self.assertEqual(first['a_m_s2'],2.)
        self.assertEqual(first['F_kN'],80.)
        self.assertEqual(first['CM_x_m'],3.)
        self.assertEqual(first['CM_y_m'],1.5)
        self.assertEqual(first['Mz_EX_master_kNm'],-120.)
        self.assertEqual(first['Mz_EY_master_kNm'],240.)
        self.assertEqual(base,50.)
        self.assertEqual(sum(r['masa_t'] for r in audit),50.)

    def test_height_alone_does_not_change_acceleration(self):
        floors,_,_=calcular_pisos(self.diaph,self.coords,self.G,self.Q,self.cfg)
        self.assertEqual(floors[0]['a_m_s2'],floors[1]['a_m_s2'])
        self.assertEqual(floors[1]['F_kN'],20.)

    def test_professor_profile_zero_and_negative(self):
        cfg={**self.cfg,'aceleracion_por_piso_g':{'1':0.,'2':-.3}}
        floors,_,_=calcular_pisos(self.diaph,self.coords,self.G,self.Q,cfg)
        self.assertEqual(floors[0]['F_kN'],0.)
        self.assertEqual(floors[1]['F_kN'],-30.)

    def test_duplicate_and_missing_mass_rejected(self):
        with self.assertRaises(ValueError):
            calcular_pisos(self.diaph+[self.diaph[0]],self.coords,self.G,self.Q,self.cfg)
        with self.assertRaises(ValueError):
            calcular_pisos(self.diaph[:1],self.coords,self.G,self.Q,self.cfg)

    def test_invalid_floor_override_rejected(self):
        with self.assertRaises(ValueError):
            calcular_pisos(self.diaph,self.coords,self.G,self.Q,
                           {**self.cfg,'aceleracion_por_piso_g':{'7':.4}})


    def test_gravity_factor_changes_mass_and_center(self):
        floors,_,base=calcular_pisos(self.diaph,self.coords,self.G,self.Q,
                                   {**self.cfg,'ponderador_G_masa':2.})
        self.assertEqual(floors[0]['masa_t'],70.)
        self.assertEqual(floors[0]['F_kN'],140.)
        self.assertAlmostEqual(floors[0]['CM_x_m'],20/7)
        self.assertAlmostEqual(floors[0]['CM_y_m'],10/7)
        self.assertEqual(base,100.)

    def test_invalid_mass_factors_and_zero_rejected(self):
        for ag,aq in ((-1,.5),(1,-.5),(float('nan'),.5),(1,float('inf')),(0,0)):
            with self.subTest(ag=ag,aq=aq),self.assertRaises(ValueError):
                calcular_pisos(self.diaph,self.coords,self.G,self.Q,
                               {**self.cfg,'ponderador_G_masa':ag,'fraccion_Q_masa':aq})

    def test_zero_mass_floor_allowed_only_for_internal_basis(self):
        cfg={**self.cfg,'ponderador_G_masa':0.,'fraccion_Q_masa':1.}
        with self.assertRaises(ValueError):
            calcular_pisos(self.diaph,self.coords,self.G,self.Q,cfg)
        floors,_,_=calcular_pisos(self.diaph,self.coords,self.G,self.Q,
                                 {**cfg,'_permitir_masa_nula_base':True})
        self.assertEqual(floors[1]['F_kN'],0.)


if __name__=='__main__':
    unittest.main()
