# Informe P1A4 - Laboratorio estructural interactivo v1

## Datos generales

| Campo | Informacion |
| --- | --- |
| Asignatura | Metodos Computacionales en Ingenieria de Obras Civiles |
| Laboratorio | Semana 5 - P1A4 |
| Modelo | Edificio 3D en OpenSees y visor interactivo en Unity |
| Ejecucion | `Edificio/analysis/load_cases/ejecutar.py` |
| Escena Unity | `Edificio/visualization/unity/UnityVisualization/Assets/Main.unity` |
| Unidades | kN, m, s, radianes y kN/m2 |

## Introduccion

El objetivo de esta entrega es integrar el modelo estructural elastico con un
visor Unity que permita inspeccionar geometria, cargas, respuestas y capacidad.
El visor funciona como postprocesador: OpenSees/Python genera los resultados y
Unity los lee desde archivos JSON y CSV. La escena no ejecuta nuevamente el
analisis estructural.

La corrida base contiene 1251 nodos, 694 elementos y 82 panos de muro separados
por piso. La separacion de muros conserva los nodos compartidos entre niveles,
por lo que no introduce desconexiones estructurales.

## 1. Funciones implementadas

La palabra implementada describe la existencia de la funcion en el codigo; no
significa que haya sido probada en un telefono fisico.

| Funcion | Estado | Implementacion y limite |
|---|---|---|
| Navegacion | Implementada | `OrbitCamera.cs` permite orbita, zoom, desplazamiento y reinicio de vista. Incluye gestos de uno y dos dedos, pendientes de prueba fisica. |
| Seleccion | Implementada | `ElementInspector.cs` usa raycast, resaltado, filtros, busqueda e identificacion por ID. Elementos interiores pueden quedar ocluidos. |
| Apoyos | Implementada | `BuildingVisualizer.cs` visualiza las restricciones exportadas. El simbolo no reemplaza una tabla de los seis GDL. |
| Ejes | Implementada | Se muestran `xlocal`, `ylocal` y `zlocal`. OpenSees usa XYZ y Unity representa la vertical Z como Y. |
| Cargas | Implementada | `Semana3Visualizer.cs` muestra G, Q, EX, EY y R, incluyendo fuerzas de piso y centros de masa. No es un editor libre de cargas en tiempo real. |
| Areas tributarias | Parcial | La transferencia y conservacion numerica estan implementadas. El dibujo interactivo es aproximado y no debe usarse para medir el reparto exacto. |
| Deformada | Implementada | Usa desplazamientos exportados, interpolacion de barras y bordes de shells, con escala visual configurable. |
| Diagramas | Implementada para barras | Se muestran N, Vy, Vz, My y Mz en 41 estaciones por barra. Las cargas distribuidas se incorporan al analisis y los resultados de estaciones se exportan desde OpenSees para Unity. |
| Superposicion | Implementada y verificada | Se combinan G, Q, EX y EY mediante ponderadores lambda. Es valida para el modelo lineal exportado. |
| P-M | Implementada | `SectionGraphs.cs` y `WallSectionGraphs.cs` muestran capacidad nominal y demanda de columnas y panos de muro. La comparacion es uniaxial y no normativa. |
| Modificacion del modelo | Implementada | Se probó el flujo dato -> modelo -> OpenSees -> resultados -> Resources -> Unity con dos variantes. |

## 2. Flujo de integracion

```text
datos de entrada
    -> analisis Python/OpenSees
    -> resultados CSV, JSON y NPZ
    -> exportacion de Resources
    -> carga de la escena Unity
    -> seleccion e inspeccion visual
```

El contrato principal es `Edificio/results/modelo_3d_manual.json`. Los
identificadores, nodos, conectividades, propiedades y casos se conservan desde
el modelo hasta el objeto visual. En muros, `source_wall_id` y `floor` permiten
relacionar cada pano con su muro de origen y su nivel.

El archivo `Edificio/results/README.md` describe los resultados generados. Para
regenerar el conjunto completo se ejecuta:

```powershell
python Edificio/analysis/load_cases/ejecutar.py
```

## 3. Dos modificaciones completas

En ambos casos las variantes parten del estado base y no se acumulan entre si.
El flujo manual reproducible es modificar un archivo de parametros, ejecutar el
analisis, regenerar los Resources y reiniciar Play en Unity.

### 3.1 Modificacion A: carga viva uniforme

En la primera variante se reemplazo la distribucion zonificada de la carga viva
por una intensidad uniforme de `4 kN/m2` sobre las areas cargadas. El valor se
introdujo mediante el parametro `q_Q_kN_m2`. A partir de ese cambio, el modelo
transformo las areas tributarias en nuevas fuerzas Q, volvio a resolver el
edificio y exporto los resultados actualizados para Unity. Como la carga viva
participa en la masa sismica, tambien se recalcularon las respuestas EX y EY.

| Magnitud | Base | Variante Q = 4 kN/m2 |
|---|---:|---:|
| Carga Q vertical total [kN] | 24606.921695 | 25807.238184 |
| Maximo absoluto uz de Q [mm] | 6.564188 | 6.050321 |
| Fuerza EX total [kN] | 19402.148829 | 19522.180478 |

El archivo de parametros es
`Edificio/documentation/semana05_evidencias/mod_Q_parametros.json` y el
resumen de resultados y equilibrio esta en `modificaciones.json`.

### 3.2 Modificacion B: aceleracion pseudoestatica

En la segunda variante se aumento la aceleracion pseudoestatica horizontal desde
`0.20 g` hasta `0.25 g`, manteniendo sin cambios la geometria, la rigidez, las
masas y el patron de aceleracion. El nuevo valor se introdujo mediante el
parametro `aceleracion_fraccion_g`. Luego se recalcularon las fuerzas sismicas
como masa por aceleracion, OpenSees volvio a resolver los casos EX y EY y los
resultados se exportaron nuevamente para su inspeccion en Unity.

| Magnitud | Base 0.20 g | Variante 0.25 g |
|---|---:|---:|
| Fuerza EX total [kN] | 19402.148829 | 24252.686036 |
| Maximo absoluto ux de EX [mm] | 5.209994 | 6.512491 |
| Razon de desplazamientos variante/base | - | 1.249999665 |

La razon coincide con 0.25/0.20 = 1.25 dentro del error numerico. El archivo de
parametros es `mod_sismo_parametros.json`; los resultados contrastables estan
en `Edificio/documentation/semana05_evidencias/modificaciones.json`.

## 4. Superposicion interactiva

La escena permite seleccionar R y aplicar cuatro factores en el orden G, Q, EX,
EY. Se mantuvieron alphaG = 1 y alphaQ = 0.5. Cada estado se contrasto contra
desplazamientos, reacciones y fuerzas locales de una solucion explicita.

| Estado | lambdaG | lambdaQ | lambdaEX | lambdaEY | Error max. traslaciones | Error max. reacciones | Error max. fuerzas |
|---|---:|---:|---:|---:|---:|---:|---:|
| S1 | 1.0 | 1.0 | 0.0 | 0.0 | 3.583e-8 | 3.113e-8 | 3.952e-8 |
| S2 | 1.2 | 1.4 | 0.8 | -0.3 | 9.824e-8 | 1.282e-7 | 3.142e-7 |
| S3 | 1.0 | 0.5 | -1.0 | 0.7 | 3.117e-7 | 3.279e-7 | 9.983e-7 |

Todos cumplen la tolerancia relativa 1e-5. Ejemplos para el nodo 900116 y la
barra 15:

| Estado | Respuesta | Superpuesta | Explicita |
|---|---|---:|---:|
| S1 | uz nodo 900116 [m] | -0.027240596712 | -0.027240596223 |
| S2 | uz nodo 900116 [m] | -0.034979511052 | -0.034979511690 |
| S3 | uz nodo 900116 [m] | -0.022422445938 | -0.022422446950 |
| S1 | Ni barra 15 [kN] | 5875.709110 | 5875.709108 |
| S2 | Ni barra 15 [kN] | 7378.855645 | 7378.855638 |
| S3 | Ni barra 15 [kN] | 5049.985072 | 5049.985057 |

La evidencia completa esta en `superposicion.csv` y `superposicion.json` dentro
de `Edificio/documentation/semana05_evidencias`. Los errores se expresan en
coordenadas OpenSees; el cambio XYZ a XZY solo afecta la representacion Unity.

## 5. Sidequest: carga movil SQ4

La carga movil se implemento como una carga viva vertical, localizada, lineal
elastica y cuasiestatica. El avatar representa la posicion de la carga; su
velocidad de recorrido es solamente visual y no introduce impacto, inercia,
vibracion, fisuracion ni plastificacion.

### 5.1 Regla fisica

La funcionalidad se habilita sobre 20 paneles completos: cuatro paneles en cada
uno de los cinco niveles `Z = 3.96, 7.92, 11.88, 15.84 y 19.80 m`. La franja
recorrible esta entre `X = -28.30` y `X = -24.55 m`. Se excluyen vacios y
paneles cuyos bordes no coinciden completamente con dos vigas receptoras.

Para una carga vertical `P` ubicada en `(x,y)`, se define:

```text
eta = (y - y_min)/(y_max - y_min)
P_inferior = P(1 - eta)
P_superior = P eta
```

Las dos fuerzas se aplican en las vigas opuestas, en la proyeccion longitudinal
`x` de la posicion del avatar y respetando el sentido local `i -> j` de cada
barra. Esta es una regla didactica unidireccional para la carga localizada; no
reemplaza el reparto gravitacional a 45 grados de G/Q ni modela la losa mediante
elementos finitos.

La regla conserva tanto la fuerza como el primer momento:

```text
P_inferior + P_superior = P
P_inferior*r_inferior + P_superior*r_superior = P*r
```

En un borde compartido entre dos paneles, ambas referencias apuntan a la misma
viga receptora. Por ello, la carga no se duplica al cruzar de un panel al
siguiente.

### 5.2 Panel de control

El acceso se realiza desde el boton **Explorar carga movil** del panel izquierdo.
El usuario puede:

- Definir `P` entre 0 y 100 kN, con accesos rapidos para 0, 1, 10 y 50 kN.
- Seleccionar panel y nivel.
- Arrastrar la posicion en planta.
- Moverse con `W/A/S/D`.
- Cambiar de panel o nivel mediante los selectores.
- Usar **Recorrer**, **Pausar**, **Centrar** y **Enfocar**.
- Salir con **Volver a casos del edificio**, restaurando las capas anteriores.

La tarjeta de informacion muestra la carga recibida por cada viga, su porcentaje,
el error de conservacion de fuerza y momento y la suma de reacciones verticales.
Con `P = 0` desaparecen las flechas y la respuesta incremental es nula.

### 5.3 Reparto y respuesta estructural

El recorrido utiliza 25 vigas receptoras. Para cada una se resolvieron cuatro
casos OpenSees independientes de 1 kN en `x/L = 0, 1/3, 2/3 y 1`. Las
respuestas nodales y acciones de extremo para una posicion intermedia se
interpolan mediante Lagrange. La magnitud y el reparto se superponen
linealmente, por lo que Unity actualiza la respuesta sin ejecutar Python en cada
cuadro.

La respuesta de las vigas cargadas incluye la carga puntual dentro de la
integracion de curvatura. El diagrama presenta el salto de corte y el cambio de
pendiente del momento en el punto de aplicacion. El maximo de momento se busca
en los extremos y en la carga, y el maximo de desplazamiento se revisa en 101
puntos por viga. Las demas barras utilizan la deformada Hermite existente.

Los valores mostrados como `Delta` corresponden unicamente al incremento
producido por SQ4; no son la respuesta total `G + Q + sismo + SQ4` ni una
verificacion de capacidad.

### 5.4 Evidencia de conservacion

La auditoria `Edificio/results/verificacion_carga_movil.json` contiene 148
controles SQ4, todos correctos. Se verificaron:

- Conservacion de la fuerza total transferida.
- Conservacion del primer momento de la carga.
- Balance de fuerzas verticales en los apoyos.
- Bordes compartidos sin duplicacion.
- Caso de carga cero.
- Posiciones interiores, extremos y magnitudes diferentes.
- Respuestas de todas las barras y reacciones contra soluciones explicitas.

Tambien se verificaron 15 transiciones entre paneles vecinos usando el mismo
receptor, sin duplicar `P`. La prueba automatizada completa termino con 25
pruebas correctas. El control de ejecucion Unity `SQ4_RUNTIME_OK` comparo la
deformada en el extremo con el desplazamiento nodal y comprobo respuesta nula,
con un error maximo aproximado de `1.46e-11 m`.

### 5.5 Respuesta visual

La interfaz visual distingue:

- Avatar y flechas proporcionales a `P`.
- Panel activo resaltado.
- Viga inferior receptora en turquesa.
- Viga superior receptora en ambar.
- Porcentajes y magnitudes en kN.
- Deformada incremental en rosa.
- Diagramas `Delta My` y `Delta Vz`.
- Valores en los extremos y maximo interior.

La evidencia visual corresponde a una ejecucion Windows a 1440 x 900:

![Carga movil centrada](assets/carga_movil/carga_movil_centro.png)

En la carga centrada se observa `25 + 25 = 50 kN`.

![Carga movil excéntrica](assets/carga_movil/carga_movil_reparto.png)

En una posicion excéntrica se observa `9 + 41 = 50 kN`, de acuerdo con `eta`.

![Carga movil cero](assets/carga_movil/carga_movil_cero.png)

Con carga cero se comprueba que la respuesta incremental desaparece.

La implementacion y sus limites estan documentados en
`Edificio/documentation/CARGA_MOVIL.md`. La escena puede probarse con **Play**
en `Assets/Main.unity` y luego seleccionando **Explorar carga movil**.

## 6. Evaluacion UX estructural

| Pregunta | Respuesta del viewer | Evaluacion |
|---|---|---|
| Donde esta el elemento? | Seleccion directa, ID, filtros, resaltado y camara orbital. | Util en escritorio; falta validacion con usuarios. |
| Como esta apoyado? | Capa visual derivada de restricciones exportadas. | Parcial; conviene mostrar explicitamente los seis GDL. |
| Que lo carga? | Inspector de losas con G/Q, receptores, centros de masa y flechas sismicas. | Permite seguir el camino de carga, pero el dibujo tributario es aproximado. |
| Como se deforma? | Deformada amplificada, valor numerico y escala configurable. | Debe distinguirse siempre la amplificacion de la respuesta fisica. |
| Que fuerzas tiene? | Acciones locales y diagramas de barras; muros con resultantes del corte inferior. | Util para inspeccion, sujeto a las hipotesis del postproceso. |
| Cuanta capacidad tiene? | Curvas P-M y momento-curvatura con punto de demanda. | Comparacion nominal uniaxial, no diseño normativo completo. |

## 7. Preparacion movil

El dispositivo objetivo es iPhone 15 con iOS 26. Se prepararon los controles de
interfaz, area segura, paneles vertical/horizontal, navegacion tactil y
exportacion iOS en `Assets/Editor/BuildMobile.cs`. La camara usa un dedo para
orbitar y dos dedos para zoom y desplazamiento.

Se comprobo la compilacion de scripts C# en Unity de escritorio. La generacion
de IPA/proyecto Xcode no pudo completarse porque el editor disponible no tenia
instalado iOS Build Support. No se probó el visor en el telefono ni se midieron
FPS, tiempos de carga o legibilidad fisica.

La documentacion del estado movil y sus limites esta en
`Edificio/documentation/interfaz_celular.md`.

## 8. IA: funcionalidad compleja y verificacion

### Funcionalidad implementada por el agente

La funcionalidad compleja implementada con apoyo directo de un agente fue el
sidequest **SQ4: carga movil sobre paneles del edificio**. El agente participo
en el diseño del flujo, la implementacion del calculo y la integracion con
Unity. La funcionalidad completa permite seleccionar una carga `P`, ubicarla en
un panel, repartirla entre dos vigas receptoras, calcular la respuesta
estructural incremental y visualizar la deformada y los diagramas `Delta My` y
`Delta Vz`.

Los principales archivos implementados o integrados fueron:

- `Edificio/analysis/load_cases/carga_movil.py`: genera las bases de respuesta de las 25 vigas receptoras y la auditoria SQ4.
- `Edificio/analysis/load_cases/casos.py`: incorpora los resultados de las cargas interiores de barra y sus diagramas por estaciones.
- `Edificio/visualization/unity/UnityVisualization/Assets/Scripts/MovingLoadViewer.cs`: controla el panel, la posicion, el reparto y la respuesta visual.
- `Edificio/visualization/exports/exportar_resultados_unity.py`: exporta los resultados que consume Unity.
- `Edificio/visualization/unity/UnityVisualization/Assets/Resources/carga_movil.json`: contrato entre el analisis y el visor.

### Verificacion de la funcionalidad

La verificacion fue realizada de forma independiente a la asistencia del agente.
Primero se comprobo la implementacion numericamente mediante:

```powershell
python Edificio/analysis/load_cases/ejecutar.py --carga-movil
python -m unittest discover -s Edificio/verification/tests -p "test_*.py"
```

La primera ejecucion regenero el analisis, los recursos de Unity y
`Edificio/results/verificacion_carga_movil.json`. La auditoria obtuvo 148
controles en estado `OK`. La segunda ejecucion termino con 25 pruebas correctas.

Ademas, se verifico que:

- La suma de las cargas repartidas es igual a `P`.
- El primer momento de las cargas repartidas coincide con el momento aplicado.
- Los bordes compartidos entre paneles no duplican la carga.
- La respuesta para `P = 0` es nula.
- Las respuestas interpoladas coinciden con soluciones explicitas de OpenSees.
- La deformada integrada coincide con una viga subdividida en los casos biapoyado y biempotrado.
- Unity completa la comprobacion `SQ4_RUNTIME_OK`.

Finalmente, se comprobo visualmente el ejecutable Windows con carga centrada,
carga excentrica y carga cero. Esta verificacion se hizo en escritorio; no se
presenta como una prueba en iPhone ni como una validacion normativa. El detalle
tecnico se encuentra en `Edificio/documentation/CARGA_MOVIL.md`.

## 9. Resultados y verificaciones

| Control | Resultado |
|---|---|
| Demanda-capacidad total | 1650 elementos revisados, 1650 cumplen y 0 fuera de capacidad |
| Muros | 410 revisiones, todas dentro de capacidad nominal |
| Columnas | 1240 revisiones, todas dentro de capacidad nominal |
| Pruebas automatizadas | 25 pruebas, todas OK |
| Auditoria SQ4 | 148 controles, todos OK; 20 paneles y 25 vigas receptoras |
| Conservacion de carga Q | Error absoluto maximo aproximado 4.855e-5 kN |

El equilibrio y los resultados globales deben consultarse en
`Edificio/results/verificaciones_globales.csv` y
`Edificio/results/resumen_global.json`. Las curvas de capacidad se encuentran
en `capacidad_HA.png` y `capacidad_PM_muros.png`.

## Limitaciones

- El modelo es lineal elastico y academico.
- Las losas transfieren cargas tributarias; no tienen elementos finitos ni esfuerzos internos de placa.
- Las curvas P-M son nominales y uniaxiales; no incluyen automaticamente factores phi, corte, esbeltez, segundo orden ni interaccion biaxial completa.
- La armadura de borde adoptada para algunos muros es un supuesto academico pendiente de confirmar con planos.
- La deformada y los diagramas tienen componentes amplificadas o reconstruidas para visualizacion.
- La evidencia numerica esta disponible; la prueba visual interactiva y la prueba fisica en iPhone siguen pendientes.

## Conclusiones

Se integro el modelo OpenSees con Unity mediante un contrato de datos trazable,
con inspeccion por ID, cargas, deformada, diagramas de barras, curvas P-M y una
carga movil SQ4. Se documentaron dos modificaciones completas, tres estados de
superposicion con errores inferiores a 1e-5 frente a soluciones explicitas y
una carga movil con conservacion de fuerza y momento verificada.

El entregable movil para iPhone queda preparado en codigo, pero bloqueado para
generar IPA por falta del modulo iOS Build Support. La carga movil SQ4 fue
verificada en el visor Windows, no en iPhone/iOS.

## Checklist P1A4

| Requisito | Ubicacion en este informe | Estado |
|---|---|---|
| Funciones implementadas | Seccion 1 | Cubierto |
| Dos modificaciones completas | Seccion 3 | Cubierto |
| Tres estados de superposicion | Seccion 4 | Cubierto |
| Sidequest de carga movil | Seccion 5 | Implementado y verificado |
| Evaluacion UX | Seccion 6 | Cubierto con limitaciones |
| Preparacion movil | Seccion 7 | Preparada; build iOS pendiente por entorno |
| Funcionalidad compleja con IA | Seccion 8 | Implementada y verificada |

## Referencias y archivos principales

- Enunciado: `Enunciados e Instrucciones/SEMANA_4/P1A4_AVANCE_laboratorio_estructural_interactivo_v1_Entregable.txt`.
- Modelo y resultados: `Edificio/results/README.md`.
- Postproceso: `Edificio/documentation/POSTPROCESO_SEMANA4.md`.
- Interfaz movil: `Edificio/documentation/interfaz_celular.md`.
- Trazabilidad OpenSees-Unity: `Edificio/documentation/TRAZABILIDAD_P1L4.md`.
- Evidencia de modificaciones: `Edificio/documentation/semana05_evidencias/modificaciones.json`.
- Evidencia de superposicion: `Edificio/documentation/semana05_evidencias/superposicion.csv` y `superposicion.json`.
- Entrega de vigas y carga movil: `reports/2026-09-21_vigas_y_carga_movil.md`.
- Evidencia visual SQ4: `reports/assets/carga_movil/`.
