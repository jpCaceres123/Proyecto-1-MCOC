"""Conservation and validation of per-element editor inputs without OpenSees."""
import copy
import sys
import unittest
from pathlib import Path
sys.path.insert(0, str(Path(__file__).resolve().parents[2]/'model/builders'))
from variantes_interactivas import apply, section


class InteractiveVariants(unittest.TestCase):
    def setUp(self):
        self.data = {'elements': [{'id': 1, 'type': 'BEAM_X'}, {'id': 2, 'type': 'COLUMN'}],
                     'walls': [{'id': 101, 'thickness_m': .2}],
                     'slabs': [{'id': 10, 'thickness_m': .15, 'density_kg_m3': 2500,
                                'self_weight_kN_m2': .15*2500*9.80665/1000}],
                     'beam_load_cases': [{'slab_id': 10, 'tributary_area_m2': 3,
                                          'dead_load_kN': 20, 'q_SC_kN_m2': 5, 'w_G_max_kN_m': 10}],
                     'wall_load_cases': [{'slab_id': 10, 'tributary_area_m2': 2,
                                          'dead_load_kN': 12, 'q_SC_kN_m2': 5}]}

    def test_slab_weight_and_live_conservation(self):
        apply(self.data, [dict(kind='Losa', id=10, changeSection=True, h=.20,
                               changeLoad=True, q=4)])
        rows = self.data['beam_load_cases']+self.data['wall_load_cases']
        self.assertAlmostEqual(sum(r['dead_load_kN'] for r in rows), 32+.05*2500*9.80665/1000*5)
        self.assertEqual(sum(r['interactive_q_kN_m2']*r['tributary_area_m2'] for r in rows), 20)

    def test_changes_are_specific_to_selected_bar(self):
        original = copy.deepcopy(self.data['elements'][1])
        apply(self.data, [dict(kind='Viga', id=1, changeSection=True, b=.75, h=.45, changeLoad=True, q=2)])
        self.assertEqual(self.data['elements'][1], original)
        self.assertAlmostEqual(self.data['elements'][0]['section_override']['A_m2'], .3375)

    def test_shs_preserves_hollow_geometry(self):
        s = section(.3, .02, True)
        self.assertAlmostEqual(s['A_m2'], .3**2-.26**2)
        self.assertAlmostEqual(s['Iy_m4'], (.3**4-.26**4)/12)
        with self.assertRaises(ValueError): section(.3, .16, True)

    def test_invalid_inputs(self):
        for x in (0, -1, float('nan'), float('inf')):
            with self.assertRaises(ValueError): section(x, .3)
        with self.assertRaises(ValueError): apply(self.data, [dict(kind='Columna', id=1)])
        with self.assertRaises(ValueError): apply(self.data, [dict(kind='Muro', id=999)])


if __name__ == '__main__':
    unittest.main()
