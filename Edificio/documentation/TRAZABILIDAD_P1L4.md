# Trazabilidad del modelo OpenSees hacia Unity

## Objetivo

La trazabilidad demuestra que un elemento seleccionado en Unity corresponde al
mismo elemento que fue generado y resuelto en OpenSees. Para conseguirlo, se
conserva el identificador del elemento durante todo el flujo:

```text
datos de entrada
    -> modelo estructural Python/OpenSees
    -> resultados numericos
    -> exportacion para Unity
    -> objeto visual Unity
    -> inspector y resultados
    -> seccion y capacidad
```

La regla principal es que el ID no debe cambiar de significado durante el
proceso. En barras, el ID es directamente el `elementTag` de OpenSees. En
muros, varios elementos `ShellMITC4` pueden formar un paño por piso, por lo que
se utiliza un identificador intermedio de paño (`wall_id`).

## 1. Datos de entrada

El proceso comienza con la geometria, las cargas, los materiales y las
armaduras definidos en `Edificio/data/`.

### 1.1 Geometria

Desde `Edificio/data/geometry/geometria_manual.json` se obtiene la geometria
fuente del edificio: ejes, niveles, posiciones y trazados de los muros.

El archivo `Edificio/model/builders/generar_modelo_manual.py` lee esa geometria
y crea los nodos y elementos del modelo. Para cada barra se genera un campo
`id`. Ese campo se conserva como el identificador estructural de la barra.

Por ejemplo, si el constructor genera:

```text
id = 15
type = COLUMN
i = 700006
j = 701006
```

entonces el elemento estructural que se crea posteriormente en OpenSees tiene
el `elementTag = 15`.

### 1.2 Muros por piso

El mismo constructor separa los muros fuente por niveles consecutivos. El muro
de origen se identifica mediante `source_wall_id` y cada paño recibe su propio
`id`.

Por ejemplo:

```text
wall_id = 1002
source_wall_id = 10
floor = 2
```

Esto significa que el paño `1002` pertenece al muro de origen `10` y se ubica en
el segundo piso. La explicación completa de esta separación está en
`Edificio/documentation/muros_por_piso.md`.

### 1.3 Materiales y armaduras

Los parámetros de columnas se leen desde:

```text
Edificio/data/parameters/parametros.json
```

En la corrida documentada se utilizan, entre otros, los siguientes valores:

```text
Columna: 0.70 x 0.70 m
Hormigon: f'c = 35 MPa
Acero: fy = 420 MPa
Modulo del acero: Es = 210000 MPa
Armadura longitudinal: 16 barras Ø28
Estribos: Ø12 @ 10 cm
```

Las armaduras de muros se asignan mediante:

```text
Edificio/data/reinforcement/asignacion_armadura_muros.json
```

La armadura se relaciona con el muro de origen. Al calcular un paño, el
programa recupera el `source_wall_id`, busca su asignacion y la aplica al paño
correspondiente.

## 2. Creacion del modelo OpenSees

El archivo:

```text
Edificio/model/opensees/modelo_opensees_3d.py
```

recibe los nodos y elementos generados por el constructor.

### 2.1 Barras

Para una barra, el programa usa el ID del elemento para crear el elemento
OpenSees. Conceptualmente:

```text
elemento Python id = 15
        -> ops.element(..., tag=15, ...)
        -> OpenSees elementTag = 15
```

La conectividad también se conserva. Si el elemento tiene nodos `i` y `j`, esa
misma conectividad se utiliza para construir la barra y luego para reconstruirla
visualmente en Unity.

### 2.2 ShellMITC4 de muros

Los muros se discretizan en elementos `ShellMITC4`. Cada shell posee un tag
propio y cuatro nodos de conectividad.

La malla puede tener varios shells dentro de un mismo paño. Por esto la
correspondencia de muros no es:

```text
un ShellMITC4 = un objeto Unity
```

sino:

```text
varios ShellMITC4
        -> un wall_id por piso
        -> un objeto Muro_ID_<wall_id>_Piso_<floor> en Unity
```

La continuidad estructural no se elimina al separar los paños: los paños
consecutivos comparten nodos o coordenadas en sus bordes según la malla
generada.

## 3. Calculo de resultados

El archivo:

```text
Edificio/analysis/load_cases/casos.py
```

aplica los casos de carga y resuelve el modelo. Para cada caso recupera:

- desplazamientos nodales;
- reacciones;
- fuerzas globales;
- fuerzas locales de las barras;
- tags de nodos y elementos.

Los casos principales son:

```text
G   carga permanente
Q   carga viva
EX  sismo en X
EY  sismo en Y
R   combinacion de referencia
```

### 3.1 Resultados de barras

Para cada caso, las fuerzas locales se guardan en archivos como:

```text
Edificio/results/R_fuerzas_locales.json
Edificio/results/EX_fuerzas_locales.json
Edificio/results/EY_fuerzas_locales.json
```

La clave del diccionario es el ID del elemento. Para el elemento `15`, se
consulta la entrada asociada a `15` y se obtienen las fuerzas de los extremos
`i` y `j`:

```text
Ni, Vyi, Vzi, Ti, Myi, Mzi
Nj, Vyj, Vzj, Tj, Myj, Mzj
```

El archivo tabular equivalente es:

```text
Edificio/results/EX_fuerzas_locales.json
Edificio/results/EY_fuerzas_locales.json
Edificio/results/G_fuerzas_locales.json
Edificio/results/Q_fuerzas_locales.json
Edificio/results/R_fuerzas_locales.json
```

La tabla que se prepara para Unity es:

```text
Assets/Resources/semana3_esfuerzos_locales.csv
```

En ella, la combinación `R` y el elemento `15` se identifican mediante:

```text
caso = R
elemento = 15
```

### 3.2 Resultados de muros

Para los muros, las fuerzas de los shells se agrupan en el corte inferior del
paño. La demanda exportada conserva:

```text
panel_id
source_wall_id
floor
shells_corte
P_compresion_kN
M_principal_kNm
V_en_plano_kN
V_fuera_plano_kN
```

El archivo principal es:

```text
Edificio/results/demanda_muros.csv
```

Para el paño `1002`, por ejemplo, la fila conserva `panel_id = 1002`,
`source_wall_id = 10` y `floor = 2`. La columna `shells_corte` indica cuantos
elementos ShellMITC4 participaron en la resultante del corte.

## 4. Exportacion de datos para Unity

El archivo:

```text
Edificio/visualization/exports/exportar_resultados_unity.py
```

prepara los resultados que Unity puede leer.

### 4.1 Geometria que Unity lee

La exportacion genera:

```text
Edificio/visualization/unity/UnityVisualization/Assets/Resources/model_3d.csv
```

Este CSV contiene registros de distintos tipos:

```text
N = nodo
E = barra o elemento lineal
W = paño de muro
S = losa
V = vacio de losa
```

Para una barra, el registro `E` conserva:

```text
id, type, i, j
```

Para un muro, el registro `W` conserva el ID del paño, sus extremos, cotas,
espesor y piso.

### 4.2 Respuestas que Unity lee

Los resultados se exportan a `Assets/Resources/`, entre otros, como:

```text
semana3_desplazamientos.csv
semana3_pisos.csv
semana3_aceleraciones.csv
semana3_esfuerzos_locales.csv
semana3_reparto_losas.csv
semana3_pesos_losas.csv
```

Unity no vuelve a ejecutar OpenSees. Lee esos archivos ya calculados y los usa
para actualizar la deformada, las fuerzas, los centros de masa, las cargas de
losas y el inspector.

## 5. Creacion del objeto Unity

El archivo:

```text
Assets/Scripts/BuildingVisualizer.cs
```

lee `model_3d.csv` y crea los objetos visuales.

### 5.1 Barra

Cuando encuentra una fila `E`, Unity toma el ID, el tipo y los nodos extremos.
Luego transforma las coordenadas de OpenSees a Unity:

```text
OpenSees: X, Y, Z
Unity:    X, Z, Y
```

Para el elemento `15`, el flujo es:

```text
fila E con id = 15
    -> tipo COLUMN
    -> nodos i y j
    -> objeto COLUMN_ID_15
    -> registro del inspector con id = 15
```

El objeto se registra en `ElementInspector` utilizando el mismo ID. Por eso una
seleccion visual puede encontrar las fuerzas asociadas a `15`.

### 5.2 Muro

Cuando encuentra un registro `W`, `BuildingVisualizer` divide visualmente el
muro en tramos por piso y crea un objeto con nombre:

```text
Muro_ID_1002_Piso_2
```

El inspector registra el paño con:

```text
id = 1002
kind = Muro
graphId = 1002
segmentIndex = correspondiente al piso
```

Así, el objeto seleccionado no pierde la relacion con el muro origen ni con el
segmento vertical al que pertenece.

## 6. Inspector y consulta de resultados

El archivo:

```text
Assets/Scripts/ElementInspector.cs
```

mantiene una lista de objetos registrados. Cada objeto contiene, como minimo:

```text
id
tipo
descripcion
graphId
segmentIndex
```

Al seleccionar una barra, el inspector usa el ID y el caso activo para buscar
los resultados. Para la columna `15` y el caso `R`, la busqueda conceptual es:

```text
forces["R:15"]
```

Luego presenta las seis componentes de fuerza y momento en cada extremo.

Para un muro, el inspector entrega el `wall_id` y el `segmentIndex` al objeto
`WallSectionGraphs`. Esto permite seleccionar el segmento de capacidad
correspondiente al paño por piso.

## 7. Seccion y material

### 7.1 Columnas

Los datos de seccion y material de columnas se exportan a:

```text
Assets/Resources/semana3_graficos_seccion.json
```

El archivo contiene una lista `members`. Cada registro relaciona el ID de la
barra con su tipo y si tiene capacidad disponible:

```text
id = 15
type = COLUMN
has_capacity = true
```

El mismo archivo contiene los datos de la seccion de referencia:

```text
b = 0.70 m
h = 0.70 m
fc = 35 MPa
fy = 420 MPa
```

La armadura completa se define en `parametros.json` y se utiliza para calcular
la curva P-M. `SectionGraphs.cs` busca el ID seleccionado y dibuja la curva de
interaccion, la seccion de fibras, la relacion tension-deformacion y los puntos
nominales A-G.

### 7.2 Muros

La capacidad de muros se prepara en Python y se guarda en:

```text
Edificio/results/PM_muros_unity.json
```

Cada muro de este archivo contiene:

```text
id
source_id
floor
name
segments
```

Cada segmento contiene geometria, espesores, armaduras, puntos nominales y la
curva P-M. `WallSectionGraphs.cs` busca el `wall_id`, selecciona el segmento y
superpone la demanda del caso activo sobre la envolvente de capacidad.

## 8. Cadena completa: columna 15

La demostracion completa para la columna `15` se puede explicar asi:

1. Desde `generar_modelo_manual.py` obtenemos un elemento con `id = 15`, tipo
   `COLUMN` y sus nodos extremos.
2. Desde `modelo_opensees_3d.py` usamos ese ID para crear el elemento OpenSees
   con `elementTag = 15`.
3. Desde `casos.py` resolvemos el modelo y recuperamos las fuerzas locales del
   elemento `15` para `G`, `Q`, `EX`, `EY` y `R`.
4. Desde `R_fuerzas_locales.json` obtenemos las doce componentes de fuerza y
   momento de los extremos `i` y `j`.
5. Desde `exportar_resultados_unity.py` llevamos la geometria y las respuestas a
   `Assets/Resources/`.
6. Desde `BuildingVisualizer.cs` leemos la fila `E` y creamos el objeto
   `COLUMN_ID_15`.
7. Desde `ElementInspector.cs` registramos el objeto con ID `15` y consultamos
   la fila `R,15` de `semana3_esfuerzos_locales.csv`.
8. Desde `semana3_graficos_seccion.json` recuperamos la seccion y los materiales
   asociados al miembro `15`.
9. Desde `SectionGraphs.cs` dibujamos la curva P-M y comparamos la demanda con la
   capacidad nominal.

La cadena queda resumida como:

```text
OpenSees elementTag 15
    <-> COLUMN_ID_15
    <-> R_fuerzas_locales.json: 15
    <-> semana3_esfuerzos_locales.csv: R,15
    <-> semana3_graficos_seccion.json: member 15
    <-> curva P-M de la seccion de columna
```

## 9. Cadena completa: muro-paño 1002

Para el paño `1002`, la explicacion es:

1. Desde `generar_modelo_manual.py` obtenemos `wall_id = 1002`,
   `source_wall_id = 10` y `floor = 2`.
2. Desde `modelo_opensees_3d.py` generamos la malla ShellMITC4 del paño.
3. Cada ShellMITC4 recibe un tag OpenSees y una conectividad de cuatro nodos.
4. Desde `exportar_resultados_unity.py` obtenemos los tags ShellMITC4 y sus
   nodos mediante `ops.getEleTags()` y `ops.eleNodes(tag)`.
5. Con la conectividad identificamos el unico `wall_id` al que pertenece cada
   shell.
6. Desde el postproceso agrupamos los shells del paño para obtener la demanda en
   `demanda_muros.csv`.
7. Desde `PM_muros_unity.json` recuperamos la geometria, armadura y curva P-M
   del paño `1002`.
8. Desde `BuildingVisualizer.cs` creamos el objeto
   `Muro_ID_1002_Piso_2`.
9. Desde `WallSectionGraphs.cs` consultamos la curva P-M y la demanda del caso
   activo para ese paño.

La cadena queda resumida como:

```text
ShellMITC4 elementTag(s)
    <-> wall_id 1002
    <-> source_wall_id 10 / floor 2
    <-> Muro_ID_1002_Piso_2
    <-> demanda_muros.csv: panel_id 1002
    <-> PM_muros_unity.json: id 1002
    <-> curva P-M del paño
```

## 10. Verificacion numerica final

El archivo:

```text
Edificio/results/verificacion_demanda_capacidad.json
```

resume la comprobacion final. La corrida documentada contiene:

```text
total = 1650
cumplen = 1650
fuera_capacidad = 0
muros = 410
columnas = 1240
estado = OK
```

El detalle se encuentra en:

```text
Edificio/results/verificacion_demanda_capacidad.csv
```

Este archivo permite comprobar la demanda, la capacidad y el estado de cada
registro sin depender del color o de la posicion del objeto en Unity.

## 11. Que debe mostrar la demostracion

Para demostrar la trazabilidad en vivo:

1. Seleccionar `Columna 15` en Unity.
2. Mostrar el ID y tipo en el inspector.
3. Mostrar las fuerzas del caso `R`.
4. Abrir `R_fuerzas_locales.json` o `semana3_esfuerzos_locales.csv` y comprobar
   el elemento `15`.
5. Abrir la pestaña de diagrama P-M y mostrar la seccion de la columna.
6. Seleccionar `Muro 1002` en Unity.
7. Mostrar `source_wall_id = 10` y `floor = 2`.
8. Mostrar la demanda del paño y la curva P-M.
9. Comparar el estado con `verificacion_demanda_capacidad.json`.

## 12. Aclaracion sobre la interfaz actual

Los datos de seccion y material ya existen y se utilizan para generar los
graficos de capacidad. Sin embargo, en la version actual el texto principal del
inspector de una columna muestra principalmente el tipo y la longitud; no
presenta todavia una ficha completa con dimensiones, `f'c`, `fy` y armadura.

Por eso, para una demostracion estricta del requisito “mostrar seccion y
material”, se debe abrir la pestaña de seccion/capacidad o agregar esos campos
directamente al inspector. La fuente de datos ya esta disponible; lo pendiente
es presentar toda la ficha en el lugar mas visible de la interfaz.

## Conclusion

La trazabilidad no depende de una sola pantalla. Se demuestra combinando el
identificador visible en Unity con los registros de los archivos numericos y
con los datos de seccion/capacidad. Para barras, la relacion es directa entre
`elementTag`, ID Unity y resultados. Para muros, se documenta adicionalmente la
agrupacion de los elementos ShellMITC4 en paños por piso.
