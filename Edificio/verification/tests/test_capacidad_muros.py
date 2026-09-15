import unittest
from capacidad_muros import calculate, dense_envelope, vertical_steel_layers


class WallInteractionTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.sections,cls.registry=calculate()

    def test_all_model_walls_are_assigned(self):
        self.assertEqual(set(range(1,25)),{item['source_wall_id'] for item in self.sections})

    def test_each_analysis_wall_is_one_storey(self):
        self.assertEqual(len({item['wall_id'] for item in self.sections}),
                         len({(item['source_wall_id'],item['piso']) for item in self.sections}))
        self.assertTrue(all(item['piso'] >= 1 for item in self.sections))

    def test_profiles_cover_wall_height(self):
        by_wall={}
        for item in self.sections:
            by_wall.setdefault(item['source_wall_id'],0.0)
            by_wall[item['source_wall_id']]+=item['z_max_m']-item['z_min_m']
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


if __name__=='__main__':
    unittest.main()
