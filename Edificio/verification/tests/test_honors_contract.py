import copy
import importlib.util
import json
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location('contracts', ROOT/'analysis/contracts.py')
contracts = importlib.util.module_from_spec(spec)
spec.loader.exec_module(contracts)


class ContractTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.data = json.loads((ROOT/'visualization/unity/UnityVisualization/Assets/Resources/semana4_resultados.json').read_text(encoding='utf-8'))

    def test_base_contract(self):
        self.assertGreater(contracts.validate(self.data), 600)

    def test_reject_nonfinite_and_missing_stations(self):
        for value in (float('nan'), float('inf')):
            data = copy.deepcopy(self.data)
            data['cases'][0]['bars'][0]['my'][0] = value
            with self.assertRaises(ValueError): contracts.validate(data)
        data = copy.deepcopy(self.data)
        data['cases'][0]['bars'][0]['s'][1] = 0
        with self.assertRaises(ValueError): contracts.validate(data)

    def test_campus_adapter_preserves_numbers(self):
        delivered = contracts.campus(self.data)
        original = self.data['cases'][0]['bars'][0]
        graphs = delivered['entries'][0]['cases'][0]['graphs']
        for f, graph in zip(contracts.FIELDS, graphs):
            self.assertEqual(original[f], graph['values'])
            self.assertEqual(original['s'], graph['stations'])


if __name__ == '__main__': unittest.main()
