# Project

Digital structural laboratory of the Edificio de Ingenieria (Universidad de los Andes).
7-week course project combining OpenSees/OpenSeesPy analysis with Unity visualization and AR.

# Units

SI throughout. Forces in kN, lengths in meters, stresses in kPa. Every JSON field must have explicit units or a single global convention.

# Structural model

- Global model: linear elastic 3D, 6 DOF per node.
- Slabs are NOT modeled with finite elements.
- Floor gravity load = slab self-weight + uniform finishes (q_G).
- Slab-to-beam transfer must use tributary areas explicitly.
- RC capacity analysis (Fiber Sections) is SEPARATE from the global model.
- Load cases: G (gravity), Q (live), EX (lateral X), EY (lateral Y).
- Superposition must be verified numerically: R(A+B) = R(A) + R(B).

# Architecture

- OpenSees/OpenSeesPy owns structural analysis.
- Unity owns visualization, preprocessing, and interaction.
- JSON is the contract between OpenSees and Unity.
- Mobile does NOT run OpenSees in the base project.
- Geometry and structural data must exist in formats independent of the Unity scene.
- RC capacity (Fiber Sections, M-phi, P-M curves) runs separately from the global elastic model.

# Repository layout

- `planos_edificio_ing/` — original building CAD plans (DWG format, read-only reference).
- `Planos/` — structural calculation plans and specialties (DWG/PDF).
- `*.txt` — project specification documents (authoritative for requirements).

# Source documents

- `Enunciado_General.txt` — full project scope, required deliverables, weekly schedule.
- `P1_Sidequests.txt` — optional bonus features (SQ1-SQ4).
- `P1_Honors_Track.txt` — extra-credit objectives (H1-H5).
- `P1_Recursos_tecnicos_y_estrategia_de_trabajo_con_IA.txt` — AI usage strategy and review agents.
- `Cronograma.txt` — weekly milestones and grading breakdown.

# Verification rules

- Check equilibrium: sum(F_applied) + sum(R) ≈ 0.
- Check units on every JSON field.
- Check local axes orientation.
- Check superposition numerically.
- Every exported elementTag must exist exactly once in the viewer.
- Never modify reference benchmark results without justification.
- Tribute area load transfer must conserve: sum(transferred loads) = q * A.

# What NOT to delegate without human review

- Structural idealization choices.
- Local axis meaning.
- Tributary area distribution.
- Support/boundary conditions.
- Capacity criteria and P-M interpretation.
- AR physical alignment rules.
- Load transfer rule for SQ4 (mobile user load).

# Workflow

- Use the cycle: Issue -> Plan -> Build -> Test -> Review -> Merge.
- Agent prompts should be specific: state objective, constraints, acceptance criteria, and how to verify.
- Bad prompt example: "Make the tributary area tool."
- Good prompt example: "Implement reading tributary_areas.json. Do not modify the schema. Verify that sum of transferred loads equals q*A within tolerance 1e-10."

# OpenSees key references

- OpenSeesPy docs: https://openseespydoc.readthedocs.io/
- elasticBeamColumn: https://openseespydoc.readthedocs.io/en/latest/src/elasticBeamColumn.html
- geomTransf: https://openseespydoc.readthedocs.io/en/latest/src/geomTransf.html
- rigidDiaphragm: https://openseespydoc.readthedocs.io/en/latest/src/rigidDiaphragm.html
- Fiber Section: https://openseespydoc.readthedocs.io/en/latest/src/fibersection.html
- DisplacementControl: https://openseespydoc.readthedocs.io/en/latest/src/displacementControl.html
- Element Recorder: https://opensees.github.io/OpenSeesDocumentation/user/manual/output/ElementRecorder.html
