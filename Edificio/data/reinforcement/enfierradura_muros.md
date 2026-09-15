# Registro de enfierradura de muros

Registro progresivo de la ubicacion y armadura identificada en elevaciones
estructurales. Las separaciones estan en centimetros y corresponden a doble
malla, salvo que se indique lo contrario.

## Criterio de registro

El registro se realizo revisando las elevaciones estructurales por eje y por
nivel. Para cada muro se identificaron su ubicacion entre ejes, espesor y
armadura principal. Cuando una columna intermedia divide un muro, se registran
los paños por separado; cuando aparece una junta de dilatacion, se registra como
una separacion o vacio y no como un muro continuo.

Las indicaciones `D.M.H.` y `D.M.V.` se transcriben como doble malla horizontal
y vertical, respectivamente. Cuando el plano solo indica `D.M.`, se interpreta
preliminarmente como la misma malla en ambas direcciones. Los refuerzos
localizados de borde, trabas, confinamiento o lado tierra se anotan aparte para
no mezclarlos con la malla principal. Las longitudes se expresan como tramos
entre ejes; las cotas parciales se conservan como referencias de ubicacion.

La armadura se separa por grupos de pisos cada vez que cambia el diametro, la
separacion o el espesor del muro. Una lectura que no es completamente legible
se marca como `Por confirmar` y no se incorpora como dato definitivo para una
Fiber Section hasta verificarla en otra vista, planta o detalle constructivo.

## Tabla de muros

| Eje | Identificacion | Nivel | Ubicacion | Tramo entre ejes / referencia | Espesor [cm] | Armadura horizontal | Armadura vertical | Refuerzos adicionales | Estado |
|---|---|---|---|---:|---:|---|---|---|---|
| `1-1'` | `M1` | `1°S` | Entre E-F | Por confirmar | 20 | D.M. `Ø8@20` | D.M. `Ø8@20` | Por confirmar | Registrado |
| `1-1'` | `M1` | Piso 1 | Entre H-I' | Por confirmar | 30 | D.M. `Ø12@16` | D.M. `Ø10@20` | Por confirmar | Registrado |
| `1''` | `M-1''-Eb/Ec` | `1°S` | Entre Eb-Ec | Por confirmar | 20 | D.M. `Ø10@12` | D.M. `Ø10@20` | Por confirmar | Registrado |
| `1''` | `M-1''-E/Ec` | Pisos 1 a 4 | Entre E-Ec | Por confirmar | 20 | D.M. `Ø10@12` | D.M. `Ø10@20` | Por confirmar | Registrado |
| `I` | `M-I-1/2` | Piso 1 | Entre ejes 1-2 | Por confirmar | 30 | D.M. `Ø12@20` | D.M. `Ø16@20` | Por confirmar | Registrado |
| `I` | `M-I-2/3` | Piso 1 | Entre ejes 2-3 | Por confirmar | 30 | D.M. `Ø12@20` | D.M. `Ø16@20` | Por confirmar | Registrado |
| `F-F'` | `M-F-F'-1/2` | `1°S` | Entre ejes 1-2 | Por confirmar | 30 | D.M. `Ø12@20` | D.M. `Ø16@20` | Refuerzos laterales por confirmar | Registrado |
| `F-F'` | `M-F-F'-2/3` | `1°S` | Entre ejes 2-3 | Por confirmar | 30 | D.M. `Ø12@20` | D.M. `Ø16@20` | Refuerzos laterales por confirmar | Registrado |
| `1''` | `M-1''-E/Ec` | Elevación eje `1''`, pisos 1 a 4 | Entre E' y Ec | Por confirmar | 20 | D.M. `Ø10@20` | D.M. `Ø10@12` | Detalle lateral `+TØ8@24`; barras longitudinales de borde por confirmar | Registrado |
| `2a` | `M-2a-Ea/Ed` | `1°S` a piso 4 | Entre Ea-Ed | Por confirmar | 20 | D.M. `Ø10@20` | D.M. `Ø10@20` | Bordes visibles por confirmar | Registrado |
| `Ea` | `M-Ea-2a/2` | `1°S` | Desde 2a, 1.579 m hacia 2 | 1.579 | 25 | D.M. `Ø10@16` | D.M. `Ø8@16` | `2Ø28` de borde y otros por confirmar | Registrado |
| `Ea` | `M-Ea-2a/2` | Piso 1 | Desde 2a, 1.579 m hacia 2 | 1.579 | 25 | D.M. `Ø10@16` | D.M. `Ø8@16` | `2Ø28` de borde y otros por confirmar | Registrado |
| `Ea` | `M-Ea-2a/2` | Piso 2 | Desde 2a, 1.579 m hacia 2 | 1.579 | 25 | D.M. `Ø10@16` | D.M. `Ø8@16` | `2Ø28` de borde y otros por confirmar | Registrado |
| `Ea` | `M-Ea-2a/2` | Pisos 3 y 4 | Desde 2a, 1.579 m hacia 2 | 1.579 | 25 | D.M. `Ø8@16` | D.M. `Ø8@16` | `2Ø28` de borde y otros por confirmar | Registrado |
| `Ec` | `M-Ec-1''/2` | `1°S` y piso 1 | Desde 1'', 2.25 m hacia 2 | 2.25 | 30 | D.M. `Ø10@16` | D.M. `Ø10@16` | Por confirmar | Registrado |
| `Ec` | `M-Ec-1''/2` | Pisos 2 a 4 | Desde 1'', 2.25 m hacia 2 | 2.25 | 30 | D.M. `Ø10@20` | D.M. `Ø10@20` | Por confirmar | Registrado |
| `Ed` | `M-Ed-2a/2` | `1°S` a piso 2 | Desde 2a, 1.579 m hacia 2 | 1.579 | 25 | D.M. `Ø10@16` | D.M. `Ø8@16` | `2Ø28` de borde y otros por confirmar | Registrado |
| `Ed` | `M-Ed-2a/2` | Pisos 3 y 4 | Desde 2a, 1.579 m hacia 2 | 1.579 | 25 | D.M. `Ø8@16` | D.M. `Ø8@16` | `2Ø25` de borde y otros por confirmar | Registrado |
| `E-E'` | `M-E-E'-3/2` | `1°S` subterráneo | Entre ejes 3-2 | Por confirmar | 20 | D.M. `Ø8@20` | D.M. `Ø10@20` | Por confirmar | Registrado |
| `E-E'` | `M-E-E'-2/1` | `1°S` subterráneo | Entre ejes 2-1 | Por confirmar | 20 | D.M. `Ø8@20` | D.M. `Ø10@16` | Por confirmar | Registrado |
| `E-E'` | `Junta de dilatacion` | `1°S` subterráneo | Separacion del muro hacia eje 1'' | Por confirmar | Por confirmar | No aplica | No aplica | Debe modelarse como vacio/separacion | Por confirmar |
| `LT2 / 1` | `M-LT2-1-A'` | `1°S` subterráneo | Muro en A': tramo horizontal y retorno perpendicular | 1.85; retorno 2.92 | 30; retorno 60 | D.M. `Ø12@10` | D.M. `Ø12@20` | `3Ø28` longitudinales en borde; estribos/refuerzos por confirmar | Registrado |
| `LT2 / 1` | `M-LT2-1-A'` | Piso 1 | Muro en A': tramo horizontal y retorno perpendicular | 1.85; retorno 2.92 | 30; retorno 60 | D.M. `Ø12@20` | D.M. `Ø12@20` | `3Ø28` longitudinales en borde; estribos/refuerzos por confirmar | Registrado |
| `LT2 / 1` | `M-LT2-1-A'` | Piso 2 | Muro en A': tramo horizontal y retorno perpendicular | 1.85; retorno 2.92 | 30; retorno 60 | D.M. `Ø10@20` | D.M. `Ø10@20` | `3Ø22` longitudinales en borde; longitudes y confinamiento por confirmar | Registrado |
| `LT2 / 1` | `M-LT2-1-A'` | Pisos 3 y 4 | Muro en A': tramo horizontal y retorno perpendicular | 1.85; retorno 2.92 | 30; retorno 60 | D.M. `Ø10@20` | D.M. `Ø10@20` | `3Ø22` longitudinales en borde; `EØ10@20` y trabas por confirmar | Registrado |
| `LT2 / 1'` | `M-LT2-1'-C'/D'` | `1°S` subterráneo | Entre ejes C'-D' | Por confirmar | 30 | D.M. `Ø12@10` | D.M. `Ø12@20` | `3Ø22` longitudinales en borde y otros por confirmar | Registrado |
| `LT2 / 1'` | `M-LT2-1'-C'/D'` | Piso 1 | Entre ejes C'-D' | Por confirmar | 30 | D.M. `Ø12@10` | D.M. `Ø12@20` | Refuerzos de borde por confirmar | Registrado |
| `LT2 / 1'` | `M-LT2-1'-C'/D'` | Piso 2 | Entre ejes C'-D' | Por confirmar | 30 | D.M. `Ø12@20` | D.M. `Ø12@20` | `3Ø22` longitudinales en borde y otros por confirmar | Registrado |
| `LT2 / 1'` | `M-LT2-1'-C'/D'` | Pisos 3 y 4 | Entre ejes C'-D' | Por confirmar | 30 | D.M. `Ø10@20` | D.M. `Ø10@20` | `3Ø16` longitudinales en borde; otros por confirmar | Registrado |
| `LT2 / 3` | `M-LT2-3-A'` | `1°S` subterráneo | Muro análogo al eje 1; ubicación exacta por confirmar | Por confirmar | 30; retorno 60 | D.M. `Ø12@10` | D.M. `Ø12@20` | `3Ø28` longitudinales en borde; confinamiento por confirmar | Registrado |
| `LT2 / 3` | `M-LT2-3-A'` | Piso 1 | Muro análogo al eje 1; ubicación exacta por confirmar | Por confirmar | 30; retorno 60 | D.M. `Ø12@20` | D.M. `Ø12@20` | `3Ø28` longitudinales en borde; confinamiento por confirmar | Registrado |
| `LT2 / 3` | `M-LT2-3-A'` | Piso 2 | Muro análogo al eje 1; ubicación exacta por confirmar | Por confirmar | 30; retorno 60 | D.M. `Ø10@20` | D.M. `Ø10@20` | `3Ø22` longitudinales en borde; confinamiento por confirmar | Registrado |
| `LT2 / 3` | `M-LT2-3-A'` | Pisos 3 y 4 | Muro análogo al eje 1; ubicación exacta por confirmar | Por confirmar | 30; retorno 60 | D.M. `Ø10@20` | D.M. `Ø10@20` | `3Ø22` longitudinales en borde; confinamiento por confirmar | Registrado |
| `LT2 / 3` | `M-LT2-3-A'-retorno` | `1°S` subterráneo | Retorno perpendicular en A', sobre eje 3 | Por confirmar | 60 | D.M. `Ø12@20` (lectura por confirmar) | D.M. `Ø12@20` (lectura por confirmar) | Barras de borde visibles, aparentemente `3Ø32`; por confirmar | Parcial |
| `LT2 / 3` | `M-LT2-3-A'-retorno` | Pisos 1 a 4 | Retorno perpendicular en A', sobre eje 3 | Por confirmar | 60 | `4.M. Ø10@20` (lectura literal) | Por confirmar | Refuerzos de borde visibles, por confirmar | Parcial |
| `LT2 / C'` | `M-LT2-C'-1'/2` | `1°S` subterráneo | Desde eje 1', 2.945 m hacia eje 2 | 2.945 | 25 | D.M. `Ø10@15` | D.M. `Ø10@20` | Barras de borde `2Ø25`, longitudes por confirmar | Registrado |
| `LT2 / C'` | `M-LT2-C'-1'/2` | Piso 1 | Desde eje 1', 2.945 m hacia eje 2 | 2.945 | 25 | D.M. `Ø10@20` | D.M. `Ø10@20` | Refuerzos de borde por confirmar | Registrado |
| `LT2 / C'` | `M-LT2-C'-1'/2` | Piso 2 | Desde eje 1', 2.945 m hacia eje 2 | 2.945 | 25 | D.M. `Ø8@16` | D.M. `Ø10@20` | Barras de borde `2Ø25`, longitudes por confirmar | Registrado |
| `LT2 / C'` | `M-LT2-C'-1'/2` | Pisos 3 y 4 | Desde eje 1', 2.945 m hacia eje 2 | 2.945 | 25 | D.M. `Ø8@16` | D.M. `Ø10@20` | Barras de borde `2Ø25`, longitudes por confirmar | Registrado |
| `LT2 / D-D'` | `M-LT2-D-D'-2/3` | `1°S` subterráneo | Entre ejes 2-3 | Por confirmar | 25 | D.M. `Ø10@20` | D.M. `Ø10@20` | `+2TØ8@20`, `2Ø16` y otros refuerzos por confirmar | Registrado |
| `LT2 / D-D'` | `M-LT2-D-D'-1'/2` | `1°S` subterráneo | Desde eje 1', 2.945 m hacia eje 2 | 2.945 | 25 | D.M. `Ø10@20` | D.M. `Ø10@20` | Refuerzos de borde por confirmar | Registrado |
| `LT2 / D-D'` | `M-LT2-D-D'-1'/1` | `1°S` subterráneo | Entre ejes 1'-1 | Por confirmar | 25 | D.M. `Ø10@20` | D.M. `Ø10@20` | Refuerzos de borde por confirmar | Registrado |
| `LT2 / D-D'` | `M-LT2-D-D'-2/3` | Piso 1 | Entre ejes 2-3 | Por confirmar | 25 | D.M. `Ø10@20` | D.M. `Ø10@20` | `+2TØ8@20`, `2Ø16` y otros refuerzos por confirmar | Registrado |
| `LT2 / D-D'` | `M-LT2-D-D'-1'/2` | Piso 1 | Desde eje 1', 2.945 m hacia eje 2 | 2.945 | 25 | D.M. `Ø10@20` | D.M. `Ø10@20` | `+2TØ8@20`, `2Ø16` y otros refuerzos por confirmar | Registrado |
| `LT2 / D-D'` | `M-LT2-D-D'-1'/1` | Piso 1 | Entre ejes 1'-1 | Por confirmar | 25 | D.M. `Ø10@20` | D.M. `Ø10@20` | Refuerzos de borde por confirmar | Registrado |
| `LT2 / D-D'` | `M-LT2-D-D'-2/3` | Piso 2 | Entre ejes 2-3 | Por confirmar | 25 | D.M. `Ø8@16` | D.M. `Ø10@20` | Refuerzos de borde por confirmar | Registrado |
| `LT2 / D-D'` | `M-LT2-D-D'-1'/2` | Piso 2 | Desde eje 1', 2.945 m hacia eje 2 | 2.945 | 25 | D.M. `Ø8@16` | D.M. `Ø10@20` | Refuerzos de borde por confirmar | Registrado |
| `LT2 / D-D'` | `M-LT2-D-D'-1'/1` | Piso 2 | Entre ejes 1'-1 | Por confirmar | 25 | D.M. `Ø8@16` | D.M. `Ø10@20` | Refuerzos de borde por confirmar | Registrado |
| `LT2 / D-D'` | `M-LT2-D-D'-2/3` | Piso 3 | Entre ejes 2-3 | Por confirmar | 25 | D.M. `Ø8@16` | D.M. `Ø10@20` | Refuerzos de borde por confirmar | Registrado |
| `LT2 / D-D'` | `M-LT2-D-D'-1'/2` | Piso 3 | Desde eje 1', 2.945 m hacia eje 2 | 2.945 | 25 | D.M. `Ø8@16` | D.M. `Ø10@20` | Refuerzos de borde por confirmar | Registrado |
| `LT2 / D-D'` | `M-LT2-D-D'-1'/1` | Piso 3 | Entre ejes 1'-1 | Por confirmar | 25 | D.M. `Ø8@16` | D.M. `Ø10@20` | Refuerzos de borde por confirmar | Registrado |
| `LT2 / D-D'` | `M-LT2-D-D'-2/3` | Piso 4 | Entre ejes 2-3 | Por confirmar | 25 | D.M. `Ø8@16` | D.M. `Ø10@20` | Refuerzos de borde por confirmar | Registrado |
| `LT2 / D-D'` | `M-LT2-D-D'-1'/2` | Piso 4 | Desde eje 1', 2.945 m hacia eje 2 | 2.945 | 25 | D.M. `Ø8@16` | D.M. `Ø10@20` | `+2Ø22`, `+2TØ8@20` y otros por confirmar | Registrado |
| `LT2 / D-D'` | `Vacio-D-D'-1'/1` | Piso 4 | Entre ejes 1'-1 | Por confirmar | No aplica | No aplica | No aplica | Vacio identificado |

## Criterios y pendientes

- `D.M.H.` significa doble malla horizontal y `D.M.V.` significa doble malla vertical.
- Cuando el plano solo indica `D.M. Ød@e`, se registra la misma malla en ambas direcciones, pendiente de confirmar con la leyenda del plano.
- Los muros con una columna intermedia se separan en dos paños para su identificacion y modelacion.
- Una indicacion `(DILATADO)` se interpreta preliminarmente como una junta de dilatacion: debe existir una separacion entre elementos y no una continuidad estructural directa.
- El ancho del vacio y la forma exacta de representar la junta deben confirmarse en planta o en el detalle constructivo correspondiente.
- Los refuerzos localizados de borde, lado tierra, trabas o barras adicionales no se incorporan a la malla principal hasta confirmar su funcion y cantidad.
- En `LT2 / 1`, la cota `185` se registra como el largo del tramo horizontal del muro y la cota `292` como el largo del retorno perpendicular. La cota `375` corresponde a la distancia entre referencias `A'` y `A`, no al largo total del muro.
- La imagen muestra `M.H.A. e=30` en el tramo horizontal y `M.H.A. e=60` en el retorno perpendicular; este ultimo debe modelarse como un muro conectado, no como un pilar, salvo que otro detalle lo contradiga.
- Falta relacionar cada fila con el ID y las coordenadas exactas del muro en `P1L2/data/geometria_manual.json`.
- Falta revisar si los largos indicados en los planos corresponden a la longitud completa del muro o solo a un tramo de refuerzo.
- La columna `Tramo entre ejes / referencia` debe indicar desde que eje hasta que eje se extiende el muro. Las cotas como `1.579 m`, `2.25 m` y `2.945 m` son referencias parciales desde un eje y no deben confundirse con la longitud total entre ejes.
