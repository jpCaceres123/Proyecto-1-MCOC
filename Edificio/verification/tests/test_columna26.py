"""Checks for reference units and the saved-results campus refresh (no solver)."""
import csv
import json
from pathlib import Path
import unittest
import numpy as np

ROOT=Path(__file__).resolve().parents[2]
FIELDS=('n','vy','vz','t','my','mz')


class Column26Tests(unittest.TestCase):
    def test_recalculated_cases_and_clear_span_hypothesis(self):
        report=json.loads((ROOT/'documentation/comparacion_columna26/verificacion_caras.json').read_text(encoding='utf-8'))
        self.assertAlmostEqual(report['clear_length_m'],3.16)
        for check in report['fresh_solver_checks']:
            self.assertTrue(check['stored_reproduction_pass'])
            self.assertTrue(check['equilibrium_force_pass'])
        self.assertAlmostEqual(report['model_face_axial_drop_kN'],report['clear_span_self_weight_kN'],places=8)
        self.assertAlmostEqual(report['model_face_axial_drop_kN'],report['reference_axial_drop_kN'],delta=.001)
        self.assertEqual(report['incident_beams']['i'],[])

    def test_reference_units_and_station_equilibrium(self):
        with (ROOT/'data/references/columna26_C9_foto.csv').open(encoding='utf-8',newline='') as stream:
            rows=list(csv.DictReader(stream))
        self.assertEqual(int(rows[0]['P_N'])/1000,-3556.504)
        self.assertEqual(int(rows[0]['M2_Nmm'])/1e6,-10.254164)
        for i,j in (rows[:2],rows[2:]):
            length=int(j['estacion_mm'])/1000
            for moment,shear in (('M2_Nmm','V3_N'),('M3_Nmm','V2_N')):
                difference=(int(j[moment])-int(i[moment]))/1e6
                self.assertAlmostEqual(difference,-int(i[shear])/1000*length,delta=.002)

    def test_all_exported_bars_match_current_station_results(self):
        for project in ('CampusPlayable','CampusCardboard'):
            path=ROOT/f'visualization/unity/{project}/Assets/Resources/inspeccion_estructural.json'
            data=json.loads(path.read_text(encoding='utf-8'))
            cases={}
            for entry in data['entries']:
                if not entry['key'].startswith('E:'):continue
                tag=entry['key'][2:]
                for case in entry['cases']:
                    name=case['name']
                    if name not in cases:
                        cases[name]=json.loads((ROOT/f'results/{name}_diagramas_barras.json').read_text(encoding='utf-8'))
                    if tag not in cases[name]:
                        self.assertNotIn('endI',case)
                        self.assertNotIn('graphs',case)
                        continue
                    original=cases[name][tag]
                    for field,graph in zip(FIELDS,case['graphs']):
                        self.assertEqual(graph['values'],original[field])
                        self.assertEqual(graph['stations'],original['s'])

    def test_id26_end_actions_and_sections_have_distinct_signs(self):
        for name in ('G','Q'):
            f=json.loads((ROOT/f'results/{name}_fuerzas_locales.json').read_text())['26']
            b=json.loads((ROOT/f'results/{name}_diagramas_barras.json').read_text())['26']
            np.testing.assert_allclose([b[k][0] for k in FIELDS],[f[0],*[-x for x in f[1:6]]])
            np.testing.assert_allclose([b[k][-1] for k in FIELDS],[-f[6],*f[7:12]])
            self.assertGreater(b['n'][0],0)
            self.assertGreater(b['n'][-1],0)


if __name__=='__main__':unittest.main()
