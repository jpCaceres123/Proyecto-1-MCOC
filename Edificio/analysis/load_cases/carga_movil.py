"""Public SQ4 entrypoint. Units: kN, m, rad; all slab panels, schema 2."""
from pathlib import Path
import sys
import numpy as np

ROOT=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(ROOT/'model/opensees'))
import modelo_opensees_3d as model

# Kept for the independent cubic beam benchmark.
POSITIONS=np.array([0.,1./3.,2./3.,1.])
def weights(s):
    return np.array([np.prod([(s-b)/(a-b) for j,b in enumerate(POSITIONS) if j!=i]) for i,a in enumerate(POSITIONS)])

def generate(cfg=None):
    from carga_movil_global import generate as generate_all
    return generate_all(cfg)

if __name__=='__main__':generate()
