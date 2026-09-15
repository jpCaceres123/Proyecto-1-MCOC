"""Controles del contrato de postproceso y equilibrio de esfuerzos interiores."""
import json
from pathlib import Path
import unittest

import numpy as np

ROOT = Path(__file__).resolve().parents[2]


class PostprocessTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.data = json.loads((ROOT / 'visualization/unity/UnityVisualization/Assets/Resources/semana4_resultados.json').read_text())
        cls.xyz = {n['id']: np.array(n['xyz']) for n in cls.data['nodes']}
        cls.model = json.loads((ROOT / 'results/modelo_3d_manual.json').read_text())
        cls.r_forces = json.loads((ROOT / 'results/R_fuerzas.json').read_text())

    def test_exported_axes_and_connectivity(self):
        for b in self.data['bars']:
            basis = np.array([b['x'], b['y'], b['z']])
            np.testing.assert_allclose(basis @ basis.T, np.eye(3), atol=1e-10)
            self.assertAlmostEqual(np.linalg.det(basis), 1)
            delta = self.xyz[b['j']] - self.xyz[b['i']]
            np.testing.assert_allclose(b['x'], delta / np.linalg.norm(delta), atol=1e-10)
        for shell in self.data['shells']:
            self.assertEqual(len(set(shell['nodes'])), 4)
            self.assertTrue(set(shell['nodes']) <= self.xyz.keys())

    def test_all_six_dofs_match_saved_analysis(self):
        for case in self.data['cases']:
            with np.load(ROOT / 'results' / (case['name'] + '.npz')) as result:
                self.assertEqual([n['id'] for n in case['nodes']], result['node_tags'].tolist())
                np.testing.assert_array_equal([n['u'] + n['r'] for n in case['nodes']], result['u'])

    def test_element_metadata_and_wall_demands_are_traceable(self):
        metadata = {item['id']: item for item in self.data['elementMetadata']}
        self.assertEqual(len(metadata), len(self.data['bars']))
        for bar in self.data['bars']:
            item = metadata[bar['id']]
            self.assertEqual((item['i'], item['j']), (bar['i'], bar['j']))
            self.assertIn(item['section'], ('section_columns', 'section_beams',
                                            'section_small_beams', 'section_variable_beams',
                                            'section_40x60_beams', 'section_steel_columns'))
            self.assertIn(item['material'], ('material', 'material_steel'))
            self.assertEqual(set(item['restraints']), {'i', 'j'})
        self.assertGreaterEqual(len(self.data['wallDemands']), 24)
        for wall in self.data['wallDemands']:
            self.assertEqual({d['name'] for d in wall['demands']}, {'G', 'Q', 'EX', 'EY', 'R'})
            for demand in wall['demands']:
                self.assertTrue(np.isfinite([demand['p'], demand['m']]).all())

    def test_member_cut_equilibrium_for_every_case(self):
        # No member loads: N,V,T are constant. On the +x face,
        # dMy/dx=Vz and dMz/dx=-Vy. This independently checks the end sign rule.
        for case in self.data['cases']:
            forces = json.loads((ROOT / 'results' / (case['name'] + '_fuerzas_locales.json')).read_text())
            for b in self.data['bars']:
                f = np.array(forces[str(b['id'])])
                length = np.linalg.norm(self.xyz[b['j']] - self.xyz[b['i']])
                np.testing.assert_allclose(f[:4] + f[6:10], 0, atol=1e-5)
                self.assertAlmostEqual(f[10] + f[4], -f[2] * length, delta=1e-4)
                self.assertAlmostEqual(f[11] + f[5], f[1] * length, delta=1e-4)

    def test_wall_10_demand_matches_shell_base_resultant(self):
        wall = min((w for w in self.model['walls'] if w.get('source_wall_id') == 10),
                   key=lambda w: w['z_i_m'])
        shells = [s for s in self.data['shells'] if s['wall'] == wall['id']]
        bottom_z = min(min(self.xyz[node][2] for node in shell['nodes']) for shell in shells)
        bottom = [shell for shell in shells
                  if min(self.xyz[node][2] for node in shell['nodes']) <= bottom_z + 1e-8]
        start = np.array([wall['x_i_m'], wall['y_i_m'], bottom_z])
        end = np.array([wall['x_j_m'], wall['y_j_m'], bottom_z])
        longitudinal = end - start
        longitudinal[2] = 0.0
        longitudinal /= np.linalg.norm(longitudinal)
        transverse = np.cross([0.0, 0.0, 1.0], longitudinal)
        center = (start + end) / 2.0
        force = np.zeros(3)
        moment = np.zeros(3)
        for shell in bottom:
            values = self.r_forces[str(shell['id'])]
            for index, node in enumerate(shell['nodes']):
                if abs(self.xyz[node][2] - bottom_z) > 1e-8:
                    continue
                force += values[index * 6:index * 6 + 3]
                moment += np.cross(self.xyz[node] - center, values[index * 6:index * 6 + 3])
                moment += values[index * 6 + 3:index * 6 + 6]
        expected = next(w for w in self.data['wallDemands'] if w['id'] == wall['id'])
        demand = next(d for d in expected['demands'] if d['name'] == 'R')
        self.assertAlmostEqual(demand['p'], force[2], places=8)
        self.assertAlmostEqual(demand['m'], abs(np.dot(moment, transverse)), places=8)


if __name__ == '__main__':
    unittest.main()
