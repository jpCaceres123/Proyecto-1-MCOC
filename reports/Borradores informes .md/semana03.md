# Informe Semana 03

## Datos generales

| Campo | Informacion |
| --- | --- |
| Asignatura | Metodos Computacionales en Ingenieria de Obras Civiles |
| Laboratorio | Semana 03 |
| Modelo | Edificio 3D en OpenSees |
| Archivo de ejecucion | `Edificio/analysis/load_cases/ejecutar.py` |
| Unidades | kN, m, s |
| Estado actual | Verificado para G, Q, EX, EY, R, capacidad de hormigon armado de columna y curvas P-M de muros |

## Resumen

Este informe documenta el desarrollo y la verificacion del modelo estructural
de Semana 03. El analisis reutiliza la geometria y las cargas de Semana 02 y
agrega la definicion de casos base, carga sismica pseudoestatica,
superposicion de resultados y analisis de secciones de hormigon armado.

Los resultados numericos se generan en `Edificio/results/`. Las tablas de este informe
se obtienen de esos archivos y las figuras se generan durante la ejecucion del
modelo o se incorporan como evidencia independiente.

## 1. Casos base

Antes de estudiar una combinación de cargas, se analiza cada solicitación por
separado. Estos análisis independientes se denominan casos base. El objetivo
es identificar qué efecto produce cada tipo de carga y, posteriormente, poder
sumar sus respuestas de acuerdo con los factores de combinación definidos.

### 1.1 Caso G: carga permanente

`G` representa las acciones permanentes, es decir, aquellas que actúan durante
la vida útil de la estructura. Incluye el peso propio de vigas, columnas,
muros y losas, además de las cargas permanentes adicionales, como las
terminaciones.

Este caso es necesario porque el edificio siempre está sometido a su propio
peso y a las cargas que no dependen del uso de los recintos. Además, `G` es una
de las principales fuentes de masa para el análisis sísmico. En el modelo se
consideran:

- **Losas:** peso calculado a partir de espesor, densidad y area.
- **Terminaciones:** pavimentos, revestimientos, cielos y otras instalaciones fijas.
- **Vigas y columnas:** peso propio calculado desde la seccion y longitud de
  cada barra.
- **Muros:** peso propio distribuido en los nodos de los elementos de muro.

### 1.2 Caso Q: carga viva

`Q` representa las sobrecargas variables asociadas al uso del edificio. Sus
intensidades y áreas se reutilizan desde la Semana 2 y se transfieren a vigas y
muros mediante las áreas tributarias, tal como se explicó en el capítulo
anterior.

Se mantiene como caso independiente porque puede participar con un factor
distinto en una combinación estructural o en la masa sísmica. Separarla de `G`
permite controlar esa participación sin modificar la carga viva original.

### 1.3 Caso EX: sismo en X

`EX` representa una acción horizontal pseudoestática aplicada en la dirección
global X. Para cada piso se calcula una fuerza equivalente utilizando su masa
sísmica y la aceleración horizontal adoptada.

Se define `EX` para estudiar la respuesta lateral del edificio frente a una
accion horizontal en X. Mantenerlo separado de `EY` permite identificar la
respuesta asociada a cada direccion principal.

### 1.4 Caso EY: sismo en Y

`EY` se construye de la misma manera que `EX`, pero la fuerza actúa en la
dirección global Y. Mantener ambos casos separados permite estudiar la
respuesta del edificio en cada dirección principal.

Se incluye `EY` porque la rigidez, la masa y la geometria pueden producir una
respuesta diferente en la direccion Y. No se supone que la respuesta en X sea
representativa de ambas direcciones.

### 1.5 Diferencia entre casos base y combinaciones

Los casos `G`, `Q`, `EX` y `EY` no se aplican simultáneamente desde el comienzo.
Cada uno se resuelve por separado para obtener desplazamientos, reacciones y
fuerzas internas. Luego, como el modelo global es elástico lineal, sus
respuestas pueden combinarse linealmente mediante los factores correspondientes.

## 2. Carga viva

### 2.1 Reutilización de las áreas tributarias

La carga viva utilizada en el modelo corresponde a la información definida en
la Semana 2. En esta etapa no se modificaron arbitrariamente los paños ni se
volvió a estimar su geometría; se reutilizaron las zonas de carga, sus
intensidades y las áreas tributarias ya establecidas. La información de entrada
se encuentra en `Edificio/data/loads/cargas_losas.json`.

Para interpretar el procedimiento, se distinguen cuatro conceptos. El paño es
la superficie de losa analizada; una zona de carga es una parte del paño que
posee una misma intensidad de sobrecarga; el área tributaria es la porción de
esa zona que corresponde a un elemento receptor; y el receptor es la viga o el
muro al que finalmente se transfiere la carga.

Las áreas tributarias se obtienen mediante la partición a `45°` adoptada en la
Semana 2. Una vez determinada el área correspondiente a cada receptor, la
carga viva se calcula multiplicando la intensidad superficial por dicha área:

```text
Q_zona = q_Q_zona × A_zona
```

En esta expresión, `q_Q_zona` corresponde a la carga viva superficial en
`kN/m²`, `A_zona` es el área tributaria en `m²` y `Q_zona` es la fuerza resultante
en `kN`. Por ejemplo, para una asignación con `A_zona = 3.096679 m²` y
`q_Q_zona = 4.903325 kN/m²`, se obtiene:

```text
Q_zona = 4.903325 × 3.096679 = 15.184024 kN
```

El archivo `Edificio/results/transferencia_Q.csv` contiene el detalle de este
proceso. Para cada losa identifica el receptor, su ID, el área tributaria, la
intensidad aplicada y la carga resultante. De esta manera, la tabla permite
reproducir el cálculo y comprobar que las áreas reutilizadas se transforman en
cargas aplicadas a vigas y muros.

### 2.2 Conservación de la carga viva

La reutilización de las áreas debe conservar la carga total. Por ello, para
cada nivel se compara la carga calculada a partir de las zonas originales con
la suma de las cargas transferidas al modelo:

```text
Q_origen = suma(q_Q_zona × A_zona)
Q_transferida = suma(cargas aplicadas a vigas y muros)
error = Q_transferida − Q_origen
```

Si el procedimiento está correctamente implementado, el error debe ser nulo
dentro de la tolerancia numérica. Un error pequeño se atribuye al redondeo de
las áreas y de las cargas; en cambio, una diferencia apreciable indicaría una
pérdida, duplicación o asignación incorrecta de carga.

### 2.3 Evidencia del procedimiento

La verificación se respalda mediante tres fuentes complementarias:

- `Edificio/data/loads/cargas_losas.json` contiene las zonas y áreas de origen.
- `Edificio/results/transferencia_Q.csv` muestra el reparto hacia cada receptor.
- `Edificio/results/conservacion_Q.csv` compara los totales por nivel.

Además, el script `Edificio/verification/load_transfer/verificar_reparto_combinacion.py`
revisa automáticamente que la suma de las cargas transferidas coincida con la
carga de cada losa y que no existan receptores duplicados. La representación
gráfica de las zonas de carga complementa esta evidencia, pero la comprobación
principal se basa en las tablas y en el resultado numérico del verificador.

### 2.4 Resultados

| Cota [m] | Area origen [m2] | Q origen [kN] | Q transferida [kN] | Error [kN] |
| --- | ---: | ---: | ---: | ---: |
| 3.960 | 711.280506 | 3027.990398 | 3027.990360 | -0.000039 |
| 7.920 | 1367.873306 | 5427.242434 | 5427.242393 | -0.000042 |
| 11.880 | 1391.723306 | 5536.081834 | 5536.081785 | -0.000049 |
| 15.840 | 1494.555506 | 5268.791291 | 5268.791259 | -0.000032 |
| 19.800 | 1486.376931 | 5346.815901 | 5346.815899 | -0.000002 |

### 2.5 Conclusión

Los resultados muestran que las áreas tributarias definidas originalmente se
reutilizan sin alterar la carga viva de cada nivel. La carga de origen y la
carga transferida presentan diferencias del orden de `10⁻⁵ kN`, atribuibles al
redondeo numérico. Asimismo, la verificación automática confirma que las `652`
losas analizadas conservan sus cargas `G/Q` y que no existen receptores
duplicados. Por lo tanto, la transferencia y conservación de la carga viva
quedan verificadas para la idealización adoptada.

## 3. Sismo pseudoestático

### 3.1 Objetivo

El objetivo es representar la acción sísmica mediante fuerzas horizontales
equivalentes aplicadas en los distintos niveles del modelo. Se consideran dos
direcciones independientes: `EX`, en la dirección global X, y `EY`, en la
dirección global Y.

Este procedimiento corresponde a una idealización pseudoestática con fines académicos. Las fuerzas sísmicas se calculan directamente como `F = m × 0.20g`. Por lo tanto, el modelo no incorpora un espectro normativo ni los factores de reducción sísmica asociados a la ductilidad y disipación de energía de la estructura. El símbolo `R` utilizado posteriormente corresponde únicamente al nombre de una combinación de cargas y no debe confundirse con el factor normativo de reducción sísmica.

### 3.2 Masa sismica

La masa sísmica de cada piso se obtiene a partir de la carga permanente y de una
fracción de la carga viva. En este caso se considera el `100%` de `G` y el
`50%` de `Q`:

```text
W_i = alpha_G * G_i + alpha_Q * Q_i
m_i = W_i / g
```

En estas ecuaciones, `W_i` es el peso sismico del piso o bloque `i` en `kN`,
`G_i` es la carga permanente en `kN`, `Q_i` es la carga viva en `kN`, `m_i` es
la masa sismica en `kN s2/m`, `alpha_G` y `alpha_Q` son ponderadores sin
unidades, y `g` es la aceleracion de gravedad, aproximadamente `9.80665 m/s2`.
El peso sísmico se divide por la aceleración de gravedad para obtener la masa
que utiliza OpenSees. De esta forma, la fuerza inercial se calcula mediante
`F = m × a`.

Los valores utilizados en esta corrida son:

```text
alpha_G = 1.00
alpha_Q = 0.50
```

Esto significa que se considera el `100%` de `G` y el `50%` de `Q` para formar el peso sismico. La carga viva `Q` no cambia; solo cambia la fraccion de ella
que participa en la masa sismica.

Se adopta `alpha_G = 1.00` porque el peso permanente representa una accion
permanente de la estructura. Se adopta `alpha_Q = 0.50` porque es el valor
indicado para esta corrida de laboratorio. Este valor es un parametro del
ejercicio y no una justificacion normativa general.

### 3.3 Fuerza horizontal equivalente

La fuerza horizontal de cada piso se calcula como:

```text
a_i = alpha_sismo_i * g
F_i = m_i * a_i
```

En estas ecuaciones, `a_i` es la aceleracion horizontal del piso `i` en
`m/s2`, `alpha_sismo_i` es la aceleracion expresada como fraccion de `g`, `m_i`
es la masa sismica en `kN s2/m` y `F_i` es la fuerza horizontal equivalente en
`kN`. La ecuacion `F_i = m_i * a_i` corresponde a la segunda ley de Newton.

Para esta corrida se adopta una aceleración uniforme en altura:

```text
alpha_sismo = 0.20
```

El valor `0.20` indica que la aceleración horizontal corresponde al `20%` de
`g`. Se utiliza un perfil uniforme porque esta etapa busca comprobar la
implementación de masas, fuerzas y equilibrio; además, el enunciado utilizado
no exige una distribución triangular con la altura. El parámetro queda editable
para una etapa posterior.

Por lo tanto, la fuerza aplicada equivale al `20%` del peso sismico de cada
piso. El parametro puede modificarse mediante `aceleracion_por_piso_g` si se
requiere asignar aceleraciones diferentes por nivel.

### 3.4 Centro de masa y momento equivalente

La fuerza horizontal de cada piso se aplica en su centro de masa. Como el
modelo representa cada diafragma mediante un nodo maestro, la fuerza se aplica
en ese nodo junto con un momento equivalente que reproduce el efecto de
transportarla desde el centro de masa.

Se utiliza el nodo maestro porque los diafragmas se representan mediante
restricciones cinematicas y no mediante una superficie rigida adicional. El
centro de masa es el punto fisico de aplicacion de la fuerza, mientras que el
nodo maestro es el punto disponible para aplicar la carga en el modelo.

El centro de masa se calcula a partir de las masas nodales de cada piso. El
momento equivalente permite conservar el mismo efecto estático que se obtendría
aplicando directamente la fuerza en el centro de masa.

Para una fuerza en X se utiliza la diferencia de coordenadas en Y. Para una
fuerza en Y se utiliza la diferencia de coordenadas en X:

```text
M_z,EX = -F_x * (y_CM - y_M)
M_z,EY =  F_y * (x_CM - x_M)
```

Donde `M_z` es el momento alrededor del eje vertical en `kN m`, `F_x` y `F_y`
son las fuerzas horizontales en `kN`, `(x_CM, y_CM)` son las coordenadas del
centro de masa en `m` y `(x_M, y_M)` son las coordenadas del nodo maestro en
`m`. Este momento evita alterar la resultante estatica al trasladar la fuerza.

No se agrega excentricidad accidental en esta etapa.

### 3.5 Resultados por piso

Los resultados se presentan en tablas separadas para distinguir con claridad
los pesos utilizados, las fuerzas aplicadas y la respuesta obtenida. La masa,
el centro de masa y las fuerzas de cada bloque se obtienen de
`Edificio/results/masas_y_sismo.csv`, mientras que los desplazamientos y giros
se obtienen de los archivos de respuesta de cada caso sísmico.

#### 3.5.1 Pesos, masas y fuerzas aplicadas

La siguiente tabla muestra el peso permanente `G`, la carga viva `Q`, el peso
utilizado para formar la masa sísmica, la masa resultante y las fuerzas
horizontales aplicadas en las dos direcciones:

| Piso | Cota [m] | G [kN] | Q [kN] | Peso sísmico [kN] | Masa [t] | Fₓ,EX [kN] | Fᵧ,EY [kN] |
| ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 1 | 3.960 | 11521.525 | 3027.990 | 13035.520 | 1329.253 | 2607.104 | 2607.104 |
| 2 | 7.920 | 17947.222 | 5427.242 | 20660.843 | 2106.820 | 4132.169 | 4132.169 |
| 3 | 11.880 | 17758.412 | 5536.082 | 20526.453 | 2093.116 | 4105.291 | 4105.291 |
| 4 | 15.840 | 18961.391 | 5268.791 | 21595.786 | 2202.157 | 4319.157 | 4319.157 |
| 5 | 19.800 | 18518.734 | 5346.816 | 21192.142 | 2160.997 | 4238.428 | 4238.428 |

El peso sísmico de cada nivel se obtiene como `G + 0.50Q`. La fuerza de cada
piso se calcula multiplicando su masa por la aceleración horizontal adoptada,
`0.20g`.

#### 3.5.2 Corte basal

El corte basal se obtiene sumando las fuerzas horizontales aplicadas en todos
los pisos. Como `EX` y `EY` utilizan la misma aceleración, ambos valores son
iguales:

| Dirección | Suma de fuerzas aplicadas [kN] | Control de equilibrio |
| --- | ---: | --- |
| `EX` | 19402.149 | `OK` |
| `EY` | 19402.149 | `OK` |

La suma de las reacciones de apoyo reproduce este valor dentro de la
tolerancia numérica. Por lo tanto, el corte basal aplicado queda equilibrado.

#### 3.5.3 Desplazamientos de los diafragmas

Los desplazamientos corresponden al centro de masa de cada diafragma. Para
`EX` se muestra el desplazamiento en X y para `EY` el desplazamiento en Y,
porque esas son las componentes principales de cada caso:

| Bloque | Cota [m] | Uₓ,EX [mm] | Uᵧ,EY [mm] |
| --- | ---: | ---: | ---: |
| LT1 | 3.960 | 0.000000 | 0.000000 |
| LT2 | 3.960 | 0.414711 | 0.551096 |
| LT1 | 7.920 | 0.807600 | 0.420147 |
| LT2 | 7.920 | 1.364738 | 1.266780 |
| LT1 | 11.880 | 2.561054 | 1.628514 |
| LT2 | 11.880 | 2.733486 | 1.936146 |
| LT1 | 15.840 | 4.047615 | 2.494831 |
| LT2 | 15.840 | 4.047615 | 2.494814 |
| LT1 | 19.800 | 5.209977 | 3.732209 |
| LT2 | 19.800 | 4.895610 | 2.684330 |

Los desplazamientos aumentan en general hacia los niveles superiores, como se
espera en una respuesta lateral. Las pequeñas diferencias entre los bloques
LT1 y LT2 reflejan que cada diafragma tiene una distribución de masa y una
rigidez diferente. Además, el control `pisos con desplazamiento contrario`
reporta `0` pisos en `EX` y `EY`, por lo que no se detectaron niveles que se
desplacen en sentido contrario al patrón aplicado.

#### 3.5.4 Rotación de los diafragmas

La rotación corresponde al giro alrededor del eje vertical `Z`. Se expresa en
milirradianes (`mrad`) para facilitar la lectura:

| Bloque | Cota [m] | Giro EX [mrad] | Giro EY [mrad] |
| --- | ---: | ---: | ---: |
| LT1 | 3.960 | -0.000000 | -0.000000 |
| LT2 | 3.960 | 0.000000 | -0.000081 |
| LT1 | 7.920 | 0.000012 | -0.000019 |
| LT2 | 7.920 | -0.000000 | -0.000020 |
| LT1 | 11.880 | 0.001190 | 0.011807 |
| LT2 | 11.880 | -0.000000 | 0.000000 |
| LT1 | 15.840 | -0.000000 | 0.000635 |
| LT2 | 15.840 | -0.000000 | 0.000000 |
| LT1 | 19.800 | 0.000001 | 0.000021 |
| LT2 | 19.800 | -0.000000 | 0.000049 |

Los giros son pequeños en comparación con los desplazamientos laterales. Su
presencia permite comprobar que la respuesta no es únicamente traslacional y
que la posición del centro de masa respecto del nodo maestro fue considerada.

### 3.6 Verificaciones

Se verifican los siguientes aspectos:

- **Equilibrio global:** las reacciones equilibran la fuerza sismica aplicada.
- **Compatibilidad de diafragmas:** los nodos de cada piso mantienen el
  movimiento rigido impuesto.
- **Relacion `F = m*a`:** cada fuerza de piso coincide con su masa y
  aceleracion asignada.
- **Momento respecto al centro de masa:** la fuerza y el par aplicado en el
  nodo maestro son estaticamente equivalentes.
- **Direccion de la respuesta:** `EX` y `EY` se resuelven como casos separados.

Los resultados detallados se encuentran en:

- `Edificio/results/masas_y_sismo.csv`: masa, centro de masa y fuerza por bloque.
- `Edificio/results/sismo_por_piso.csv`: totales por nivel.
- `Edificio/results/auditoria_masas_piso.csv`: reconstruccion de masas y momentos.
- `Edificio/results/verificaciones_globales.csv`: controles numericos del modelo.

La respuesta sismica se consulta en `Edificio/results/sismo_por_piso.csv` y en los
archivos de respuesta `Edificio/results/*.npz`.

### 3.7 Limitaciones

Los resultados validan la implementacion numerica del procedimiento adoptado,
pero no validan por si solos que los parametros representen la exigencia
normativa del edificio real. La aceleracion, la participacion de `Q` y las
condiciones de apoyo deben confirmarse con el enunciado y los antecedentes del
proyecto.

### 3.8 Conclusion

Los casos `EX` y `EY` se generan a partir de la masa formada por `G` y el `50%`
de `Q`, aplicando una aceleracion horizontal de `0.20g`. El equilibrio,
compatibilidad, relacion `F = m*a` y equivalencia del momento respecto al
centro de masa quedan verificados numericamente.

## 4. Superposición

### 4.1 Objetivo

El propósito de esta etapa es comprobar que, para un modelo elástico lineal, la
respuesta de una combinación puede obtenerse sumando las respuestas de los
casos base, cada una multiplicada por su factor correspondiente. Para verificar
que esta operación es válida se comparan:

1. **Respuesta superpuesta:** suma de las respuestas independientes de `G`,
   `Q`, `EX` y `EY`.
2. **Respuesta explícita:** nueva corrida de OpenSees aplicando directamente
   la combinacion completa.

Se realiza esta comparacion porque confirma que los factores se aplican a las
respuestas correctas y que la superposicion implementada coincide con el
resultado del solver.

### 4.2 Combinacion implementada

La ejecucion actual verifica la combinacion `R`, que corresponde a:

| Combinacion | Expresion | Proposito |
| --- | --- | --- |
| `R` | `1.2G + 1.4Q + 0.8EX - 0.3EY` | Verificar una combinacion con cargas gravitacionales y sismo en ambas direcciones. |

Los coeficientes se consideran factores de la demostracion del laboratorio y no
deben interpretarse como una combinacion normativa sin verificar el enunciado y
la norma aplicable.

### 4.3 Formulacion de la superposicion

Para cualquier respuesta estructural `S`, la respuesta superpuesta se calcula
como:

```text
S_superpuesta = lambda_G * S_G
              + lambda_Q * S_Q
              + lambda_EX * S_EX
              + lambda_EY * S_EY
```

Las variables significan:

- `S_superpuesta`: desplazamiento, reaccion o fuerza interna resultante de la
  combinacion.
- `S_G`, `S_Q`, `S_EX`, `S_EY`: respuesta obtenida en cada caso base.
- `lambda_G`, `lambda_Q`, `lambda_EX`, `lambda_EY`: factores de la combinacion,
  sin unidades.

Por ejemplo, para `R`, los factores son `1.2`, `1.4`, `0.8` y `-0.3`, en ese
orden. El signo negativo de `lambda_EY` invierte la contribucion de `EY` en la
respuesta combinada.

La superposición es válida porque el modelo del edificio utiliza una rigidez
elástica lineal, que permanece igual entre las distintas corridas. No sería
válido aplicar directamente este procedimiento si existieran plastificación,
efectos P-Delta significativos, contacto o cambios de rigidez dependientes de
la carga.

### 4.4 Comparacion con una corrida explicita de OpenSees

Para cada combinacion se deben ejecutar los siguientes pasos:

1. Resolver por separado los casos `G`, `Q`, `EX` y `EY`.
2. Multiplicar cada respuesta por el factor de su combinacion.
3. Sumar las respuestas para obtener `S_superpuesta`.
4. Reiniciar el modelo con la misma rigidez y aplicar directamente todas las
   cargas de la combinacion.
5. Resolver OpenSees para obtener `S_explicita`.
6. Comparar ambas respuestas en todos los grados de libertad, apoyos y
   componentes de fuerzas internas.

El error relativo se calcula como:

```text
error_relativo = max(abs(S_explicita - S_superpuesta))
                  / max(abs(S_explicita))
```

Donde `max` representa el maximo valor de todo el conjunto comparado. El
resultado se considera consistente cuando el error se mantiene por debajo de
la tolerancia definida para el control numerico.

### 4.5 Resultados disponibles

La corrida implementada actualmente corresponde a `R` en
`Edificio/data/parameters/parametros.json`:

| Respuesta comparada | Identificador | Superpuesta | OpenSees explicita | Error relativo |
| --- | --- | ---: | ---: | ---: |
| Desplazamiento | Nodo 900116, DOF 3 | -0.0349795325 | -0.0349795326 | 2.400e-08 |
| Reaccion de apoyo | Nodo 700006, DOF 3 | 7407.3980460 | 7407.3980431 | 4.310e-08 |
| Fuerza interna | Elemento 15, componente global 3 | 7378.8548105 | 7378.8548076 | 1.765e-07 |

La tabla muestra la componente de mayor magnitud de cada familia. La
comparacion completa incluye todos los grados de libertad, apoyos y
componentes de fuerzas internas.

Los resultados completos se encuentran en:

- `Edificio/results/comparacion_superposicion.csv`: valores superpuestos,
  explicitos y errores.
- `Edificio/results/G.npz`, `Q.npz`, `EX.npz` y `EY.npz`: respuestas de los casos
  base.
- `Edificio/results/R.npz`: respuesta explicita de la combinacion actual.

### 4.6 Conclusion

Para `R`, la respuesta superpuesta y la corrida explícita de OpenSees presentan
errores relativos menores que `1e-6` en los tres tipos de respuesta revisados.
Esto confirma que la combinación implementada se está formando correctamente
dentro de la hipótesis elástica lineal.

No se presentan otras combinaciones porque no forman parte de la corrida
implementada actualmente. Si se solicitan posteriormente, deben ejecutarse como
casos explicitos adicionales y agregarse con sus propias verificaciones.

## 5. Momento-curvatura

La curva momento-curvatura muestra cómo cambia la capacidad de momento de una
sección cuando aumenta su curvatura. La curvatura indica cuánto se deforma la
sección al doblarse. El análisis se repite para distintos niveles de carga
axial, porque una columna no tiene la misma capacidad de flexión cuando soporta
una carga vertical pequeña que cuando soporta una carga mayor. Se utiliza la
Fiber Section de la columna representativa definida en
`Edificio/data/parameters/parametros.json`:

```text
Seccion: 0.70 x 0.70 m
Hormigon: fc = 35 MPa
Acero: fy = 420 MPa, Es = 210000 MPa
Armadura: 16 barras de diametro 22 mm
```

En `Edificio/analysis/capacity/capacidad.py`, la función `moment_curvature()`
crea un elemento `zeroLengthSection`. Primero aplica la carga axial y después
incrementa gradualmente la curvatura mediante `DisplacementControl`. En cada
paso se registran la curvatura, el momento y las deformaciones extremas del
hormigón y del acero. El procedimiento se repite para `P=0`, `0.2P0` y `0.4P0`,
con el fin de observar cómo influye la carga axial.

Los resultados se guardan en `Edificio/results/momento_curvatura.csv` y la figura
general de capacidad en `Edificio/results/capacidad_HA.png`.

![Seccion Fiber, momento-curvatura y curva P-M](../Edificio/results/capacidad_HA.png)

**Figura 2.** Seccion discretizada, respuestas momento-curvatura y primeros
puntos de la curva `P-M` de la columna.

## 6. Curva P-M de columna

La curva `P-M` reúne las combinaciones de fuerza axial y momento que puede
resistir la columna. Cada punto de la curva representa un estado posible de la
sección. Para obtenerlos, `interaction_points()` impone una deformación
compatible, calcula la fuerza desarrollada por cada fibra y suma sus fuerzas y
momentos. En los resultados, la compresión axial se presenta con signo positivo.

La curva contiene los estados nominales A-G, desde compresion pura hasta
traccion pura. Los datos se encuentran en:

- `Edificio/results/PM_puntos.csv`.
- `Edificio/results/PM_compatibilidad_envolvente_material.csv`.
- `Edificio/results/resumen_capacidad.json`.

La sección se valida comprobando el equilibrio axial, repitiendo el cálculo con
una malla de fibras más refinada y comparando los resultados con una planilla
independiente mediante:

```powershell
python Edificio/verification/capacity/verificar_pm_excel.py
```

La diferencia maxima obtenida fue `1.81898940355e-12 kN` para `P` y
`4.54747350886e-13 kN m` para `M`, por lo que la interaccion calculada coincide
con la referencia independiente dentro de la precision numerica.

## 7. Curvas P-M de muros

Se calcularon curvas de capacidad para los `24` muros del modelo. Estas curvas
muestran cuánta fuerza vertical (`P`) y cuánto momento flector (`M`) puede
resistir cada muro antes de alcanzar los límites adoptados para el hormigón y
el acero. Como la armadura cambia entre algunos niveles, no se utilizó una sola
sección para todo el muro: se analizaron `45` secciones independientes.

El análisis se hace en la dirección principal del muro, es decir, considerando
que el muro se dobla dentro de su propio plano. El resultado es una capacidad
nominal del modelo, no una demanda producida por el edificio ni un diseño final.

### 7.1 Alcance y criterio de dirección

En esta sección solo se documenta el cumplimiento del requisito de generar una
envolvente `P-M` en la dirección principal de cada muro. La dirección principal
se define a partir de la longitud del muro en planta: el muro se considera
resistente frente a flexión dentro de su propio plano, no frente a una flexión
transversal independiente.

La asignación de armadura utilizada para cada muro se encuentra en:

- `Edificio/data/reinforcement/enfierradura_muros.md`.
- `Edificio/data/reinforcement/asignacion_armadura_muros.json`.

Las asignaciones se clasifican como `11` de confianza alta, `10` de confianza
media y `3` de confianza baja. Las de confianza media y baja deben contrastarse
con los planos antes de utilizar los resultados para diseño final.

### 7.2 Resultados y evidencia

El cálculo produjo `18 716` puntos para dibujar las curvas completas y `630`
puntos característicos, correspondientes a los puntos A-G de las distintas
secciones y muros. Los resultados se encuentran en:

- `Edificio/results/PM_muros_envolvente.csv`.
- `Edificio/results/PM_muros_puntos_clave.csv`.
- `Edificio/results/PM_muros_resumen.csv`.
- `Edificio/results/resumen_capacidad_muros.json`.

![Envolventes P-M de muros](../Edificio/results/capacidad_PM_muros.png)

**Figura 3.** Envolventes nominales `P-M` calculadas para los muros del modelo.

El estado de la verificación es `OK`. Esto significa que el programa pudo
asignar perfiles a los muros, cubrir toda la altura modelada y calcular puntos
con comportamiento consistente según las reglas implementadas. Además, la prueba
`Edificio/verification/tests/test_capacidad_muros.py` comprueba que los `24`
muros estén asignados, que los perfiles cubran la altura modelada, que la
separación de armadura no exceda la especificada y que los puntos límite sean
consistentes.

Este resultado corresponde a una capacidad nominal simplificada. No significa
que el muro ya esté completamente diseñado o aprobado. Todavía no se incluyen
factores de reducción, interacción con corte, confinamiento adicional, pandeo
de barras ni elementos de borde especiales. Además, las asignaciones de
armadura con confianza media o baja deben revisarse directamente en los planos.

### 7.3 Verificación de la dirección principal

El requisito de generar la envolvente en la dirección principal se respalda a
partir de la geometría de los muros, no mediante una segunda curva transversal.
Para cada muro se utilizan sus dos puntos extremos en planta. La distancia entre
ellos define la longitud resistente `L` y, por lo tanto, la dirección en la que
varía la deformación longitudinal. El espesor del muro se utiliza como la
dimensión perpendicular de la sección.

En el modelo se identificaron `14` muros cuya longitud se desarrolla en la
dirección global Y y `10` muros cuya longitud se desarrolla en la dirección
global X:

| Orientación de la longitud del muro | Criterio geométrico | Cantidad de muros | Dirección de la envolvente |
| --- | --- | ---: | --- |
| Paralela a X | `y_i = y_j` | 10 | Flexión en el plano del muro, variando en X |
| Paralela a Y | `x_i = x_j` | 14 | Flexión en el plano del muro, variando en Y |

Esta clasificación demuestra que no se aplicó una única dirección global a
todos los muros. Cada envolvente utiliza la dirección longitudinal de su propio
muro, que es su dirección principal resistente. Para cada una de estas
direcciones se consideran ambos signos del momento, `+M` y `-M`, por lo que la
envolvente representa la flexión en ambos sentidos dentro del plano del muro.

La trazabilidad puede revisarse en `Edificio/results/PM_muros_resumen.csv`,
donde cada registro conserva el ID del muro, el tramo vertical, su longitud,
su espesor y la identificación correspondiente en los planos. La evidencia
geométrica de los extremos de cada muro se encuentra en
`Edificio/results/modelo_3d_manual.json`.

## 8. Verificación RC

La verificación de capacidad de hormigón armado corresponde a la columna
representativa utilizada en el análisis de la sección. Se revisa que las
fuerzas estén en equilibrio, que el área de las fibras represente correctamente
el área de hormigón y acero, que el cálculo sea numéricamente estable y que el
resultado no cambie significativamente al modificar la discretización. El
resumen se encuentra en `Edificio/results/resumen_capacidad.json` y su estado
actual es `OK`.

Para complementar la verificación numérica, se compararon algunos puntos de la
curva `P-M` con cálculos simplificados basados en las expresiones utilizadas en
el curso de hormigón armado. La sección considerada tiene dimensiones
`0.70 × 0.70 m`, hormigón `fc = 35 MPa`, acero `fy = 420 MPa` y `16` barras de
diámetro `22 mm`. El área total de acero es:

```text
As = 16 × π × (0.022 m)² / 4 = 0.006082 m²
```

El área bruta de la sección es `Ag = 0.49 m²`. Para la compresión máxima se
utilizó la expresión simplificada:

```text
P_n = 0.85 fc (Ag − As) + fy As
P_diseño = 0.80 P_n
```

El factor `0.80` corresponde al límite adoptado para la compresión máxima en
la curva nominal. Para los puntos asociados a flexión se utilizó compatibilidad
lineal de deformaciones, el bloque rectangular de Whitney y el límite de
fluencia del acero, que es el procedimiento simplificado enseñado en el curso.

### 8.1 Comparación de puntos característicos

| Punto | Situación representada | Cálculo simplificado [kN, kN·m] | Modelo [kN, kN·m] | Diferencia aproximada |
| --- | --- | ---: | ---: | ---: |
| A | Compresión máxima, `M = 0` | `(13555.3, 0.0)` | `(13560.8, 0.0)` | `0.04%` en `P` |
| C | Acero extremo en fluencia | `(6367.6, 1741.6)` | `(6367.6, 1741.6)` | `≈ 0%` |
| F | Flexión pura, `P = 0` | `(0.0, 763.1)` | `(0.0, 763.1)` | `≈ 0%` |
| G | Tracción pura del acero, `M = 0` | `(-2554.5, 0.0)` | `(-2554.5, 0.0)` | `≈ 0%` |

La comparación muestra que el punto A coincide prácticamente con la expresión
de compresión simplificada. La diferencia de `0.04%` se explica por el uso de
las áreas y posiciones exactas de las fibras en el modelo. Los puntos C y F
requieren compatibilidad de deformaciones para determinar la posición del eje
neutro, por lo que su cálculo simplificado reproduce los valores de la curva
nominal cuando se utilizan los mismos materiales, geometría y armadura. El
punto G coincide con la resistencia a tracción de la armadura:

```text
P_G = −As fy = −0.006082 × 420000 = −2554.5 kN
```

Además de esta comparación, la sección se modeló con `1600` fibras y se
comparó con una malla refinada. La diferencia de área, la estabilidad del
equilibrio axial y la sensibilidad al refinamiento se encuentran en
`Edificio/results/resumen_capacidad.json`. Las curvas de tensión-deformación
utilizadas para el hormigón y el acero se muestran en la figura de capacidad de
la sección.

Esta verificación no constituye aún un chequeo normativo completo ni asigna una
sección distinta a cada columna real del edificio. Su objetivo es comprobar que
la implementación numérica reproduce los resultados esperados mediante las
relaciones básicas de hormigón armado.

## 9. Primera relación demanda-capacidad

La demanda de las columnas se obtiene de las fuerzas locales calculadas por
OpenSees y se registra en `Edificio/results/auditoria_axiales_columnas.csv`.
Para ubicar la demanda sobre la curva `P-M`, se toma la columna con mayor
compresión del caso `R` y se consideran la fuerza axial `P_d` y el mayor valor
absoluto del momento local `M_z` en sus extremos.

| Elemento | Ubicación | `P_d` [kN] | `M_d` [kN·m] | Capacidad `P-M` para `P_d` [kN·m] | Índice axial `P_d/P_A` | Ubicación sobre la curva |
| --- | --- | ---: | ---: | ---: | ---: | --- |
| Columna 15, eje 2C | `X=-10.80 m`, `Y=7.25 m`, `Z=0.00–3.96 m` | 7378.856 | 26.737 | 1639.099 | 0.544 | Entre B y C, dentro de la envolvente |

La columna 15 conecta los nodos `700006` y `701006`. El momento de demanda se
obtiene de los momentos locales en sus extremos: `M_zi = -26.737 kN·m` y
`M_zj = 21.487 kN·m`; para ubicar el punto en la envolvente se utiliza el valor
absoluto mayor, `M_d = 26.737 kN·m`.

En la curva nominal, el punto B tiene `P=13213.740 kN` y
`M=1047.809 kN·m`, mientras que el punto C tiene `P=6367.563 kN` y
`M=1741.581 kN·m`. Como `P_d=7378.856 kN` se encuentra entre ambos, se
interpola una capacidad aproximada de `M=1639.099 kN·m`. Por lo tanto, el
punto de demanda puede representarse como:

```text
(P_d, M_d) = (7378.856 kN, 26.737 kN·m)
```

El punto queda muy por debajo de la curva de capacidad en esa zona, por lo que
la sección representativa dispone de una reserva importante frente a esta
demanda. Esta comparación es una aproximación inicial, ya que utiliza la
sección representativa de `0.70 × 0.70 m` y no la sección y armadura específica
de la columna 15.

La comparación representa una primera relación demanda-capacidad: la demanda
proviene del análisis global y la capacidad, de la sección resistente
representativa. Para realizar un chequeo definitivo sería necesario asignar la
sección y la armadura real de cada columna y revisar simultáneamente `P` y `M`
para todos los elementos.

## 10. Uso de IA

Se utilizó inteligencia artificial como herramienta de apoyo para revisar la
organización del código, aclarar el flujo de cargas, proponer verificaciones y
mejorar la redacción de la documentación. Su participación se limitó a apoyar
estas tareas: el modelo se ejecutó localmente y sus resultados fueron revisados
mediante los archivos de salida y las comprobaciones automáticas. Por lo tanto,
la IA no reemplazó la verificación numérica ni la revisión de los supuestos
estructurales.

Las verificaciones ejecutadas fueron:

- `python .\Edificio\analysis\load_cases\ejecutar.py`.
- `python .\Edificio\verification\load_transfer\verificar_reparto_combinacion.py`.
- `python .\Edificio\verification\capacity\verificar_pm_excel.py`.
- `python -m unittest discover -s .\Edificio\verification\tests -p "test_*.py"`.

Los resultados obtenidos fueron revisados en los CSV y JSON de `Edificio/results/`.
Las principales limitaciones documentadas son el modelo global lineal, el uso
de una sección de columna representativa para esta primera comparación y la
necesidad de contrastar en los planos las asignaciones de armadura de los muros.

## Referencias de ejecucion

Desde la raiz del repositorio:

```powershell
python -m pip install -r .\Edificio\requirements.txt
python .\Edificio\analysis\load_cases\ejecutar.py
python .\Edificio\verification\load_transfer\verificar_reparto_combinacion.py
python .\Edificio\verification\capacity\verificar_pm_excel.py
python -m unittest discover -s .\Edificio\verification\tests -p "test_*.py"
```

Los controles automaticos generados por la ejecucion deben revisarse en
`Edificio/results/verificaciones_globales.csv` y en los archivos de resultados
asociados. Un resultado `REVISAR` debe explicarse en el capitulo que
corresponda; no basta con indicar solamente el estado de ejecucion.

## Anexo A. Tabla para levantar la enfierradura de columnas

La tabla se completara a partir de las elevaciones estructurales de los planos
`300` a `310`. Su objetivo es relacionar cada eje y tramo vertical con la
armadura longitudinal, los estribos y cualquier cambio de seccion.

### A.1 Elevaciones del modelo a revisar

| Eje | Coordenada X [m] | Inicio de columna en el modelo [m] | Elevaciones a revisar [m] | Enfierradura segun plano | Estado |
| --- | ---: | ---: | --- | --- | --- |
| E | 0.00 | 0.00 | 0.00, 3.96, 7.92, 11.88, 15.84, 19.80 | Por confirmar en plano | PENDIENTE |
| F | 10.00 | 0.00 | 0.00, 3.96, 7.92, 11.88, 15.84, 19.80 | Por confirmar en plano | PENDIENTE |
| G | 20.00 | 0.00 | 0.00, 3.96, 7.92, 11.88, 15.84, 19.80 | Por confirmar en plano | PENDIENTE |
| H | 30.00 | 3.96 | 3.96, 7.92, 11.88, 15.84, 19.80 | Por confirmar en plano | PENDIENTE |
| I | 40.00 | 3.96 | 3.96, 7.92, 11.88, 15.84, 19.80 | Por confirmar en plano | PENDIENTE |
| I' | 45.00 | 3.96 | 3.96, 7.92, 11.88, 15.84, 19.80 | Por confirmar en plano | PENDIENTE |
| J | 50.00 | 15.84 | 15.84, 19.80 | Por confirmar en plano | PENDIENTE |

Estas cotas provienen de la geometria actualmente utilizada por el modelo y
solo sirven para organizar la revision. La elevacion del plano tiene prioridad
si difiere de esta tabla.

### A.2 Correspondencia entre planos y elevaciones

La revision de los nombres de elevacion contenidos en los DXF permite organizar
los planos de la siguiente manera:

| Plano | Elevacion o ejes principales identificados |
| --- | --- |
| `300` | `1-1'` |
| `301` | `1''`, `1A`, `1b`, `1C`, `1BB`, `1AA` |
| `302` | `2` |
| `303` | `3-3'` |
| `304` | `2a`, `Ea`, `Eb`, `Ec`, `Ed` |
| `305` | `E-E'`, `Ga`, `H'` |
| `306` | `F-F'`, `H1`, `H2` |
| `307` | `G` |
| `308` | `H` |
| `309` | `I` |
| `310` | `I'`, `IA`, `J`, `IB` |

Esta correspondencia indica que los planos `305` a `310` contienen las
elevaciones principales que se utilizaran para levantar la armadura de las
columnas. Los planos `300` a `304` quedan fuera de este levantamiento.

### A.3 Armadura de columnas identificada en los planos

| Plano | Elevacion | Seccion indicada | Llamada de armadura de columna | Diametro y separacion legibles | Estado |
| --- | --- | --- | --- | --- | --- |
| `305` | `E-E'`, `Ga`, `H'` | `P. 70x70` | `E+6TΦ10@10` | `Φ10` cada `10 cm` | REGISTRADO |
| `306` | `F-F'`, `H1`, `H2` | `P. 70x70` | `E+6TΦ12@10` | `Φ12` cada `10 cm` | REGISTRADO |
| `307` | `G` | `P. 70x70` | `E+6TΦ12@10` | `Φ12` cada `10 cm` | REGISTRADO |
| `308` | `H` | `P. 30x30` | `E+2TΦ10@10` | `Φ10` cada `10 cm` | REGISTRADO |
| `309` | `I` | `P. 70x70` | `E+8TΦ12@10` | `Φ12` cada `10 cm` | REGISTRADO |
| `310` | `I'`, `IA`, `J`, `IB` | `P. 70x70` | `E+6TΦ12@10` | `Φ12` cada `10 cm` | REGISTRADO |

Se agregaran filas cuando una elevacion muestre un cambio de seccion, de
armadura longitudinal o de espaciamiento de estribos. No se debe repetir una
misma armadura en todos los niveles sin comprobar primero la continuidad en los
planos.

### A.4 Alcance de la lectura

La tabla anterior contiene unicamente las llamadas de armadura asociadas a las
columnas que aparecen en las elevaciones `305` a `310`. Se excluyen las
anotaciones correspondientes a losas, fundaciones, vigas y muros.

La notacion `E+6T` o `E+8T` se conserva literalmente porque el DXF no incluye,
en la misma llamada, una explicacion suficiente para decidir si `E` corresponde
a un estribo y si `T` corresponde a una traba. Por lo tanto, la tabla confirma
diametro y espaciamiento, pero la configuracion transversal completa debe
confirmarse en el detalle de seccion de cada columna.

### A.5 Detalle recibido para el eje E-E'

La imagen entregada para el eje `E-E'` muestra cuatro posiciones de armadura
longitudinal: `A1`, `A2`, `B1` y `B2`. Las cotas `(140)` y `(160)` son
separaciones entre posiciones de barras. Se transcriben las longitudes sin
convertirlas en una hipotesis adicional de desarrollo o de empalme.

#### Armadura longitudinal

| Marca | Diametro | Longitud total | Descomposicion indicada |
| --- | ---: | ---: | --- |
| `A1` | `Φ22` | 900 | `40 + 860` |
| `A1` | `Φ25` | 1000 | No indicada |
| `A1` | `Φ25` | 750 | `700 + 50` |
| `A2` | `Φ22` | 700 | `40 + 660` |
| `A2` | `Φ25` | 1000 | No indicada |
| `A2` | `Φ25` | 900 | No indicada |
| `A2` | `Φ25` | 400 | `350 + 50` |
| `B1` | `Φ22` | 1000 | `40 + 960` |
| `B1` | `Φ22` | 800 | No indicada |
| `B1` | `Φ22` | 900 | No indicada |
| `B1` | `Φ22` | 350 | `310 + 40` |
| `B2` | `Φ22` | 800 | `40 + 760` |
| `B2` | `Φ22` | 800 | No indicada |
| `B2` | `Φ22` | 900 | `860 + 40` |

#### Separacion entre posiciones

| Separacion indicada | Significado confirmado |
| ---: | --- |
| `140` | Separacion entre posiciones de armadura. |
| `160` | Separacion entre posiciones de armadura. |

Las longitudes se mantienen en las unidades graficas del detalle, que deben
confirmarse como centimetros antes de ingresarlas al modelo. Las expresiones
entre parentesis se registran como descomposiciones de longitud; por ejemplo,
`40 + 860` suma una longitud total de `900`.

#### Armadura transversal en cortes A y B

Los cortes `A` y `B` muestran la misma configuracion transversal:

| Corte | Armadura indicada | Lectura directa |
| --- | --- | --- |
| `A` | `EΦ12@10 L=280` | Estribo `Φ12` cada `10`, longitud indicada `280`. |
| `A` | `+6TΦ12@10 L=85` | Seis trabas `Φ12` cada `10`, longitud indicada `85`. |
| `B` | `EΦ12@10 L=280` | Estribo `Φ12` cada `10`, longitud indicada `280`. |
| `B` | `+6TΦ12@10 L=85` | Seis trabas `Φ12` cada `10`, longitud indicada `85`. |

La seccion del detalle indica dimensiones aproximadas de `70 x 70 cm` y
dimensiones interiores graficas de `66` y `56`. Las longitudes `L=280` y
`L=85` se registran como longitudes de fabricacion del detalle; no son alturas
de piso.

Este detalle complementa la llamada general del plano `305` y permite asociar
las marcas con sus posiciones. Todavia falta relacionar cada marca con un
tramo vertical especifico de la elevacion `E-E'`.

### A.6 Detalle recibido para el eje H

La elevacion del eje `H` muestra tres columnas de seccion `P. 30x30`. En las
columnas visibles se repite la siguiente armadura:

| Elemento | Seccion | Armadura longitudinal | Armadura transversal | Longitud indicada |
| --- | --- | --- | --- | --- |
| Columna eje `H` | `P. 30x30` | `2Φ16 + 3Φ16` | `E+2TΦ10@10` | `L=600`, con `46+509+45` |

La armadura longitudinal se identifica en el detalle mediante dos barras
`Φ16` en una posicion y tres barras `Φ16` en la posicion opuesta. Las
anotaciones `2Φ16` y `3Φ16` indican ubicacion y cantidad de barras dentro de
la seccion, no una separacion.

La armadura transversal se registra literalmente como `E+2TΦ10@10`: un
estribo y dos trabas, con barras `Φ10` cada `10 cm`, segun la nomenclatura
visible. La longitud vertical de referencia es `L=600`; la expresion
`46+509+45` descompone esa longitud.

En la misma elevacion aparecen vigas `V. 20/80` y refuerzos de viga como
`F=2Φ16 L=1050` y `EΦ8@10`. Esas armaduras no se incorporan en esta tabla,
porque el alcance de este anexo es exclusivamente la armadura de columnas.

### A.7 Datos que se deben extraer de cada plano

- **Seccion:** ancho y alto de la columna en el tramo.
- **Armadura longitudinal:** cantidad, diametro y distribucion de barras.
- **Armadura transversal:** diametro, espaciamiento y tipo de estribo.
- **Confinamiento:** longitud de zonas con estribos mas cerrados en extremos.
- **Empalmes:** ubicacion y longitud de traslapos, si aparecen.
- **Materiales:** resistencia del hormigon y fluencia del acero, si estan
  indicadas.
- **Identificador:** correspondencia entre eje, tramo del plano y elemento de
  OpenSees.

Los planos `305` a `310` se encuentran en la carpeta de referencia externa:
`Proyecto1/Planos/DXF/LT1_DXF`. La tabla conserva los datos observados en el
DXF, pero la asignacion de cada llamada a un tramo vertical especifico debe
confirmarse visualmente antes de incorporarla al modelo Fiber.
