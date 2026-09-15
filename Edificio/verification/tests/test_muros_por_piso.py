import json
from pathlib import Path
import sys
import unittest

ROOT=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(ROOT/'model'/'opensees'))
import modelo_opensees_3d as opensees_model


class WallStoreyTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.model=json.loads((ROOT/'results/modelo_3d_manual.json').read_text(encoding='utf-8'))

    def test_each_wall_record_spans_exactly_one_level_interval(self):
        levels=sorted({round(float(n['z_m']),6) for n in self.model['nodes']})
        intervals={(levels[i],levels[i+1]):i+1 for i in range(len(levels)-1)}
        for wall in self.model['walls']:
            key=(round(float(wall['z_i_m']),6),round(float(wall['z_j_m']),6))
            self.assertIn(key,intervals)
            self.assertEqual(wall['floor'],intervals[key])
            self.assertEqual(wall['id'],wall['source_wall_id']*100+wall['floor'])
            self.assertLessEqual(wall['source_z_i_m'],wall['z_i_m'])
            self.assertGreaterEqual(wall['source_z_j_m'],wall['z_j_m'])

    def test_source_wall_area_is_preserved(self):
        originals={w['id']:w for w in self.model['source_walls']}
        accumulated={wid:0.0 for wid in originals}
        for wall in self.model['walls']:
            length=((wall['x_j_m']-wall['x_i_m'])**2+(wall['y_j_m']-wall['y_i_m'])**2)**.5
            accumulated[wall['source_wall_id']]+=length*(wall['z_j_m']-wall['z_i_m'])
        for wid,wall in originals.items():
            length=((wall['x_j_m']-wall['x_i_m'])**2+(wall['y_j_m']-wall['y_i_m'])**2)**.5
            expected=length*(wall['z_j_m']-wall['z_i_m'])
            self.assertAlmostEqual(accumulated[wid],expected,places=9)

    def test_wall_loads_target_panel_below_floor(self):
        walls={w['id']:w for w in self.model['walls']}
        for load in self.model['wall_load_cases']:
            wall=walls[load['wall_id']]
            self.assertEqual(wall['source_wall_id'],load['source_wall_id'])
            self.assertAlmostEqual(wall['z_j_m'],load['level_z_m'])

    def test_transfer_wall_mesh_contains_supporting_wall_intersections(self):
        wall=next(w for w in self.model['walls'] if w['id']==1002)
        stations=opensees_model.wall_mesh_stations(wall,self.model['walls'],[])
        x_values=[wall['x_i_m']+t*(wall['x_j_m']-wall['x_i_m']) for t in stations]
        # Muros inferiores 22, 11 y 12 encuentran la base del muro 10 en
        # x=-0.25, 3.60 y 6.40 m. Los tres puntos deben ser nodos de la malla.
        for expected in (-0.25,3.60,6.40):
            self.assertTrue(any(abs(x-expected)<1e-9 for x in x_values),
                            f'falta interseccion x={expected} m')


if __name__=='__main__':
    unittest.main()
