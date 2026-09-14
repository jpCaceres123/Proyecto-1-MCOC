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


if __name__ == '__main__':
    unittest.main()
