# Entrega: diagramas de vigas y SQ4 carga móvil

Fecha: 21 de septiembre de 2026.

Estado: **SQ4 implementada y verificada para el visor de escritorio Windows**.
Código, resultados regenerados, pruebas y evidencia visual se entregan juntos.
Este estado corresponde al alcance didáctico descrito aquí, no a una
certificación de diseño del edificio ni a una validación en iPhone.

## 1. Los cinco requisitos de SQ4

| Requisito | Implementación entregada |
|---|---|
| Regla física | Carga P vertical, localizada, lineal-elástica y cuasiestática. La velocidad del avatar es sólo visual, sin impacto o inercia. |
| Panel | Magnitud 0–100 kN, preselecciones, selector de panel/nivel, planta arrastrable, W/A/S/D, recorrido automático, centrar/enfocar, escala de deformada. |
| Reparto | Dos vigas opuestas reciben P(1−η) y Pη en la proyección de la posición X. Se respetan los ejes locales y el sentido i→j. |
| Conservación | Suma de fuerzas y primer momento de la carga transferida; balance de fuerzas en apoyos; bordes comunes sin duplicación y caso P=0. |
| Respuesta visual | Avatar, panel resaltado, receptores turquesa/ámbar, flechas proporcionales, porcentajes y kN, deformada rosa, ΔMy y ΔVz con valores de extremo y máximo. |

El recorrido comprende **20 paneles completos y 25 vigas receptoras en cinco
niveles**, dentro de la franja X = −28,30 a −24,55 m. Se excluyen vacíos y
paneles cuyo borde no coincide completamente con una viga receptora. El resto
del edificio sí participa en la solución estructural. No se habilitó movimiento
arbitrario sobre todas las losas.

## 2. Cálculo y diagramas

Se generan cuatro soluciones OpenSees de 1 kN por viga receptora, en posiciones
0, 1/3, 2/3 y 1. La respuesta nodal y las acciones de extremo se interpolan
cúbicamente y se superponen con los factores de reparto. La interpolación se
contrasta con soluciones explícitas en otras posiciones, incluyendo carga
cero y bordes. Unity actualiza la respuesta sin lanzar Python en cada cuadro.

Para SQ4, las vigas cargadas incorporan el término de carga puntual en la
integración de curvatura: no se usa solamente Hermite entre extremos. Se
verifican vigas biapoyadas y biempotradas con subdivisión independiente y
flechas centrales PL³/(48EI) y PL³/(192EI). El máximo de momento considera
extremos y punto de carga; el máximo de desplazamiento se busca en 101 puntos
por receptora. Se informa el desplazamiento real en mm y la amplificación
visual por separado.

**Una viga no tiene siempre momento parabólico.** Con carga uniforme, M es
parabólico; con distribución triangular/trapezoidal tiene tramos de orden
superior; con carga puntual es lineal por tramos y el corte salta en la carga.
SQ4 muestra esta última respuesta, sin aplicar un suavizado ficticio.

También se incluyen los cambios previos de diagramas G/Q: cargas interiores
de barra mediante cuadratura, integración del equilibrio en 41 estaciones,
signos y ejes locales, valores de extremo y búsqueda de máximos interiores.
Se regeneraron los nueve casos/bases y los recursos que consume Unity. En el
visor general, la deformada gravitacional sigue siendo interpolación Hermite
de los desplazamientos y giros calculados: **no se declara exacta la flecha
interior bajo carga distribuida**. La integración particular verificada de
esta entrega se implementó en el modo SQ4.

## 3. Verificaciones realizadas

| Comprobación | Resultado |
|---|---|
| `python Edificio/analysis/load_cases/ejecutar.py --carga-movil` | Finalizó con estado global OK; análisis, recursos y auditoría regenerados. |
| Auditoría SQ4 | 148 controles, todos OK; 20 paneles y 25 receptoras. |
| Controles globales exportados | 72 controles, todos OK, dentro de sus alcances y tolerancias. |
| `python -m unittest discover -s Edificio/verification/tests -p "test_*.py"` | 25 pruebas, todas OK. |
| Continuidad de bordes compartidos | 15 transiciones con el mismo receptor, sin duplicar P. |
| Compilación Unity Windows64 | Exitosa, sobre copia aislada con Unity 6000.5.9f1 instalado. No se guardó ni sustituyó la escena del usuario. |
| Comprobación dentro del ejecutable | `SQ4_RUNTIME_OK`: extremo de deformada contra desplazamiento nodal y respuesta cero. Error máximo aproximado 1,46×10⁻¹¹ m. |
| Evidencia visual real | Capturas del ejecutable a 1440×900 con P=50 kN centrada, reparto excéntrico y P=0. |

La auditoría está en
[`verificacion_carga_movil.json`](../Edificio/results/verificacion_carga_movil.json).
Los controles globales están en
[`verificaciones_globales.csv`](../Edificio/results/verificaciones_globales.csv).
Las pruebas nuevas están en
[`test_carga_movil.py`](../Edificio/verification/tests/test_carga_movil.py).

## 4. Evidencia visual

### Carga centrada: 25 + 25 = 50 kN

![Carga móvil centrada](assets/carga_movil/carga_movil_centro.png)

### Carga excéntrica: 9 + 41 = 50 kN

![Reparto según posición](assets/carga_movil/carga_movil_reparto.png)

### Carga cero: respuesta incremental nula

![Carga cero](assets/carga_movil/carga_movil_cero.png)

El color y la escala de los diagramas permiten leer la forma; sus unidades y
valores numéricos son la referencia para comparar magnitudes.

## 5. Cómo usarlo

1. Abrir `Edificio/visualization/unity/UnityVisualization/Assets/Main.unity` y ejecutar Play.
2. Pulsar **Explorar carga móvil** en el panel izquierdo.
3. Elegir P, panel y posición; arrastrar el punto en planta o usar W/A/S/D.
4. Seleccionar uno de los dos botones de viga para leer su diagrama.
5. Usar **Enfocar** para recuperar la vista; ajustar la escala en el panel derecho.
6. **Volver a casos del edificio** restaura la visualización previa.

En el equipo de trabajo también se dejó un ejecutable independiente en
`Edificio/visualization/unity/UnityVisualization/Build/SQ4-2026-09-21/Edificio.exe`.
Esa carpeta es local y no se publica en GitHub; el código fuente y Resources
permiten reconstruirla. No se reemplazó el ejecutable previo.

Los gráficos están rotulados Δ: corresponden sólo a SQ4, no a la suma
G+Q+sismo. Losas opacas se desactivan al entrar para no tapar la deformada y se
restaura su estado al salir. El panel resaltado es una superficie de recorrido,
no un elemento finito de losa.

## 6. Límites que deben conservarse visibles

- Regla unidireccional didáctica para carga localizada; no sustituye el reparto
  gravitacional de 45° ni simula flexión bidireccional de losas.
- Se conserva la idealización existente, los apoyos y los vínculos. Hay
  `equalDOF` entre nodos no coincidentes que pueden introducir pares; el
  residuo de momento usando sólo reacciones SP queda registrado (máximo
  aproximado 8,629 kN·m por kN en los casos base). **No se afirma equilibrio
  global de momento del edificio basándose sólo en esas reacciones.** La
  transferencia de SQ4 conserva por separado fuerza y momento aplicado.
- No incluye dinámica, impacto, fisuración, plastificación, ni comprobación
  normativa de capacidad; requiere revisión profesional para decisiones reales.
- Prueba visual en Windows, no en iPhone/iOS ni en todas las resoluciones.
- Al cambiar geometría, secciones, materiales o restricciones, regenerar
  análisis y bases SQ4 juntos; reconstruir los ejecutables para incorporar Resources.
- No se incluyen en el commit carpetas Library/Build, configuraciones locales,
  layouts ni otros archivos ajenos a esta implementación.

Detalle técnico y reproducción:
[`CARGA_MOVIL.md`](../Edificio/documentation/CARGA_MOVIL.md).
La aplicación de cargas respeta la interfaz local de
[`eleLoad` de OpenSeesPy](https://openseespydoc.readthedocs.io/en/latest/src/eleload.html).
