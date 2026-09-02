# Benchmark 3D OpenSeesPy

Este laboratorio corresponde a la Semana 1 del proyecto P1 de MCOC. El contexto general se encuentra en [`Contexto_Proyecto.md`](../Contexto_Proyecto.md).

Caso reproducible para P1L1: marco de un vano en cada dirección, con losa descargando sobre las vigas.

## Ejecución

```text
python -m pip install -r .\P1L1\requirements.txt
python .\P1L1\model.py
python .\P1L1\plot_model.py
```

`model.py` crea `results/nodal_displacements.csv`, `results/reactions.csv`, `results/element_forces.csv`, `results/model.json`, `results/verification.json` y `results/verification.md`. `plot_model.py` crea `results/geometry.png`.

## Caso adoptado

- Planta: `6.0 m x 5.0 m`; altura: `3.0 m`.
- Cuatro columnas en `(0,0)`, `(6,0)`, `(6,5)`, `(0,5)`.
- Columnas: `0.30 x 0.30 m`; vigas: `0.25 x 0.40 m`.
- Hormigón elástico: `E = 25 GPa`, `nu = 0.20`, `G = E/[2(1+nu)]`.
- Los cuatro apoyos inferiores son empotrados en los 6 GDL.
- La losa se representa mediante `q = 5 kN/m2`, incluyendo peso propio y sobrecarga.
- La losa se considera bidireccional con líneas de tributación a 45 grados.
- Las vigas de `6 m` reciben cargas trapezoidales: `0 -> 12.5 -> 12.5 -> 0 kN/m`.
- Las vigas de `5 m` reciben cargas triangulares: `0 -> 12.5 -> 0 kN/m`.
- La carga total de losa es `5 kN/m2 * 6 m * 5 m = 150 kN` hacia `-Z`.

El archivo `results/verification.md` es el artefacto que debe enlazarse en Canvas después de ejecutar el modelo.

`results/model.json` es el contrato de geometría para Unity. Debe regenerarse con `python .\P1L1\model.py` antes de abrir la escena; contiene unidades explícitas, nodos, elementos y apoyos.

## Defensa

Cada nodo tiene `[ux, uy, uz, rx, ry, rz]`. `geomTransf` define los ejes locales del elemento; las fuerzas de `eleResponse(..., 'localForce')` están en esos ejes, mientras las cargas nodales y reacciones se revisan en ejes globales. `Iy` y `Iz` son las inercias respecto de los ejes locales y controlan la flexión. OpenSees resuelve el equilibrio estático `K u = P` y luego recupera fuerzas. Que el análisis termine solo demuestra convergencia numérica, no que las unidades, conectividad, signos, apoyos o magnitudes sean correctos.

## Visualización Unity

El proyecto Unity se encuentra en `UnityProject/`. Abrir esa carpeta desde Unity Hub y cargar `Assets/Scenes/Frame3D.unity`. La guía paso a paso está en [`UnityProject/GUIA_INICIO.md`](UnityProject/GUIA_INICIO.md).

## Comparación SAP2000

Los archivos para reproducir la geometría en SAP2000 están en `sap2000/`:

- `sap2000/sap2000_marco_3d.dxf`: geometría 3D importable como frames.
- `sap2000/sap2000_build_model.py`: script para crear el modelo usando la API de SAP2000.

Los prototipos y avances históricos de P1L1 están en [`Avances/`](Avances/).

La losa bidireccional se modela con dos cargas triangulares en las vigas de `5 m` y dos cargas trapezoidales en las vigas de `6 m`, con intensidad máxima `12.5 kN/m`.

## Flujo de trabajo

1. Crear una rama para cada tarea: `git switch -c nombre-de-la-tarea`.
2. Guardar cambios con un commit descriptivo.
3. Subir la rama y abrir un Pull Request para revisión.
4. Integrar los cambios a `main` después de revisarlos.
