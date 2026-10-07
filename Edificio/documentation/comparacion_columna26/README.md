> Evidencia histórica: esta comparación corresponde al modelo anterior a las prolongaciones de las vigas 240 y 341. Los resultados de la ampliación están en Edificio/results y en el visor UnityVisualization.

# Comparación C9 / ID26

## Verificación adicional: caras a 40 cm de cada extremo

Se resolvieron nuevamente G y Q con los datos actuales: las fuerzas locales de todas las barras reproducen los archivos guardados sin diferencia numérica. El desequilibrio relativo de fuerzas globales es 6,52×10⁻⁹ en G y 3,08×10⁻⁹ en Q. Esto no certifica equilibrio global de momentos: subsisten vínculos heredados `equalDOF` entre nodos no coincidentes de muros y pórticos.

Se corrigió la comparación de estaciones para evaluar la hipótesis indicada por el usuario: los 0 y 3160 mm de la foto se leen en x=0,40 y x=3,56 m del elemento de 3,96 m. Son cortes dentro de la columna actual; no se introdujeron zonas rígidas en el análisis ni se redujo artificialmente el peso propio del edificio.

| Caso supuesto | Estación foto | Compresión foto (kN) | Compresión modelo en corte (kN) | Diferencia |
|---|---:|---:|---:|---:|
| CM / G | 0 mm | 3556,504 | 3644,340 | +2,470 % |
| CM / G | 3160 mm | 3518,542 | 3606,378 | +2,496 % |
| CV / Q | ambas | 1062,180 | 1261,731 | +18,787 % |

La diferencia axial entre los dos cortes en G es 37,961542 kN, compatible con los 37,962 kN de la foto al redondear: A·γ·L = 0,49·24,516625·3,16. La diferencia total entre modelos, especialmente CV y cortantes, permanece. Por ejemplo, en Q el cortante Vz del modelo tiene magnitud 2,895536 kN y V3 de la foto tiene magnitud 5,819 kN; requiere confirmar sus ejes y cargas antes de atribuir causas.

Las vigas conectadas al extremo superior son 405, 406, 233 y 420, con la sección general del modelo (A=0,48 m², compatible con 0,60×0,80 m). En el extremo inferior no hay vigas conectadas, ni siquiera considerando nodos coincidentes. Por tanto, la topología actual no permite justificar automáticamente dos offsets rígidos de 0,40 m. La coincidencia de longitud y peso no determina su rigidez.

Evidencia: `verificacion_caras.json` y `comparacion_caras.csv`. Reproducir con:

```powershell
python Edificio/verification/interactive/verificar_caras_columna26.py
```

**Estado:** errores del visor corregidos; resultados actuales reproducidos; hipótesis de longitud libre comprobada respecto al peso propio; equivalencia completa con el original aún no demostrada. No se alteraron cargas, apoyos o rigideces para forzar la tabla.

## Comparación inicial en nodos

La correspondencia C9 → ID26 fue indicada por el usuario. Solo se dispone de una fotografía de cuatro filas, sin modelo SAP, ejes locales, cargas o asignaciones de extremos. CM → G y CV → Q se usan como correspondencias provisionales, no como equivalencias verificadas.

## Diferencias confirmadas

- La foto usa N, N·mm y mm. El proyecto usa kN, kN·m y m: dividir fuerzas por 1000 y momentos por 1 000 000.
- La columna analítica 26 conecta los nodos 101201 y 201201, en (20; 7,25; 3,96) y (20; 7,25; 7,92) m: longitud 3,96 m. La foto termina en 3,16 m. No corresponde comparar ambas estaciones finales como si fueran iguales.
- La caída de compresión CM en la foto es 37,962 kN; en G del modelo es 47,572 kN. Con A=0,49 m² y peso específico 24,516625 kN/m³, corresponden aproximadamente a pesos propios sobre 3,16 y 3,96 m. Esto sugiere una diferencia de longitud cargada o zonas de extremo, pero no prueba su definición ni reparto de rigidez. No se introdujeron offsets arbitrarios de 0,40 m.
- El inspector tenía respuestas antiguas: N de extremo i para G era 3695,307 kN y para Q 1243,403 kN. Los resultados actuales son 3649,145 y 1261,731 kN. Se regeneraron las respuestas de barras del láser en PC y Cardboard desde los archivos actuales.
- El inspector mostraba acciones nodales de extremo bajo el título de esfuerzos internos. Ahora muestra los valores de las secciones del diagrama: N positivo en compresión y signos consistentes entre i y j. Las acciones nodales se conservan en los datos sin alterarlas.
- Los IDs visuales 695–704 no tienen resultados en la corrida actual. Sus respuestas antiguas se retiraron del inspector y se informa que no hay datos; no se reasignaron a otras barras.

## Axiales, comparación de magnitudes

| Caso supuesto | Sección | Foto: compresión (kN) | Modelo: compresión (kN) | Diferencia |
|---|---|---:|---:|---:|
| CM / G | i | 3556,504 | 3649,145 | +2,60 % |
| CM / G | j | 3518,542 | 3601,573 | +2,36 % |
| CV / Q | i y j | 1062,180 | 1261,731 | +18,79 % |

Las estaciones j difieren en longitud. Las columnas Vy/Vz/My/Mz del CSV se ponen junto a V2/V3/M2/M3 por posición nominal, **sin confirmar una rotación o inversión de ejes**; sus diferencias porcentuales son descriptivas. La torsión de CM es 0,402987 kN·m en la foto y aproximadamente cero en el modelo, por lo que las discrepancias no son únicamente conversiones de unidades o signos.

## Reproducción

Desde la raíz, ejecutar:

```powershell
python Edificio/verification/interactive/comparar_columna26.py --actualizar-visores
python -m unittest discover -s Edificio/verification/tests -p test_columna26.py
```

`comparacion.json` registra hashes, valores y datos anteriores del inspector; `comparacion.csv` contiene los 24 valores comparados. La tabla de la fotografía se conserva en `Edificio/data/references/columna26_C9_foto.csv`.

Se comprobó la coherencia de las seis componentes en las estaciones inicial y final contra las fuerzas de extremo guardadas para 612 barras y nueve casos, y se ejecutaron las pruebas del contrato y de esta comparación. No se modificó la geometría, apoyos, cargas, ejes o resultados analíticos para forzar coincidencia con la foto. La equivalencia con el modelo original permanece sin verificar hasta disponer de sus definiciones.

