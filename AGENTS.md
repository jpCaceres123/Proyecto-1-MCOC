# Repository Instructions

## Entrypoints and Commands

- `P1L1/` is the organized Semana 1 benchmark; `P1L2/` is reserved for the next laboratory. Do not treat the root as an analysis package.
- Install Semana 1 dependencies with `python -m pip install -r .\P1L1\requirements.txt`.
- Run Semana 1 with `python .\P1L1\model.py`, then generate its plot with `python .\P1L1\plot_model.py`.
- There is no configured root test, lint, formatter, or typecheck command. Treat `P1L1/results/verification.md` and `P1L1/results/verification.json` as the focused analysis check; inspect every `REVISAR` result, not only the process exit code.

## Outputs and Unity

- `P1L1/model.py` overwrites the generated files in `P1L1/results/`: CSV responses, `model.json`, verification files, and `slab_load_distribution.csv`.
- `P1L1/results/model.json` is the geometry/data contract consumed by Unity. Regenerate it before opening the viewer; do not hand-edit generated results.
- Open `P1L1/UnityProject/` in Unity `6000.5.10f1` and load `Assets/Scenes/Frame3D.unity`. The viewer reads `../../results/model.json` relative to `P1L1/UnityProject/Assets` and maps OpenSees global Z to Unity Y.
- `P1L1/plot_model.py` uses hard-coded geometry and members, so geometry changes require updating it separately; it writes `P1L1/results/geometry.png`.

## Structural Invariants

- Use kN, m, radians, and kN/m2 (stress fields in JSON use `kN/m2`); keep units explicit in new JSON fields.
- The global model is linear-elastic 3D with 6 DOF per node. Slabs are represented by tributary beam loads, not finite elements; the current case uses 45-degree two-way distribution and `q = 5 kN/m2`.
- Preserve the meaning of `geomTransf`, local element forces, `Iy`/`Iz`, support conditions, and global/local signs. Validate equilibrium and load conservation after structural changes.
- Do not change structural idealization, tributary-load rules, boundary conditions, local axes, or benchmark/reference checks without documenting and reviewing the justification.

## References

- Requirements and deliverables are in `Enunciados e Instrucciones/`; the week-1 benchmark specification is `Enunciados e Instrucciones/SEMANA_1/Semana_1_LAB_benchmark_3D_OpenSees.txt`.
- `Planos/`, `planos_edificio_ing/`, and CAD/DWG material are reference inputs, not generated source; do not replace originals.
