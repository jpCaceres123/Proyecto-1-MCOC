# Benchmark 3D OpenSeesPy de LT1

Este modelo utiliza un fragmento regular de la primera sección del edificio
(`LT1`), basado en los planos estructurales `2017_67`.

## Fuente e idealización

- Planos: `LT1_PDF/2017_67-102.pdf`, `2017_67-103.pdf`, `2017_67-300.pdf` y
  `2017_67-304.pdf`.
- Fragmento: ejes `1-3` por `E-G` (`E-F-G`), con dos vanos en cada dirección
  horizontal.
- Nodos estructurales en los ejes de centro de columnas y vigas: X=`0.00,
  10.00, 20.00 m` (`E-F=10.00 m`, `F-G=10.00 m`); Y=`0.00, 8.90, 16.15 m`
  (`1-2=8.90 m`, `2-3=7.25 m`).
- El eje `E'` está `0.25 m` fuera del eje `E` y se considera únicamente como
  un offset rígido geométrico; no se utiliza para ubicar los nodos analíticos.
- Niveles: apoyos en la base del modelo en `-7.97 m`, seguido por `-4.01`,
  `-0.05`, `3.91`, `7.87` y `11.83 m`.
- Columnas: `0.70 m x 0.70 m`; vigas principales: `0.60 m x 0.80 m`;
  losa: `0.15 m`.
- La losa no se malló. Su carga se transfiere explícitamente a las vigas
  mediante anchos tributarios y se divide equitativamente entre las dos
  direcciones de vigas.
- Los pisos utilizan diafragmas rígidos. El modelo global es elástico lineal,
  tridimensional y tiene 6 GDL por nodo.
- El piso -1/subterráneo se representa hasta la cota `-7.97 m`; las
  fundaciones no se modelan y la base se representa únicamente mediante
  apoyos fijos.

## Datos confirmados en los planos

- Los planos indican un espesor de losa `e=15 cm` en las plantas seleccionadas.
- Los planos indican columnas `P. 70x70 cm` y vigas principales `V. 60/80 cm`.
- Las elevaciones indican los niveles `-7.97`, `-4.01`, `-0.05`, `+3.91`,
  `+7.87` y `+11.83 m`, con una separación típica de `3.96 m`.
- Las notas estructurales indican una resistencia del hormigón de
  `f'c = 25 MPa` para los elementos de hormigón armado correspondientes.

## Supuestos que deben revisarse

- Peso unitario del hormigón: `24 kN/m3`.
- Terminaciones: `1.50 kN/m2`.
- Carga viva: `2.00 kN/m2`.
- La carga de la losa se divide 50/50 entre las dos direcciones porque el
  benchmark utiliza una idealización explícita de descarga tributaria. No es
  un modelo de elementos finitos de la losa.
- Fuerza lateral del benchmark: `20 kN` por piso y dirección.
- La base de apoyo en `-7.97 m` representa el límite inferior del modelo; no
  representa ni reemplaza el modelamiento de las fundaciones.

Estos valores son supuestos explícitos del benchmark y no afirmaciones sobre
datos que no estén documentados en los planos.

## Archivos principales

- `benchmark_3d.py`: construye y analiza el modelo OpenSeesPy.
- `reference_checks.py`: ejecuta verificaciones independientes simplificadas.
- `visualize_geometry.py`: genera una visualización de la geometría y los ejes.
- `P1L1_report.md`: informe del laboratorio P1L1.
- `DEFENSA_P1L1.md`: guía para la defensa individual.
- `results/benchmark_results.json`: resultados del análisis y verificaciones.
- `results/reference_checks.json`: resultados de las referencias independientes.
- `results/benchmark_geometry.png`: imagen de la geometría del benchmark.
- `export_unity_model.py`: exporta el contrato JSON para reconstruir el modelo
  en Unity.
- `results/unity_model.json`: nodos, elementos, secciones, apoyos, diafragmas,
  ejes locales y conversión de coordenadas para Unity.

## Ejecución

Desde la carpeta raíz del proyecto:

```powershell
python .\P1L1\Avances\Avances_P1L1\benchmark_3d.py
python .\P1L1\Avances\Avances_P1L1\reference_checks.py
python .\P1L1\Avances\Avances_P1L1\visualize_geometry.py
python .\P1L1\Avances\Avances_P1L1\export_unity_model.py
```

Los resultados se escriben en `P1L1/Avances/Avances_P1L1/results/`.

El archivo `unity_model.json` sigue la convención del tutorial de Unity:
OpenSees utiliza `(X,Y,Z)` y Unity recibe `(X,Z,Y)`, es decir, `X` permanece
horizontal, `Z` de OpenSees pasa a ser `Y` vertical en Unity y `Y` de OpenSees
pasa a ser `Z` de Unity. Unity puede reconstruir el modelo desde este archivo
sin que la escena sea la fuente única de verdad.

La herramienta de losas bidireccionales está organizada separadamente en
`P1L1/Avances/losas_bidireccionales/`.

## Verificaciones implementadas

- Equilibrio entre cargas aplicadas y reacciones.
- Conservación de la carga transferida desde la losa mediante áreas tributarias.
- Desplazamientos de un nodo de referencia.
- Fuerzas locales de todos los elementos.
- Momento y fuerzas del elemento de referencia.
- Superposición lineal: `GQ = G + Q`.

## Convención de unidades

- Longitud: `m`.
- Fuerza: `kN`.
- Esfuerzo: `kPa`.
- Momento: `kN*m`.
