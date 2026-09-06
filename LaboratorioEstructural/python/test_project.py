import unittest
import json
from pathlib import Path
import numpy as np
import openseespy.opensees as ops
from model import ROOT,rectangle,load_config,read_tables,build_geometry
from solver import create_domain,load_cases,run_case

class MechanicsTests(unittest.TestCase):
    def test_all_column_bases_fixed(self):
        c=load_config();m=build_geometry(c,read_tables(ROOT/c['workbook']))
        nodes={n['id']:n for n in m['nodes']};roots={}
        for e in m['elements']:
            if e['kind']!='COLUMN':continue
            for tag in (e['i'],e['j']):
                n=nodes[tag];key=(n['building'],n['x'],n['y'])
                if key not in roots or n['z']<roots[key]['z']:roots[key]=n
        self.assertEqual(len(roots),29)
        for n in roots.values():
            self.assertTrue(n['fix'],n)
            self.assertFalse(any(n['id'] in d['nodes'] for d in m['diaphragms']))
        self.assertTrue({100301,101301,102301,100451,101451,102451,400501,401501,402501}.issubset(n['id'] for n in roots.values()))

    def test_confirmed_elevated_column_bases(self):
        c=load_config();t=read_tables(ROOT/c['workbook']);m=build_geometry(c,t)
        nodes={n['id']:n for n in m['nodes']}
        for y in [0.,7.25,16.15]:
            column_nodes=[n for n in nodes.values() if abs(n['x']-40)<1e-6 and abs(n['y']-y)<1e-6]
            self.assertAlmostEqual(min(n['z'] for n in column_nodes),3.96)
            base=next(n for n in column_nodes if abs(n['z']-3.96)<1e-6)
            self.assertTrue(base['fix'])
            self.assertTrue(any(e['kind']=='COLUMN' and e['i']==base['id'] for e in m['elements']))
            self.assertFalse(any(base['id'] in d['nodes'] for d in m['diaphragms']))
        self.assertTrue({4,8,12}.isdisjoint(e['source'] for e in m['elements']))
        # Preserve all other members of the source column geometry.
        expected={e['element_id'] for e in t['Elementos'] if e['tipo']=='COLUMN'}-{4,8,12}
        self.assertEqual({e['source'] for e in m['elements'] if e['kind']=='COLUMN'},expected)
        lower_base_ids={401,1401,2401}
        self.assertTrue(lower_base_ids.isdisjoint(nodes))

    def test_cantilever_exact_deflection_and_reaction(self):
        E=30e6;sec=rectangle(.6,.8);L=4.;P=100.
        ops.wipe();ops.model('basic','-ndm',3,'-ndf',6)
        ops.node(1,0,0,0);ops.node(2,L,0,0);ops.fix(1,1,1,1,1,1,1)
        ops.geomTransf('Linear',1,0,-1,0)
        ops.element('elasticBeamColumn',1,1,2,sec['A'],E,E/2.4,sec['J'],sec['Iy'],sec['Iz'],1)
        ops.timeSeries('Linear',1);ops.pattern('Plain',1,1);ops.load(2,0,0,-P,0,0,0)
        ops.constraints('Plain');ops.numberer('Plain');ops.system('BandGeneral');ops.algorithm('Linear');ops.integrator('LoadControl',1);ops.analysis('Static')
        self.assertEqual(ops.analyze(1),0);ops.reactions()
        expected=-P*L**3/(3*E*sec['Iz'])
        self.assertAlmostEqual(ops.nodeDisp(2,3)/expected,1.,places=10)
        self.assertAlmostEqual(ops.nodeReaction(1,3),P,places=8)
        self.assertAlmostEqual(abs(ops.nodeReaction(1,5)),P*L,places=8)

    def test_gravity_profile_quadrature_integral(self):
        # Integrate a symmetric triangular load with exact cubic consistent nodal shape functions.
        L=6.;total=120.;x,w=np.polynomial.legendre.leggauss(3);eq=np.zeros(4)
        for lo,hi in [(0,L/2),(L/2,L)]:
            for g,weight in zip(x,w):
                s=(lo+hi)/2+g*(hi-lo)/2;t=s/L;q=4*total/L**2*min(s,L-s)
                H=np.array([1-3*t*t+2*t**3,L*(t-2*t*t+t**3),3*t*t-2*t**3,L*(-t*t+t**3)])
                eq+=H*q*weight*(hi-lo)/2
        self.assertAlmostEqual(eq[0]+eq[2],total,places=9)
        self.assertAlmostEqual(eq[2]*L+eq[1]+eq[3],total*L/2,places=9)
        self.assertAlmostEqual(eq[0],eq[2],places=9)

    def test_model_and_load_destinations(self):
        c=load_config();t=read_tables(ROOT/c['workbook']);m=build_geometry(c,t);ls,tot=load_cases(m,t,c)
        n={x['id']:x for x in m['nodes']}
        for e in m['elements']:
            self.assertEqual(n[e['i']]['building'],n[e['j']]['building'])
            self.assertGreater(e['L'],1e-6)
            if e['kind'].startswith('BEAM'):self.assertAlmostEqual(e['ey'][2],1,places=7)
        for d in m['diaphragms']:self.assertEqual({n[tag]['building'] for tag in d['nodes']},{d['building']})
        self.assertEqual(len(m['diaphragms']),10)
        # The physical wall faces define 10 cm independently of the analytical centroids.
        walls={w['id']:w for w in m['walls']}
        right_lt2=walls[5]['ax']+walls[5]['thickness']/2
        left_lt1=walls[13]['ax']-walls[13]['thickness']/2
        self.assertAlmostEqual(left_lt1-right_lt2,.10,places=8)
        self.assertTrue(all(cmp['supported'] for cmp in m['components']))
        for name,field in [('G','G_floor'),('Q','Q_floor')]:
            applied=sum(-p[3] for ps in ls[name]['points'].values() for p in ps)+sum(-f[2] for f in ls[name]['nodal'].values())
            self.assertAlmostEqual(applied,tot[field],places=6)

    def test_result_superposition_and_fiber_validation(self):
        v=json.loads((ROOT/'resultados/validacion.json').read_text(encoding='utf8'))
        self.assertLess(v['superposition']['displacements']['relative'],1e-6)
        self.assertLess(v['superposition']['forces']['relative'],1e-6)
        self.assertLess(v['diaphragmError'],1e-8)
        data=json.loads((ROOT/'resultados/proyecto.json').read_text(encoding='utf8'))
        fixed_ids={n['id'] for n in data['model']['nodes'] if n['fix']}
        for case in data['cases']:
            self.assertLess(case['forceResidual'],1e-5);self.assertLess(case['momentResidual'],1e-5)
            for node in case['nodes']:
                if node['id'] in fixed_ids:
                    np.testing.assert_allclose(node['u'],np.zeros(6),atol=1e-12,rtol=0)
            # Endpoint equilibrium of exported diagrams against local end force at j.
            for e in case['elements']:
                p=e['diagram'][-1];f=e['forces']
                actual=np.array([p[k] for k in ['N','Vy','Vz','T','My','Mz']])
                np.testing.assert_allclose(actual,f[6:12],atol=1e-4,rtol=1e-6)
        for cap in data['capacities']:
            self.assertLess(cap['checkCompression']['relative'],.01)
            self.assertGreater(len(cap['mphi']),20)

    def test_dimensions_recompute_stiffness_and_self_weight(self):
        c=load_config();t=read_tables(ROOT/c['workbook']);base=build_geometry(c,t);_,total0=load_cases(base,t,c)
        c['beam_section_multiplier']=1.1;c['column_section_multiplier']=1.1
        changed=build_geometry(c,t);_,total1=load_cases(changed,t,c)
        lookup={e['id']:e for e in changed['elements']}
        for e in base['elements']:
            if e['kind']=='COLUMN' or e['kind'].startswith('BEAM'):
                self.assertAlmostEqual(lookup[e['id']]['A']/e['A'],1.1**2,places=9)
                self.assertAlmostEqual(lookup[e['id']]['Iz']/e['Iz'],1.1**4,places=9)
        self.assertGreater(total1['G_members'],total0['G_members'])
        self.assertAlmostEqual(total1['G_floor'],total0['G_floor'],places=8)

if __name__=='__main__':unittest.main(verbosity=2)
