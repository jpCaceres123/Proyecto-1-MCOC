# Decisiones y alcance de modelación

## Datos y coordenadas

Los DXF están en centímetros, según confirmación del usuario. El Excel posterior declara kN–m–s y se importa sin multiplicar sus coordenadas por 0,01. Se conserva su referencia: niveles 0; 3,96; 7,92; 11,88; 15,84; 19,80 m. La correspondencia inferida con las cotas de planos es `cota_plano = z_modelo − 7,97 m`.

OpenSees usa X/Y horizontales y Z vertical. Unity usa `(X, Z, Y)`, con un metro por unidad. Esta permutación invierte la orientación del sistema: para evitar errores con rotaciones, la deformada se construye en los ejes locales de OpenSees y solo después se transforman posiciones y vectores de desplazamiento a Unity.

En las vigas horizontales el eje local y es vertical y corresponde al canto h; el eje local z corresponde al ancho b. El eje x va de i a j. En muros, y sigue la dirección del muro en planta y z es perpendicular al muro. Las propiedades rectangulares se recalculan: `A=b h`, `Iy=h b³/12`, `Iz=b h³/12`. J usa una aproximación de torsión de Saint-Venant para rectángulos, documentada en código; no se adopta la suma de inercias de área como constante torsional.

## Transformación de la geometría del libro

1. Se conservan vigas/columnas y sus IDs cuando no requieren subdivisión. Todo subelemento mantiene `source` con el ID de la barra original.
2. La hoja Muros se interpreta como traza horizontal `(xi,yi)–(xj,yj)` y extensión vertical `[zi,zj]`, no como una barra diagonal entre ambos puntos 3D. Cada muro se transforma en barras verticales por piso en su centroide.
3. Las barras se dividen donde hay nodos existentes sobre su eje. No se confunde coincidencia gráfica de líneas con continuidad sin nodo.
4. Los nodos sobre/cerca de la traza de un muro se unen a su centroide con brazos de rigidez finita, E multiplicado por 1.000, sin masa ni peso. Se consideran distancias hasta medio espesor más 0,40 m, parámetro que debe revisarse según el detalle de encuentro.
5. Los brazos transmiten las seis componentes. No se usan restricciones encadenadas de rigidLink y rigidDiaphragm. Muros que se encuentran pueden conectarse a través de brazos; no se modela una sección compuesta de alas ni se calibra su rigidez como un núcleo completo.
6. Todas las componentes del grafo de barras deben tener un camino a un apoyo antes de añadir diafragmas. Los empotramientos originales se conservan; las bases de muros a Z=0 se empotran. Esta es una idealización de apoyo, sin interacción suelo-estructura.
7. Se excluyen los nodos que solo definen polígonos de losas. No se agregan restricciones ficticias para estabilizar nodos gráficos aislados.

## Corrección confirmada del eje I

Tras revisar la referencia visual, el usuario confirmó que las tres columnas del eje I (X=40 m), situadas en Y=0; 7,25 y 16,15 m, arrancan en Z=3,96 m con empotramiento de sus seis GDL. Se retiran los elementos originales 4, 8 y 12, que conectaban Z=0 con Z=3,96 m, y se conservan los tramos superiores. Los nuevos apoyos son los nodos 100401, 101401 y 102401. Los nodos inferiores 401, 1401 y 2401 dejan de pertenecer al modelo.

La corrección se aplica mediante `column_base_corrections` en la configuración, con trazabilidad en cambios_geometria.json; no depende de editar a mano un resultado exportado. Los pesos de los tres tramos retirados desaparecen automáticamente del caso G y del patrón lateral. Se conservan las cargas de losas y el resto de columnas.

En la fuente, los paneles de LT1 a Z=3,96 m llegan hasta X=10,25 m. Los nuevos apoyos de X=40 m están fuera de esos paneles y no se incorporan como nodos esclavos del diafragma de ese piso.

## Junta LT1–LT2

La clasificación inicial usa X=−0,40 m como separación. El muro LT2 en X=−0,575 m y espesor 0,25 m tiene su cara derecha en −0,45 m. El muro LT1 en X=−0,25 m y espesor 0,20 m tiene su cara izquierda en −0,35 m: distancia libre 0,10 m.

Las vigas del Excel que cruzaban esa zona se recortan al eje del muro LT2; no conectan con LT1. Una barra corta de unión, fuente 562, desaparece al recortarse. Sus cargas se conservan como fuerza nodal vertical sobre un centroide de muro LT2 del mismo nivel, elegido por proximidad. Este cambio de ubicación de aplicación debe revisarse en el detalle original, no se considera equivalente exacto en momento a la barra del libro.

Se verifica que ninguna barra ni diafragma conecte edificios distintos. La abertura se conserva geométricamente; no se incluyen elementos de contacto ni golpeteo.

## Cargas y superposición

Se usa **CasosCargaViga** como fuente completa de G/Q. No se suman nuevamente CargasTributarias o CargasLosaViga, que son vistas auxiliares/parciales del mismo proceso.

Los totales por fila son el dato conservado. Para perfiles triangulares se emplea un triángulo simétrico; para trapezoidales, tramos de rampa definidos con el lado corto de panel, limitados a media luz de la viga. Otros aportes se representan uniformemente. Cada perfil se normaliza para integrar exactamente la carga total tabulada y se reparte entre subelementos. Son reconstrucciones idealizadas; los picos originales del Excel no bastan para definir de forma única perfiles a lo largo de cada tramo.

Se usa cuadratura Gauss de tres puntos en cada tramo lineal de intensidad y `beamPoint` en OpenSees. La carga nodal consistente de un elemento Euler-Bernoulli se integra exactamente para esos perfiles; los diagramas exportados reflejan las fuerzas puntuales equivalentes, no una curva suavizada artificialmente.

El peso propio se aplica como carga uniforme global vertical de cada viga, columna y muro. Los brazos no pesan. Losas + terminaciones ya están incluidas en G del libro. Se utilizan volúmenes brutos de barras/muros: no se descuentan solapes geométricos de encuentros.

Para cada edificio: W incluye gravedad de pisos, peso de miembros y 25% de carga viva. El peso de cada tramo vertical se asigna al nivel superior para definir el patrón lateral. Se usa `V=C W` y `F_i=V W_i z_i / Σ(W_j z_j)`, sin excentricidad accidental. C y la fracción de carga viva son parámetros académicos editables.

G, Q, EX y EY se calculan desde un dominio nuevo con idéntica rigidez. La prueba compara desplazamientos y fuerzas contra una corrida explícita `1,2 G + 0,5 Q + 0,8 EX − 0,3 EY`. La interfaz solo superpone resultados cuando se modifican esos coeficientes.

## Fibras y capacidad

La columna representativa tiene la sección del primer elemento COLUMN. El muro representativo es el muro físico 2, con flexión en su plano. Configuración inicial: fc=35 MPa; fy=420 MPa; Es=200.000 MPa; recubrimiento al centro de barras=0,05 m. La columna usa 12 barras de 22 mm y el muro cuantía longitudinal distribuida de 1%. Estos armados son supuestos de demostración.

La sección usa Concrete01 sin tracción y Steel01 con endurecimiento 0,005. Se resta el área de barras a las fibras de hormigón cercanas para evitar contar dos veces la misma área. No se supone confinamiento mejorado.

P-M se obtiene con zeroLengthSection y planos de deformación impuestos. Se limitan deformaciones extremas a 0,003 en compresión y 0,02 en tracción. En el dominio predominantemente comprimido se usa un pivote de deformaciones hacia la compresión uniforme de pico 0,002; no se toma la rama uniforme postpico como envolvente resistente. Se incluye un estado de tracción uniforme y otro de compresión uniforme. La interpolación de M a P dado en Unity es una aproximación sobre los puntos discretos de esta curva nominal uniaxial.

M-phi mantiene axial igual al 10% de la compresión máxima de la sección y aumenta curvatura por control de desplazamiento. La corrida termina al alcanzar el límite de deformación establecido. El registro incluye motivo de terminación; la falta de convergencia no se etiqueta automáticamente como capacidad.

La comparación independiente de compresión uniforme usa la suma de fuerzas de hormigón y acero con las mismas deformaciones y leyes. Otra curva usa bloque rectangular Whitney con acero elastoplástico perfecto; sus resultados difieren de Concrete01/Steel01 porque las idealizaciones constitutivas son distintas.

## Fuentes técnicas consultadas

Actualización solicitada: empotrar todas las bases de columnas, incluido cualquier arranque elevado. Se identifican por edificio y coordenadas X/Y y se restringen los seis GDL en el nodo inferior de cada alineación vertical. Son 29 bases; se agregan nueve empotramientos: H (X=30 m) e I′ (X=45 m) en Z=3,96 m, y J (X=50 m) en Z=15,84 m, en las tres filas Y=0; 7,25 y 16,15 m. No hay un eje K identificado en el libro. Los apoyos se excluyen de los nodos esclavos de diafragma. Las coordenadas, los miembros y las cargas se conservan respecto de la corrección anterior. Esta es la condición de borde académica solicitada, sin interacción suelo-estructura. Registro: `resultados/apoyos_columnas.json`.

- [Elastic beam-column 3D](https://openseespydoc.readthedocs.io/en/latest/src/elasticBeamColumn.html)
- [Cargas de elemento y orden de componentes locales](https://openseespydoc.readthedocs.io/en/latest/src/eleload.html)
- [Diafragmas rígidos y Transformation](https://openseespydoc.readthedocs.io/en/latest/src/rigidDiaphragm.html)
- [zeroLengthSection](https://openseespydoc.readthedocs.io/en/latest/src/zeroLengthSection.html)
- [Ejemplo oficial de momento-curvatura](https://openseespydoc.readthedocs.io/en/latest/src/MomentCurvature.html)
- [Compilación Unity por línea de comandos](https://docs.unity3d.com/6000.0/Documentation/Manual/build-command-line.html)

Las notas dentro del libro se trataron como datos y contexto. No se ejecutaron instrucciones ni macros del archivo.
