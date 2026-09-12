# Proceso lógico de las cargas axiales en las columnas

Este documento describe la ruta completa usada por el proyecto para obtener la
fuerza axial de cada columna. El recorrido comienza en la geometría y el peso de
las losas, continúa con el reparto a vigas o muros y termina con la solución del
modelo tridimensional en OpenSees y su presentación en Unity.

## Resumen paso a paso en palabras simples

Antes de leer las funciones y formulas, es importante separar dos preguntas:

- **Que carga recibe la columna:** es la demanda calculada por el modelo global.
- **Cuanta carga puede resistir la columna:** es la capacidad calculada con su
  seccion, materiales y enfierradura.

El codigo actual resuelve la primera pregunta para cada columna y calcula una
capacidad generica para una seccion Fiber. La comparacion final entre demanda y
capacidad se realizara cuando cada columna tenga su enfierradura confirmada.

### Paso 1. Se describe el edificio

Primero se ingresan los ejes, niveles, coordenadas, secciones y apoyos del
edificio. Esta informacion le indica al programa donde estan las columnas y
como se conectan con vigas, muros y losas.

### Paso 2. Se calcula el peso de las losas

El programa calcula el peso propio de cada losa usando su espesor, densidad y
area. Tambien descuenta los vacios, porque un hueco no transmite peso.

### Paso 3. Se reparte la carga a los elementos

Cada losa se divide en areas tributarias. La carga de cada area se envia a la
viga o al muro que la recibe. Asi la carga no desaparece: cambia de una carga
superficial a fuerzas aplicadas en nodos.

### Paso 4. Se agregan las otras cargas

Se suman las cargas permanentes adicionales, la carga viva y el peso propio de
vigas, columnas y muros. En este punto se forman los casos base `G` y `Q`.

### Paso 5. Se forman las cargas sismicas

El programa toma `G` y el `50%` de `Q` para formar la masa sismica de cada piso.
Despues aplica una aceleracion horizontal de `0.20g` para crear los casos `EX`
y `EY`. Aunque estas cargas son horizontales, pueden aumentar o disminuir la
compresion de una columna por el efecto de volcamiento.

### Paso 6. Se arma el modelo de OpenSees

OpenSees recibe nodos, columnas, vigas, muros, apoyos y diafragmas. Tambien
recibe las cargas nodales calculadas en los pasos anteriores. Antes de cada
caso se eliminan los patrones heredados para no aplicar dos veces la misma
carga.

### Paso 7. OpenSees calcula los desplazamientos y esfuerzos

OpenSees resuelve el equilibrio del edificio. A partir de los desplazamientos
calcula las fuerzas internas de cada elemento. Para una columna se obtiene la
fuerza axial en sus dos extremos, junto con cortantes, torsion y momentos.

### Paso 8. Se obtiene la carga axial de cada columna

El programa identifica cada elemento vertical, revisa sus nodos inferior y
superior y lee su fuerza axial local. Con los dos extremos calcula un unico
valor de compresion para el tramo:

```text
P_columna = (N_i - N_j) / 2
```

Este valor es la carga axial que resulta del analisis global. No se obtiene
sumando pisos a mano, porque las vigas, muros y apoyos pueden desviar parte de
la carga hacia otras columnas.

### Paso 9. Se revisa el recorrido vertical de la carga

El programa busca si sobre el extremo superior de una columna existe otra
columna alineada. Luego compara la carga del tramo con la suma de las columnas
superiores. La diferencia representa la carga que entra o sale del nudo por
medio de vigas, muros, cargas nodales o apoyos.

Por eso una columna inferior no siempre tiene exactamente la suma de las
columnas que estan encima. El edificio es un portico tridimensional y la carga
puede redistribuirse.

### Paso 10. Se crea la seccion resistente

Para calcular la resistencia se define una seccion de hormigon armado con sus
dimensiones, materiales, barras longitudinales, estribos y recubrimiento. El
programa divide la seccion en fibras pequenas; cada fibra representa una parte
de hormigon o una barra de acero.

### Paso 11. Se calcula cuanto puede resistir la seccion

El programa aplica deformaciones a la seccion Fiber y calcula las fuerzas de
cada fibra. Sumando todas las fibras obtiene la fuerza axial `P` y el momento
`M` que puede resistir la seccion.

Al repetir el calculo para distintas posiciones del eje neutro se construye la
curva de interaccion `P-M`. Esta curva indica que combinaciones de compresion y
momento puede resistir la columna.

### Paso 12. Se compara demanda y capacidad

Finalmente se toma la demanda obtenida de OpenSees y se coloca sobre la curva
de interaccion:

```text
demanda:  (P_demanda, M_demanda)
capacidad: curva P-M de la seccion armada
```

Si el punto queda dentro de la curva, la seccion resiste esa solicitacion bajo
las hipotesis adoptadas. Si queda fuera, se debe revisar la seccion, la
enfierradura o la solicitacion.

Actualmente este ultimo paso todavia no se aplica columna por columna. La
seccion Fiber configurada en `parametros.json` es una seccion generica de
`0.70 x 0.70 m` con `16 barras Ø22`; debe reemplazarse o parametrizarse con la
enfierradura especifica de cada eje y tramo.

## 1. Archivos que participan

| Orden | Archivo | Función dentro del proceso |
| --- | --- | --- |
| 1 | `Edificio/data/geometry/geometria_manual.json` | Dimensiones, niveles, ejes, secciones y apoyos del edificio. |
| 2 | `Edificio/data/loads/cargas_losas.json` | Zonas de carga permanente adicional y sobrecarga de uso. |
| 3 | `Edificio/model/builders/generar_modelo_manual.py` | Crea losas, descuenta vacíos, calcula áreas tributarias y genera las resultantes G y Q. |
| 4 | `Edificio/results/modelo_3d_manual.json` | Contrato numérico generado: nodos, elementos, losas y cargas transferidas. |
| 5 | `Edificio/model/opensees/modelo_opensees_3d.py` | Construye los nodos, barras, muros, apoyos, vínculos y diafragmas de OpenSees. |
| 6 | `Edificio/data/parameters/parametros.json` | Define q_Q, peso específico del HA y coeficientes de la combinación R. |
| 7 | `Edificio/analysis/seismic/sismo.py` | Convierte G+0,5Q en masa de piso y calcula las fuerzas EX/EY. |
| 8 | `Edificio/analysis/load_cases/casos.py` | Reconstruye G, Q, EX, EY y R, resuelve OpenSees y extrae `localForce`. |
| 9 | `Edificio/analysis/load_cases/ejecutar.py` | Relaciona columnas alineadas, calcula la trazabilidad y exporta los CSV. |
| 10 | `Edificio/visualization/unity/UnityVisualization/Assets/Scripts/ElementInspector.cs` | Lee y presenta el axial y su procedencia en Unity. |

Los resultados completos por columna quedan en:

- `P1L3/results/G_fuerzas_locales.json`;
- `P1L3/results/Q_fuerzas_locales.json`;
- `P1L3/results/EX_fuerzas_locales.json`;
- `P1L3/results/EY_fuerzas_locales.json`;
- `P1L3/results/R_fuerzas_locales.json`;
- `P1L3/results/auditoria_axiales_columnas.csv`.

## 2. Convención de unidades y signos

El modelo trabaja con kN, m y segundos. Una carga gravitacional aplicada hacia
abajo tiene componente global Z negativa. En los pilares verticales, la acción
local de extremo entregada por OpenSees tiene compresión positiva en el extremo
`i` y negativa en el extremo `j`.

Para eliminar pequeñas diferencias numéricas entre ambos extremos se informa:

```text
P_compresión = (Ni - Nj) / 2
```

En una barra sin carga axial distribuida, `Nj ≈ -Ni`, por lo que esta expresión
es igual a `Ni` y deja explícita la convención de compresión positiva.

## 3. Peso propio superficial de cada losa

**Archivo:** `P1L2/scripts/generar_modelo_manual.py`  
**Bloque:** inicialización de geometría.

```python
slab_thickness = config.get("slab_thickness_m", 0.15)
slab_density = config.get("slab_density_kg_m3", 2500.0)
slab_self_weight = slab_thickness * slab_density * 9.80665 / 1000.0
```

La ecuación es:

```text
q_pp = espesor × densidad × g / 1000
```

Para 0,15 m y 2500 kg/m³ resulta `q_pp = 3,67749375 kN/m²`.
La división por 1000 convierte N a kN.

## 4. Área neta de la losa

**Archivo:** `P1L2/scripts/generar_modelo_manual.py`  
**Bloques:** `void_rectangles_for_slab` y filtrado de losas.

```python
gross_area = slab["area_m2"]
voids = void_rectangles_for_slab(slab)
void_area = sum(void["area_m2"] for void in voids)
effective_area = gross_area - void_area
slab["gross_area_m2"] = gross_area
slab["voids"] = voids
slab["area_m2"] = round(effective_area, 6)
slab["area_check_m2"] = round(
    gross_area - void_area - effective_area, 6
)
```

Por tanto:

```text
A_neta = A_bruta - suma(A_vacíos)
```

El área neta es la que participa en el peso y la sobrecarga. Los huecos no
transmiten carga.

## 5. Reparto del área tributaria a los bordes

**Archivo:** `P1L2/scripts/generar_modelo_manual.py`  
**Función:** `tributary_loads_for_rectangle(lx, ly, edges)`.

La losa rectangular se divide mediante líneas a 45°. Si `long` es el lado largo
y `short` el corto:

```python
short_edge_area = short * short / 4.0
long_edge_area = short * (2.0 * long - short) / 4.0
w_max = slab_self_weight * short / 2.0
```

Cada borde corto recibe un triángulo y cada borde largo un trapecio. La suma de
las cuatro áreas tributarias debe ser el área neta de la losa:

```text
Σ A_tributaria,borde = A_neta
```

Cuando existen vacíos o distintas zonas de uso, `global_tributary_partition`
recorta los polígonos y vuelve a calcular el área efectiva de cada borde. El
control almacenado es:

```python
slab["area_check_m2"] = round(
    sum(load["tributary_area_m2"] for load in slab["tributary_loads"])
    - slab["area_m2"], 6
)
```

## 6. Intensidades G y Q y resultante de cada zona

**Archivo:** `P1L2/scripts/generar_modelo_manual.py`  
**Bloque:** creación de `beam_load_cases`.

```python
q_pm = (zone.get("pm_adic_kg_m2") or 0.0) * KG_TO_KN
q_sc = (zone.get("sc_kg_m2") or 0.0) * KG_TO_KN
q_g = slab_self_weight + q_pm

segment_area = area * share
dead_load_kN = segment_area * q_g
live_load_kN = segment_area * q_sc
```

Las ecuaciones usadas para cada porción tributaria son:

```text
G_losa = A_tributaria × (q_pp + q_permanente_adicional)
Q_losa = A_tributaria × q_SC
```

El programa conserva también la forma triangular o trapezoidal en el contrato,
pero el análisis de Semana 3 utiliza su resultante nodal equivalente.

Si `q_Q_kN_m2` en `P1L3/parametros.json` es `null`, se usa `q_SC_kN_m2` de
cada zona. Si tiene un número, ese valor uniforme reemplaza q_SC:

```python
q = cfg['q_Q_kN_m2']
q = row['q_SC_kN_m2'] if q is None else q
live = q * row['tributary_area_m2']
```

## 7. Selección del elemento receptor

Cada resultante queda asociada a un `beam_id` o a un `wall_id` dentro de
`P1L2/outputs/modelo_3d_manual.json`.

Para vigas, la carga de la porción se reparte entre sus dos extremos. Para
muros, se reparte entre los nodos del borde del muro correspondientes al nivel.
Este es el código activo en Semana 3:

**Archivo:** `P1L3/casos.py`  
**Función:** `vectors(data, cfg)`.

```python
for kind in ('beam', 'wall'):
    for row in data[f'{kind}_load_cases']:
        if kind == 'beam':
            e = elements[row['beam_id']]
            ends = [e['i'], e['j']]
        else:
            ends = walls[row['wall_id']]['edge_node_ids_by_level'][
                str(round(row['level_z_m'], 6))]

        live = q * row['tributary_area_m2']
        for n in ends:
            loads['G'][n][2] -= row['dead_load_kN'] / len(ends)
            loads['Q'][n][2] -= live / len(ends)
```

Así, para una viga de extremos `i` y `j`:

```text
Fz_G,i = Fz_G,j = -G_losa / 2
Fz_Q,i = Fz_Q,j = -Q_losa / 2
```

La fuerza total se conserva aunque la forma del diagrama local de la viga sea
una idealización por cargas concentradas equivalentes.

## 8. Peso propio de vigas y columnas

**Archivo:** `P1L3/casos.py`  
**Función:** `vectors(data, cfg)`.

```python
area = data[section_name]['A_m2']
length = np.linalg.norm(
    np.array(ops.nodeCoord(e['j'])) - ops.nodeCoord(e['i'])
)
unit_weight = (
    data['material_steel']['density_kg_m3'] * 9.80665 / 1000.0
    if e['type'] == 'STEEL_COLUMN_SHS300x20'
    else cfg['peso_especifico_HA_kN_m3']
)
weight = area * length * unit_weight
for n in (e['i'], e['j']):
    loads['G'][n][2] -= weight / 2
```

Para cada barra:

```text
W_barra = A × L × γ
Fz_i = Fz_j = -W_barra / 2
```

Esto agrega a G el peso propio de vigas, columnas HA y columnas metálicas. El
peso de cada muro se calcula con su malla y se agrega a sus nodos:

```python
for n, weight in data['wall_mesh']['node_self_weight_kN'].items():
    loads['G'][n][2] -= weight
```

## 9. Carga sísmica que también puede producir axial

**Archivo:** `P1L3/sismo.py`  
**Función:** `calcular_pisos(...)`.

Para EX y EY, primero se forma el peso sísmico de cada piso:

```python
G = sum(gravity_loads.get(n, 0.) for n in ids)
Q = sum(live_loads.get(n, 0.) for n in ids)
W = factor_g * G + fraction * Q
mass = W / g
acceleration = alpha * g
force = mass * acceleration
```

Con los parámetros actuales:

```text
W_piso = 1,0 G_piso + 0,5 Q_piso
m_piso = W_piso / g
F_piso = m_piso × (0,20g)
```

La fuerza se aplica en el nodo maestro del diafragma, junto con el momento que
la transporta al centro de masa:

```python
loads['EX'][master] += [F, 0, 0, 0, 0, floor['Mz_EX_master_kNm']]
loads['EY'][master] += [0, F, 0, 0, 0, floor['Mz_EY_master_kNm']]
```

Aunque EX y EY son horizontales, el volcamiento y la rigidez tridimensional
pueden aumentar la compresión de algunas columnas y reducirla o generar tracción
en otras.

## 10. Construcción estructural y continuidad entre pisos

**Archivo:** `P1L2/scripts/modelo_opensees_3d.py`  
**Función:** `build_model()`.

Los pilares y vigas se crean como elementos tridimensionales elásticos:

```python
ops.element(
    "elasticBeamColumn", element["id"], element["i"], element["j"],
    section["A_m2"], E, G, section["J_m4"], section["Iy_m4"],
    section["Iz_m4"], transform
)
```

Dos columnas consecutivas transmiten carga directamente cuando el nodo superior
de la inferior es exactamente el nodo inferior de la superior:

```text
columna inferior.j == columna superior.i
```

`P1L3/ejecutar.py` comprueba además coincidencias geométricas. Si hay columnas
alineadas en X, Y y Z pero no comparten nodo, la ejecución se detiene con:

```python
if disconnected:
    raise ValueError('Columnas alineadas sin nudo comun: ' + ...)
```

En el modelo actual los 128 pilares pasan este control.

Los muros se representan con `ShellMITC4`; los vínculos `equalDOF`, los
diafragmas y los apoyos forman también parte del camino de carga. Por eso la
carga puede pasar de una línea de pilares a otra mediante las vigas del piso.

## 11. Eliminación de patrones heredados y aplicación del caso correcto

**Archivo:** `P1L3/casos.py`  
**Función:** `solve(coeff, cfg)`.

`build_model()` aplica inicialmente patrones heredados de P1L2. Antes de cada
corrida independiente se eliminan para impedir una duplicación de G y Q:

```python
data = base.build_model()
for tag in (1, 2, 3, 6, 7):
    ops.remove('loadPattern', tag)
```

Después se reconstruye exactamente el caso solicitado:

```python
loads, floors, transfers, base_weight = vectors(data, cfg)
total = defaultdict(lambda: np.zeros(6))
for case, scale in coeff.items():
    for n, force in loads[case].items():
        total[n] += scale * force

ops.timeSeries('Linear', 100)
ops.pattern('Plain', 100, 100)
for n, force in total.items():
    if np.any(force):
        ops.load(n, *force.tolist())
```

Ejemplos de `coeff`:

```text
G  -> {'G': 1.0}
Q  -> {'Q': 1.0}
EX -> {'EX': 1.0}
R  -> {'G': λG, 'Q': λQ, 'EX': λEX, 'EY': λEY}
```

## 12. Solución de OpenSees

**Archivo:** `P1L3/casos.py`  
**Función:** `solve(coeff, cfg)`.

```python
ops.constraints('Penalty', cfg['penalty'], cfg['penalty'])
ops.numberer('RCM')
ops.system('UmfPack')
ops.algorithm('Linear')
ops.integrator('LoadControl', 1.0)
ops.analysis('Static')
if ops.analyze(1):
    raise RuntimeError(...)
```

Conceptualmente OpenSees resuelve:

```text
K u = F
```

La matriz `K` contiene la rigidez y conectividad de todas las vigas, pilares,
muros, vínculos y apoyos. `F` contiene las cargas nodales construidas en los
pasos anteriores. A partir de los desplazamientos `u`, OpenSees recupera las
acciones de extremo de cada elemento.

Una columna de un pórtico tridimensional hiperestático no recibe necesariamente
la suma aritmética de las columnas situadas arriba. La compatibilidad del piso
permite que las vigas transfieran carga a columnas vecinas o a un apoyo elevado.
La suma simple solo sería válida para una cadena vertical aislada y articulada.

## 13. Extracción de la carga axial de cada columna

**Archivo:** `P1L3/casos.py`  
**Función:** `solve(coeff, cfg)`.

```python
local_forces = {
    str(e['id']): ops.eleResponse(e['id'], 'localForce')
    for e in data['elements'] if e['type'] != 'WALL'
}
```

Cada vector contiene:

```text
[Ni, Vyi, Vzi, Ti, Myi, Mzi, Nj, Vyj, Vzj, Tj, Myj, Mzj]
```

El axial mostrado no se copia desde Excel ni se obtiene sumando manualmente
pisos. Es la fuerza interna recuperada por OpenSees después de resolver el
edificio completo.

## 14. Relación entre una columna y las superiores

**Archivo:** `P1L3/ejecutar.py`  
**Función:** `export_unity(...)`, bloque “Trazabilidad vertical de pilares”.

Primero se ordenan los extremos por altura y se indexan los pilares por nodo:

```python
by_lower.setdefault(col['lower_node'], []).append(col)
by_upper.setdefault(col['upper_node'], []).append(col)
```

Después se calcula la compresión y se buscan los tramos superiores conectados:

```python
compression = {
    col['id']: 0.5 * (
        float(local[str(col['id'])][0])
        - float(local[str(col['id'])][6])
    )
    for col in columns
}

above = by_lower.get(col['upper_node'], [])
p_above = sum(compression[a['id']] for a in above)
net_joint = compression[col['id']] - p_above
```

La identidad informada es:

```text
P_tramo = ΣP_superiores + aporte_neto_nudo
```

`aporte_neto_nudo` representa la transferencia vertical neta entre esa línea
de columnas y el resto del piso. Incluye el efecto conjunto de vigas, muros,
cargas nodales y restricciones. No es una carga nueva aplicada al modelo.

- Si es positivo, el piso agrega compresión a esa línea.
- Si es negativo, el piso desvía parte de la carga hacia otras líneas o apoyos.
- Si no existe columna superior, `ΣP_superiores = 0`.

## 15. Ejemplos reales del caso G

Los siguientes valores provienen de
`P1L3/results/auditoria_axiales_columnas.csv`.

### Columna 3, eje G, Z=0,00–3,96 m

```text
P del tramo                         = 3328,368 kN
P de la columna superior 20         = 3280,796 kN
Aporte vertical neto del nudo       =  +47,572 kN
Comprobación: 3280,796 + 47,572     = 3328,368 kN
```

Aquí el axial aumenta hacia abajo.

### Columna 1, eje E, Z=0,00–3,96 m

```text
P del tramo                         =  603,564 kN
P de la columna superior 18         = 1028,624 kN
Aporte vertical neto del nudo       = -425,060 kN
Comprobación: 1028,624 - 425,060    =  603,564 kN
```

La columna superior sí participa. El valor menor de la columna inferior indica
que en ese nudo el pórtico desvía 425,060 kN hacia otras líneas resistentes. El
modelo contiene apoyos elevados en H/I/I′, por lo que existe una ruta física
alternativa a través de las vigas.

## 16. Superposición para el caso R

Como el edificio global se analiza linealmente:

```text
P_R = λG P_G + λQ P_Q + λEX P_EX + λEY P_EY
```

**Archivo:** `P1L2/UnityVisualization/Assets/Scripts/ElementInspector.cs`  
**Función:** `TryAxial`.

```csharp
for(int k=0;k<4;k++)
{
    double[] source;
    if(!TryAxial(Semana3Visualizer.BaseCases[k],id,out source)) return false;
    for(int i=0;i<3;i++)
        values[i] += viewer.Combination[k] * source[i];
}
```

La misma combinación se verifica contra una corrida explícita de OpenSees en
`P1L3/results/comparacion_superposicion.csv`.

## 17. Presentación en Unity

**Archivo:** `P1L2/UnityVisualization/Assets/Scripts/ElementInspector.cs`  
**Funciones:** `Start`, `DrawAxialTrace` y `TryAxial`.

Unity lee `Assets/Resources/semana3_axiales_columnas.csv` y, al seleccionar una
columna, presenta:

```csharp
GUILayout.Label("P del tramo: " + a[0].ToString("F2") + " kN");
GUILayout.Label("Σ P de columnas alineadas superiores: "
    + a[1].ToString("F2") + " kN");
GUILayout.Label("Aporte vertical neto en el nudo: "
    + a[2].ToString("+0.00;-0.00;0.00") + " kN");
```

Las tres cantidades se calculan para G, Q, EX, EY y R. Cuando cambian los
ponderadores sísmicos o los coeficientes de R, Unity combina las bases ya
calculadas sin reemplazar los resultados de OpenSees.

## 18. Cómo consultar cualquier columna

Abrir `P1L3/results/auditoria_axiales_columnas.csv` y filtrar por:

1. `caso`: G, Q, EX, EY, R, EXG, EXQ, EYG o EYQ;
2. `elemento`: ID mostrado por Unity;
3. `eje`, `x_m`, `y_m`, `z_inferior_m` y `z_superior_m`.

Las columnas principales del CSV son:

| Campo | Significado |
| --- | --- |
| `P_compresion_kN` | Axial recuperado por OpenSees en el tramo seleccionado. |
| `elementos_superiores` | ID de los pilares que descargan directamente en su nodo superior. |
| `P_superior_kN` | Suma de los axiales de esos pilares superiores. |
| `aporte_neto_nudo_kN` | Diferencia que entra o sale mediante el resto del piso. |
| `continuidad_nodal` | Confirma que una alineación geométrica comparte realmente el nodo. |

## 19. Controles que respaldan el resultado

- Conservación de carga viva: `P1L3/results/conservacion_Q.csv`.
- Equilibrio carga–reacciones: `P1L3/results/verificaciones_globales.csv`.
- Fuerzas locales completas: `P1L3/results/*_fuerzas_locales.json`.
- Trazabilidad de 128 pilares y nueve casos:
  `P1L3/results/auditoria_axiales_columnas.csv`.
- Comparación de superposición: `P1L3/results/comparacion_superposicion.csv`.

La ejecución se reproduce desde la raíz del proyecto con:

```powershell
python P1L3/ejecutar.py
```

Si se modifica la geometría, una carga o un parámetro, este comando reconstruye
todos los casos y vuelve a exportar la trazabilidad usada por Unity.
