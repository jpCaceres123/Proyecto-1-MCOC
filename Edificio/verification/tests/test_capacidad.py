import copy
import json
from pathlib import Path
import unittest

import sys
sys.path.insert(0, str(Path(__file__).resolve().parents[2] / 'analysis' / 'capacity'))
from capacidad import interaction_points


ROOT = Path(__file__).resolve().parents[2]


class InteractionDiagramTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.column = json.loads((ROOT / 'data' / 'parameters' / 'parametros.json').read_text(
            encoding='utf-8'))['columna']

    def test_seven_calculated_states(self):
        points = interaction_points(self.column)
        self.assertEqual([point['punto'] for point in points], list('ABCDEFG'))
        self.assertAlmostEqual(points[5]['P_kN'], 0.0, places=8)
        self.assertGreater(points[2]['M_kNm'], points[1]['M_kNm'])

    def test_results_change_with_material_input(self):
        base = interaction_points(self.column)
        changed = copy.deepcopy(self.column)
        changed['fc_MPa'] *= 1.10
        modified = interaction_points(changed)
        self.assertNotAlmostEqual(base[0]['P_kN'], modified[0]['P_kN'])
        self.assertNotAlmostEqual(base[2]['M_kNm'], modified[2]['M_kNm'])


if __name__ == '__main__':
    unittest.main()
