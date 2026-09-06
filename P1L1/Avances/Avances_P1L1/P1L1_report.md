# P1L1 - Benchmark 3D OpenSeesPy

## Objective

Build and verify a reproducible three-dimensional structural benchmark based
on a regular fragment of LT1 from the `2017_67` structural drawings.

The selected reference sheets are `2017_67-102.pdf` and `2017_67-103.pdf`
(floor plans), `2017_67-300.pdf` (elevations and levels), and
`2017_67-304.pdf` (elevation dimensions, including the `E'-E=0.25 m` offset
and the `E-F=10.00 m`, `F-G=10.00 m` centerline spans).

## Model definition

- OpenSeesPy model: 3D, 6 DOF per node, linear elastic.
- Fragment: axes `1-3` by `E-G` (`E-F-G`), with two bays in each direction.
- Structural centerline coordinates: X=`0.00, 10.00, 20.00 m`
  (`E-F=10.00 m`, `F-G=10.00 m`); Y=`0.00, 8.90, 16.15 m`
  (`1-2=8.90 m`, `2-3=7.25 m`).
- `E'` is offset `0.25 m` from `E` and is represented only as a rigid
  geometric offset, not as a structural axis.
- Levels: `-7.97, -4.01, -0.05, 3.91, 7.87, 11.83 m`.
- Columns: `0.70 x 0.70 m`; beams: `0.60 x 0.80 m`; slab: `0.15 m`.
- The slab is not meshed. Its gravity load is transferred to beams by
  explicit tributary widths, with a 50/50 two-way split.
- Floors use rigid diaphragms. The base is fixed at `-7.97 m`; foundations are
  not modeled.

## Loads and cases

| Case | Definition |
|---|---|
| `G` | slab self-weight plus finishes |
| `Q` | live load |
| `GQ` | `G + Q` |
| `EX` | 20 kN at each occupied floor in global X |
| `EY` | 20 kN at each occupied floor in global Y |

Concrete strength `f'c=25 MPa` and the section dimensions are supported by
the selected plans. Concrete unit weight, finishes, live load, lateral force,
two-way split, and the fixed-base truncation are explicit benchmark
assumptions.

## Outputs

- `benchmark_results.json`: nodal displacements, support reactions, local
  forces for all elements, resultants, and verification residuals.
- `reference_checks.json`: independent resultant checks and simplified
  screening estimates for displacement, shear, and beam end moment.
- `benchmark_geometry.png`: simple geometry and global-axis visualization.

## Verification results

| Check | Maximum residual |
|---|---:|
| Equilibrium | `2.76e-10` |
| `R(G+Q)-R(G)-R(Q)` | `1.09e-11` |
| `u(G+Q)-u(G)-u(Q)` | `1.06e-16 m` |
| Tributary load conservation | exact by construction |

The equilibrium residual combines force units (kN) and moment units (kN*m)
in the exported six-component resultant and is interpreted componentwise.

## Local axes and element forces

Columns use a local reference vector in global X because their axis is global
Z. Beams use a global-Z reference vector. The `localForce` output follows the
12-component OpenSees beam-column convention: end 1 forces/moments followed
by end 2 forces/moments. `Iy` and `Iz` are the section second moments about
the local y and z axes.

## Reproduction

```powershell
python .\P1L1\Avances\Avances_P1L1\benchmark_3d.py
python .\P1L1\Avances\Avances_P1L1\reference_checks.py
python .\P1L1\Avances\Avances_P1L1\visualize_geometry.py
```

## Limitations

The simplified lateral displacement and simply-supported beam moment in
`reference_checks.json` are screening estimates, not exact analytical
  solutions of the rigid-diaphragm frame. The foundation below `-7.97 m` is not
modeled. These limitations must be explained during the live demonstration.
