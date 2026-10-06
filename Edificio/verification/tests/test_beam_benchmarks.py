"""Independent subdivided OpenSees benchmarks; no building files are written."""
import unittest
import openseespy.opensees as ops


class BeamBenchmarks(unittest.TestCase):
    def solve(self, cantilever=False, point=False):
        ops.wipe(); ops.model('basic', '-ndm', 2, '-ndf', 3)
        L, count, E, I, q, P = 6., 40, 3e7, .0256, 10., 60.
        for k in range(count+1): ops.node(k+1, L*k/count, 0)
        ops.fix(1, 1, 1, int(cantilever))
        if not cantilever: ops.fix(count+1, 0, 1, 0)
        ops.geomTransf('Linear', 1)
        for k in range(count): ops.element('elasticBeamColumn', k+1, k+1, k+2, .48, E, I, 1)
        ops.timeSeries('Linear', 1); ops.pattern('Plain', 1, 1)
        if point: ops.load(count//2+1, 0, -P, 0)
        else:
            for k in range(count): ops.eleLoad('-ele', k+1, '-type', '-beamUniform', -q)
        ops.constraints('Plain'); ops.numberer('Plain'); ops.system('BandGeneral')
        ops.algorithm('Linear'); ops.integrator('LoadControl', 1); ops.analysis('Static')
        self.assertEqual(ops.analyze(1), 0)
        ops.reactions()
        self.assertAlmostEqual(ops.nodeReaction(1, 2)+(0 if cantilever else ops.nodeReaction(count+1, 2)), P if point else q*L, places=6)
        values = [abs(ops.eleResponse(k+1, 'localForce')[2]) for k in range(count)]
        if cantilever:
            self.assertAlmostEqual(values[0], q*L**2/2, places=6)
            self.assertAlmostEqual(-ops.nodeDisp(count+1, 2), q*L**4/(8*E*I), places=9)
        else:
            self.assertAlmostEqual(values[count//2], P*L/4 if point else q*L**2/8, places=6)
            self.assertAlmostEqual(-ops.nodeDisp(count//2+1, 2), P*L**3/(48*E*I) if point else 5*q*L**4/(384*E*I), places=9)
        ops.wipe()

    def test_uniform_simply_supported(self): self.solve()
    def test_point_load_piecewise_linear_moment(self): self.solve(point=True)
    def test_cantilever_uniform(self): self.solve(cantilever=True)


if __name__ == '__main__': unittest.main()
