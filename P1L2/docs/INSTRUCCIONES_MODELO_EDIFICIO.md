# Instrucciones del Modelo del Edificio

## 1. Objetivo

Este documento explica cómo se construyó el modelo 3D del edificio de Ingeniería en la rama `Edificio-1`. La fuente de geometría es `data/geometria_manual.json`. A partir de esa fuente se generan los nodos, elementos, muros, losas, archivos para OpenSeesPy y el CSV utilizado por Unity.

La geometría se ingresó manualmente a partir de los planos y de las cotas entregadas por el usuario. No se debe editar directamente ningún archivo generado.

## 2. Flujo de trabajo

1. Editar `data/geometria_manual.json`.
2. Ejecutar `scripts/generar_modelo_manual.py`.
3. El generador actualiza:
   - `outputs/modelo_3d_manual.json`.
   - `outputs/nodos_modelo_manual.xlsx`.
   - `UnityVisualization/Assets/Resources/model_3d.csv`.
4. Ejecutar `scripts/modelo_opensees_3d.py` para construir el modelo de OpenSeesPy.
5. Abrir `UnityVisualization` en Unity 6.0.5 (`6000.5.9f1`).
6. Abrir `Assets/Main.unity` y presionar Play.

Después de cambiar el CSV, basta con detener y volver a ejecutar la escena de Unity. No es necesario cerrar Unity.

## 3. Unidades y sistema de referencia

Todas las cotas recibidas en centímetros se convirtieron a metros.

La referencia horizontal principal es:

```text
E = X 0.00 m
E' = X -0.25 m
```

La trama horizontal utilizada para el edificio es:

```text
E' - 0.25 - E - 3.60 - Eb - 2.80 - Ec - 3.60 - F
F - 10.00 - G - 10.00 - H - 10.00 - I - 5.00 - I' - 5.00 - J
```

Coordenadas principales:

```text
E' = -0.25 m
E  =  0.00 m
Eb =  3.60 m
Ec =  6.40 m
Ea =  3.30 m
Ed =  6.70 m
F  = 10.00 m
G  = 20.00 m
H  = 30.00 m
I  = 40.00 m
I' = 45.00 m
J  = 50.00 m
```

La trama vertical principal se tomó desde el eje 3 como referencia:

```text
Eje 3  = Y 0.000 m
Eje 2a = Y 2.305 m
Eje 2  = Y 7.250 m
Eje 1" = Y 12.250 m
Eje 1  = Y 16.150 m
Eje 1' = Y 16.400 m
Eje 3' = Y -0.250 m
```

Las cotas verticales son:

```text
Base/subterráneo = Z 0.00 m
Piso 1           = Z 3.96 m
Piso 2           = Z 7.92 m
Piso 3           = Z 11.88 m
Piso 4           = Z 15.84 m
Nivel superior   = Z 19.80 m
```

Cada tramo vertical tiene `3.96 m`.

## 4. Parte del edificio incorporada

El primer modelo ya contenía una parte desde los ejes `E` hasta `J`. Se agregó la parte faltante hacia la izquierda, desde `A'` hasta `E'`.

Las distancias usadas para esa zona fueron:

```text
A' - A  = 3.75 m
A  - B  = 7.50 m
B  - C  = 10.00 m
C  - C' = 7.58 m
C' - D  = 2.42 m
D  - D' = 0.225 m
D' - E' = 0.325 m
```

Con `E' = -0.25 m`, las coordenadas de esa zona son:

```text
A' = -32.05 m
A  = -28.30 m
B  = -20.80 m
C  = -10.80 m
C' =  -3.22 m
D  =  -0.80 m
D' =  -0.575 m
E' =  -0.25 m
```

## 5. Columnas

Las columnas nuevas no forman una retícula completa. Por eso se implementaron mediante `column_points` en lugar de generarlas automáticamente en todos los cruces.

Las posiciones son:

```text
1B: X -20.80, Y 16.15
1C: X -10.80, Y 16.15
1D: X  -0.80, Y 16.15

2A: X -28.30, Y 7.25
2B: X -20.80, Y 7.25
2C: X -10.80, Y 7.25

3B: X -20.80, Y 0.00
3C: X -10.80, Y 0.00
```

Estas columnas van desde `Z = 0.00 m` hasta `Z = 15.84 m`, conectando los niveles intermedios. La sección adoptada provisionalmente es `0.70 x 0.70 m`.

## 6. Vigas de la zona A'-E'

Las vigas principales se ingresaron usando coordenadas directas cuando no pertenecían a un eje completo.

En cada cielo desde el subterráneo hasta el piso 4 se agregaron:

```text
Eje 1: desde A' hasta E'
Eje 2: desde A  hasta E'
Eje 3: desde A' hasta E'
```

También se agregaron vigas longitudinales desde el eje 1 hasta el eje 3 en:

```text
A'
A
B
C

Vigas fuera de eje:

```text
Punto medio entre A y B: X -24.55 m
Punto medio entre B y C: X -15.80 m
5.00 m desde C y 2.58 m desde C': X -5.80 m
```

Las dos vigas adicionales ubicadas en los ejes intermedios son:

```text
Desde 1A" hacia la viga entre A-B:
X -32.05 a X -24.55, Y 11.885 m

Desde 2A' hacia la viga entre A-B:
X -32.05 a X -24.55, Y 4.265 m
```

Todas estas vigas usan provisionalmente sección `0.60 x 0.80 m`.

## 7. Vigas del edificio antiguo

Para el cielo del piso 4 del edificio antiguo, en `Z = 19.80 m`, se agregaron:

Vigas horizontales completas en los ejes `1`, `2` y `3`, desde `E` hasta `J`.

Vigas verticales en los ejes:

```text
E, F, G, H, I, I' y J
```

Vigas fuera de eje:

```text
Entre F-G: X 15.00 m
Entre G-H: X 25.00 m
Entre H-I: X 35.00 m
Entre I'-J: X 47.55 m
```

Voladizos:

```text
Desde 3-G hacia el lado contrario a 2-G: 4.12 m
Desde 3-H hacia el lado contrario a 2-H: 4.12 m
Las puntas se conectan mediante una viga transversal.
```

## 8. Muros estructurales

Los muros se guardan en la propiedad `walls` de `data/geometria_manual.json`. Se exportan al CSV y Unity los dibuja como sólidos verticales.

### Muros en A' y ejes 1 y 3

En el extremo del eje 3:

```text
Sobre A': desde 1.09 m hacia afuera y 1.825 m hacia el interior.
Espesor: 0.60 m.

Sobre el eje 3: desde A' hacia A, longitud 1.85 m.
Espesor: 0.30 m.
```

En el extremo del eje 1:

```text
Sobre A': desde 1.095 m hacia afuera y 1.825 m hacia el interior.
Espesor: 0.60 m.

Sobre el eje 1: desde A' hacia A, longitud 1.85 m.
Espesor: 0.30 m.
```

Estos muros van desde `Z = 0.00` hasta `Z = 19.80 m`.

### Muros en D'

```text
Entre los ejes 3 y 2 sobre D', con extensiones de 0.35 m en ambos extremos.
Espesor: 0.25 m.

Desde 1-D' avanzando 6.50 m hacia el eje 2.
Espesor: 0.25 m.

En el piso 4, desde 1'-D' avanzando 6.50 m hacia el eje 2.
Espesor: 0.25 m.
```

El muro del eje `D'` cambia de punto de inicio solamente en el piso 4. Los pisos inferiores parten desde `1-D'`; el piso 4 parte desde `1'-D'`.

### Muros entre D' y C'

```text
Desde 1'-D' hasta 1'-C'.
Espesor: 0.30 m.

Desde 1'-C' avanzando 2.945 m hacia el eje 2 por C'.
Espesor: 0.25 m.
```

Estos muros se mantienen desde `Z = 0.00` hasta `Z = 19.80 m`.

### Muros con ejes 1" y 2a

El eje `1"` está en `Y = 12.25 m`.

```text
Desde 1"-E' hasta 1"-Ec.
Espesor: 0.20 m.
```

En el subterráneo este muro se recortó para quedar solamente entre `Eb` y `Ec`.

También se agregaron:

```text
Desde 1"-Eb, avanzando 2.25 m hacia el eje 2.
Espesor: 0.30 m.

Desde 1"-Ec, avanzando 2.25 m hacia el eje 2.
Espesor: 0.30 m.
```

### Muros del subterráneo

El subterráneo está entre `Z = 0.00` y `Z = 3.96 m`.

Sus ejes exteriores son:

```text
E' = -0.25 m
E  =  0.00 m
Ea =  3.30 m
Ed =  6.70 m
F  = 10.00 m
F' = 10.25 m
```

En vertical:

```text
1' = 16.40 m
1  = 16.15 m
1" = 12.25 m
2  = 7.25 m
2a = 2.305 m
3  = 0.00 m
3' = -0.25 m
```

El perímetro del subterráneo es:

```text
E'-1' -> F'-1'   espesor 0.20 m
F'-1' -> F'-3'   espesor 0.30 m
F'-3' -> E'-3'   espesor 0.30 m
E'-3' -> E'-1'   espesor 0.20 m
```

El muro superior `E'-1'` a `F'-1'` tiene una ventana de `1.80 m`. La ventana comienza a `4.93 m` desde `E'-1'`, por lo que el muro se divide en dos sólidos:

```text
Primer tramo: X -0.25 a X 4.68 m
Segundo tramo: X 6.48 a X 10.25 m
```

## 9. Losas

Todas las losas tienen espesor `0.15 m`.

Se colocan en:

```text
Z = 3.96, 7.92, 11.88, 15.84 y 19.80 m
```

La losa principal solicitada cubre el rectángulo entre `1-A` y `3-I'`:

```text
X = -28.30 a 45.00 m
Y = 0.00 a 16.15 m
```

Esta región se repite en los cinco niveles y reemplaza las losas automáticas internas para no duplicar rigidez.

El generador también busca recintos rectangulares cerrados por vigas fuera de esa región. Las áreas abiertas no se convierten en losa.

La cantidad actual generada es aproximadamente:

```text
13 losas
```

La geometría de las losas se guarda en `outputs/modelo_3d_manual.json` y en la hoja `Losas` del Excel.

## 10. OpenSeesPy

Las losas se modelan en `scripts/modelo_opensees_3d.py` mediante áreas tributarias
(no elementos finitos): cada losa reparte su peso propio y cargas de uso sobre las
vigas perimetrales mediante la distribución a 45 grados. En OpenSees las cargas
resultantes se aplican como resultantes puntuales sobre los extremos de las vigas.

Los muros sí se discretizan como elementos finitos. Cada muro geométrico
(propiedad `walls` de `data/geometria_manual.json`) se convierte en una malla de
elementos `ShellMITC4` con una sección `ElasticMembranePlateSection` por espesor:

```text
nh = round(longitud_m / 2.0)  (al menos 1)  -> nº de divisiones horizontales
nv = n_pisos - 1                            -> nº de divisiones verticales
```

Los nodos de la malla se crean en cada nivel y los bordes se conectan a los nodos
de la estructuras (vigas/columnas) cercanos mediante `equalDOF`, de modo que el
muro transmite solicitaciones al resto del pórtico. Los nodos de malla se incluyen
en los diafragmas rígidos de cada nivel.

## 11. Unity

Unity utiliza:

```text
Unity 6000.5.9f1
```

El proyecto es:

```text
UnityVisualization/
```

La escena es:

```text
UnityVisualization/Assets/Main.unity
```

El visor lee:

```text
UnityVisualization/Assets/Resources/model_3d.csv
```

La conversión de coordenadas es:

```text
OpenSees X -> Unity X
OpenSees Y -> Unity Z
OpenSees Z -> Unity Y
```

El visor dibuja:

```text
Columnas: rojo
Vigas: azul
Muros: gris
Losas: azul claro
Nodos: amarillo
```

Los muros se dibujan como sólidos verticales. Las losas se dibujan como volúmenes de `0.15 m` y su cara superior queda alineada con la cara superior de las vigas.

## 12. Archivos que no se deben editar directamente

No editar manualmente:

```text
outputs/modelo_3d_manual.json
outputs/nodos_modelo_manual.xlsx
UnityVisualization/Assets/Resources/model_3d.csv
```

Se deben regenerar desde `data/geometria_manual.json`.

## 13. Pendientes

El estado actual todavía requiere:

1. Verificar todas las vigas contra los planos.
2. Completar las losas que no sean rectangulares o que no estén delimitadas completamente por vigas en el archivo actual.
3. Agregar los orificios de losas para escaleras, ascensores u otros vacíos.
4. Agregar losas en los voladizos donde corresponda.
5. Revisar la conectividad entre losas, vigas, columnas y muros.
6. Revisar secciones, materiales, cargas y apoyos antes del análisis definitivo. Los muros ya se discretizan como `ShellMITC4`, pero falta validar su rigidez, el peso propio aplicado y la interacción con el diafragma bajo cargas laterales.

## 14. Verificación básica

Para regenerar el modelo:

```powershell
python scripts/generar_modelo_manual.py
```

Para verificar que OpenSeesPy pueda construirlo:

```powershell
python scripts/modelo_opensees_3d.py
```

El modelo debe indicar que contiene nodos, elementos y losas. La construcción exitosa no reemplaza la verificación estructural de unidades, conectividad, secciones, cargas y condiciones de borde.
