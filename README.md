# Benchmark 3D OpenSeesPy

Este repositorio corresponde al proyecto colaborativo P1 de MCOC. El contexto original se encuentra en [`README/README.md`](README/README.md).

Caso reproducible para P1L1: marco de un vano en cada dirección, con losa descargando sobre las vigas.

## Ejecución

```text
python -m pip install -r requirements.txt
python model.py
python plot_model.py
```

`model.py` crea `results/nodal_displacements.csv`, `results/reactions.csv`, `results/element_forces.csv` y `results/verification.md`. `plot_model.py` crea `results/geometry.png`.

## Caso adoptado

- Planta: `6.0 m x 5.0 m`; altura: `3.0 m`.
- Cuatro columnas en `(0,0)`, `(6,0)`, `(6,5)`, `(0,5)`.
- Columnas: `0.30 x 0.30 m`; vigas: `0.25 x 0.40 m`.
- Hormigón elástico: `E = 25 GPa`, `nu = 0.20`, `G = E/[2(1+nu)]`.
- Los cuatro apoyos inferiores son empotrados en los 6 GDL.
- La losa se representa mediante `q = 5 kN/m2`, incluyendo peso propio y sobrecarga.
- Ancho tributario: `2.5 m` para las vigas de 6 m y `3.0 m` para las vigas de 5 m.
- Por tanto, `w_x = 12.5 kN/m` y `w_y = 15.0 kN/m`; carga total = `300 kN` hacia `-Z`.

El archivo `results/verification.md` es el artefacto que debe enlazarse en Canvas después de ejecutar el modelo.

## Defensa

Cada nodo tiene `[ux, uy, uz, rx, ry, rz]`. `geomTransf` define los ejes locales del elemento; las fuerzas de `eleResponse(..., 'localForce')` están en esos ejes, mientras las cargas nodales y reacciones se revisan en ejes globales. `Iy` y `Iz` son las inercias respecto de los ejes locales y controlan la flexión. OpenSees resuelve el equilibrio estático `K u = P` y luego recupera fuerzas. Que el análisis termine solo demuestra convergencia numérica, no que las unidades, conectividad, signos, apoyos o magnitudes sean correctos.

## Visualización Unity

El proyecto Unity se encuentra en `UnityProject/`. Abrir esa carpeta desde Unity Hub y cargar `Assets/Scenes/Frame3D.unity`. La guía paso a paso está en [`UnityProject/GUIA_INICIO.md`](UnityProject/GUIA_INICIO.md).

## Flujo de trabajo

1. Crear una rama para cada tarea: `git switch -c nombre-de-la-tarea`.
2. Guardar cambios con un commit descriptivo.
3. Subir la rama y abrir un Pull Request para revisión.
4. Integrar los cambios a `main` después de revisarlos.
