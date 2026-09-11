import unittest
from capacidad_muros import calculate, dense_envelope, vertical_steel_layers


class WallInteractionTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.sections,cls.registry=calculate()

    def test_all_model_walls_are_assigned(self):
        self.assertEqual(set(range(1,25)),{item['wall_id'] for item in self.sections})

    def test_profiles_cover_wall_height(self):
        by_wall={}
        for item in self.sections:
            by_wall.setdefault(item['wall_id'],0.0)
            by_wall[item['wall_id']]+=item['z_max_m']-item['z_min_m']
        self.assertTrue(all(height>0 for height in by_wall.values()))

    def test_spacing_does_not_exceed_specification(self):
        for item in self.sections:
            self.assertLessEqual(item['actual_spacing_m'],item['vertical_spacing_mm']/1000+1e-12)

    def test_key_endpoints_and_flexure(self):
        for item in self.sections:
            points={point['punto']:point for point in item['points']}
            self.assertGreater(points['A']['P_kN'],0)
            self.assertEqual(points['A']['M_kNm'],0)
            self.assertLess(points['G']['P_kN'],0)
            self.assertEqual(points['G']['M_kNm'],0)
            self.assertAlmostEqual(points['F']['P_kN'],0,places=5)
            self.assertGreater(points['F']['M_kNm'],0)

    def test_dense_branch_is_finite(self):
        for item in self.sections:
            self.assertTrue(all(abs(p['P_kN'])<1e9 and abs(p['M_kNm'])<1e9 for p in item['curve']))

    def test_critical_points_are_on_or_inside_envelope(self):
        for item in self.sections:
            curve=item['curve']
            for key in item['points']:
                nearby=[p['M_kNm'] for p in curve
                        if abs(p['P_kN']-key['P_kN']) < 1e-6*max(1.0,abs(key['P_kN']))]
                self.assertTrue(nearby,f"Muro {item['wall_id']} punto {key['punto']} sin curva al mismo P")
                self.assertGreaterEqual(max(nearby)+1e-6*max(1.0,key['M_kNm']),key['M_kNm'])


if __name__=='__main__':
    unittest.main()
