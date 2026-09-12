# Diagnóstico y corrección de la deformación máxima

## Problema encontrado

El modelo anterior entregaba `uz = -116.323 mm` en el nodo 900069
(`x=-0.25 m`, `y=16.15 m`, `z=11.88 m`). Ese extremo de viga estaba a
0.25 m de la columna del eje E, nodo 302001, pero ambos nodos no tenían
conexión vertical. OpenSees interpretaba la esquina como libre.

La viga más exigida en esa configuración era el elemento 543, de 8.90 m,
con un momento de aproximadamente 1828.5 kNm. La respuesta estaba gobernada
por G y Q, no por el sismo.

## Corrección aplicada

El generador ahora detecta las separaciones cortas entre caras de vigas y
ejes de columnas hasta `frame_face_link_max_m = 0.30 m`. Agrega 16 elementos
`BEAM_RIGID_OFFSET` después de calcular las áreas tributarias, de modo que los
enlaces no generen losas ni modifiquen su partición.

El enlace que corrige el máximo anterior es el elemento **689**, entre:

- nodo de columna 302001;
- nodo de viga 900069;
- longitud 0.25 m.

El contrato estructural actualizado contiene 1264 nodos y 694 elementos de
barra, incluidas las cuatro columnas tubulares de acero.

## Resultado corregido

Para `R = 1.2G + 1.4Q + 0.8EX - 0.3EY`:

- el nodo 900069 baja de `-116.323 mm` a **`-1.232 mm`**;
- el nodo 900052 baja a `-0.964 mm`;
- el nodo 900102 baja a `-1.489 mm`;
- la nueva máxima global es **34.071 mm** en el nodo **900116**;
- el nodo 900116 está en `(17.49, -4.12, 11.88) m`, en el conjunto conectado
  por la columna tubular auxiliar `CS_F_AUX`.

Máximos por caso:

| Caso | Nodo | Desplazamiento resultante máximo [mm] |
|---|---:|---:|
| G | 900116 | 20.703 |
| Q | 900116 | 6.580 |
| EX | 900046 | 0.355 |
| EY | 300451 | 0.606 |
| R | 900116 | 34.071 |

## Verificaciones

- conservación de carga viva: OK;
- equilibrio de G, Q, EX, EY y R: OK;
- compatibilidad de diafragmas: OK;
- superposición de desplazamientos, reacciones y fuerzas internas: OK;
- análisis completo de Semana 3: OK.
