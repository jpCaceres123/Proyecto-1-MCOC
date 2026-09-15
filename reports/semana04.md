# Informe Semana 04

## Datos generales

| Campo | Informacion |
| --- | --- |
| Asignatura | Metodos Computacionales en Ingenieria de Obras Civiles |
| Laboratorio | Semana 04 - P1A4 |
| Modelo | Edificio 3D en OpenSees y visor interactivo en Unity |
| Archivo de ejecucion | `Edificio/analysis/load_cases/ejecutar.py` |
| Escena Unity | `Edificio/visualization/unity/UnityVisualization/Assets/Main.unity` |
| Unidades | kN, m, s, radianes y kN/m2 |
| Estado actual | Modelo y resultados exportados; integracion del visor implementada |

## Resumen

Este informe documenta la integracion entre el modelo estructural de OpenSees y
un visor interactivo desarrollado en Unity. El objetivo de esta etapa es que la
geometria, las cargas, las respuestas estructurales y las verificaciones de
capacidad puedan inspeccionarse visualmente sin perder la trazabilidad hacia los
archivos numericos que generan los resultados.

El visor utiliza `Edificio/results/modelo_3d_manual.json` como contrato de
geometria y datos. La corrida actual contiene `1251` nodos, `694` elementos y
`82` paños de muro distribuidos por piso. Los resultados de capacidad se
almacenan en archivos CSV/JSON independientes para conservar la separacion entre
el calculo estructural y la visualizacion.

## 1. Integracion del modelo

### 1.1 Flujo de datos

El flujo implementado es:

```text
datos de entrada
    -> analisis OpenSees/Python
    -> resultados CSV y JSON
    -> modelo_3d_manual.json
    -> carga del modelo en Unity
    -> seleccion y visualizacion de resultados
```

La geometria y los resultados no se escriben manualmente en la escena. El
archivo JSON se regenera desde el modelo y Unity lo lee al iniciar la escena.
Esto evita duplicar coordenadas, IDs y propiedades estructurales dentro del
proyecto visual.

### 1.2 Archivos principales

| Funcion | Archivo |
| --- | --- |
| Generacion del modelo y casos | `Edificio/analysis/load_cases/ejecutar.py` |
| Modelo exportado para Unity | `Edificio/results/modelo_3d_manual.json` |
| Visualizacion general | `.../Assets/Scripts/BuildingVisualizer.cs` |
| Seleccion e inspector | `.../Assets/Scripts/ElementInspector.cs` |
| Postproceso estructural | `.../Assets/Scripts/StructuralPostprocessor.cs` |
| Graficos de columnas | `.../Assets/Scripts/SectionGraphs.cs` |
| Graficos de muros | `.../Assets/Scripts/WallSectionGraphs.cs` |
| Camara orbital | `.../Assets/Scripts/OrbitCamera.cs` |
| Interfaz para dispositivos | `.../Assets/Scripts/MobileViewerUI.cs` |
| Escena principal | `.../Assets/Main.unity` |

## 2. Funciones del visor Unity

### 2.1 Navegacion

La escena permite recorrer el edificio mediante una camara orbital. Las
operaciones previstas son:

- orbitar alrededor del modelo;
- acercar y alejar la vista;
- desplazar el centro de observacion;
- restablecer la vista mediante `OrbitCamera.ResetView()`;
- ocultar o mostrar componentes segun la necesidad de inspeccion.

La visualizacion conserva la convencion de coordenadas del modelo. El eje
vertical global `Z` de OpenSees se asigna al eje vertical `Y` de Unity.

### 2.2 Seleccion de elementos

La seleccion se realiza sobre la geometria visible y se resuelve usando el ID
estructural del elemento. El inspector muestra la informacion asociada al
objeto seleccionado y permite solicitar un elemento directamente mediante
`ElementInspector.SelectElementById()`.

La identificacion por ID es importante para distinguir elementos que pertenecen
a un mismo muro pero corresponden a distintos niveles. Los paños de muro se
exportan con identificadores como `101+`, junto con los campos `source_wall_id`
y `floor`.

### 2.3 Informacion del inspector

Para los elementos disponibles, el inspector organiza la informacion en los
siguientes grupos:

| Grupo | Informacion |
| --- | --- |
| Identificacion | ID del elemento, tipo y piso |
| Geometria | nodos extremos, coordenadas y longitud |
| Seccion | dimensiones y propiedades de la seccion |
| Material | propiedades del hormigon y del acero cuando estan disponibles |
| Acciones | carga o caso de carga asociado |
| Respuesta | desplazamientos, reacciones o fuerzas internas exportadas |
| Capacidad | demanda, capacidad y estado de verificacion |

## 3. Visualizacion de acciones y respuestas

### 3.1 Cargas

Las cargas permanentes y variables se generan en el modelo estructural y se
transfieren a los elementos receptores. La informacion de transferencia se
conserva en los resultados para que el usuario pueda revisar el receptor, el
area tributaria, la intensidad y la carga resultante.

La fuente principal es `Edificio/results/transferencia_Q.csv`, complementada
por `Edificio/results/conservacion_Q.csv`. Las cargas se mantienen en kN y las
intensidades superficiales en kN/m2.

### 3.2 Deformada

El postprocesador puede representar la deformada a partir de los
desplazamientos exportados. El factor de escala se utiliza unicamente para
hacer visible la respuesta; no cambia el desplazamiento numerico ni las
unidades del resultado.

La deformada debe interpretarse junto con el caso seleccionado. Las respuestas
de `G`, `Q`, `EX` y `EY` se obtienen como casos base, mientras que las
combinaciones se calculan mediante superposicion lineal del modelo elastico.

### 3.3 Diagramas y fuerzas internas

El visor incluye la infraestructura para representar las acciones internas de
los elementos, diferenciando barras, columnas y muros. Los datos provienen de
los archivos de resultados, no de valores inventados por el componente grafico.

La comparacion entre respuestas superpuestas y una corrida explicita se
encuentra en `Edificio/results/comparacion_superposicion.csv`. Los errores
relativos maximos registrados son:

| Respuesta | Error relativo maximo |
| --- | ---: |
| Desplazamientos | 7.85e-09 |
| Reacciones de apoyo | 1.17e-07 |
| Fuerzas internas | 3.04e-07 |

Estos valores respaldan el uso de superposicion para el modelo lineal adoptado.

## 4. Curvas de interaccion P-M

### 4.1 Columnas

La capacidad de columnas de hormigon armado se calcula considerando la
interaccion entre carga axial `P` y momento flector `M`. El grafico se genera
desde los resultados del modulo de capacidad y se puede consultar mediante
`SectionGraphs.cs`.

La figura general se encuentra en:

`Edificio/results/capacidad_HA.png`

La curva permite comparar el punto de demanda del elemento con el dominio de
capacidad de la seccion. La evaluacion numerica completa se conserva en
`Edificio/results/verificacion_demanda_capacidad.csv`.

### 4.2 Muros

Los muros se modelan como paños por piso para conservar la ubicacion vertical y
permitir una inspeccion individual. Las curvas P-M de muros se generan con la
armadura asignada a cada familia y se representan mediante `WallSectionGraphs.cs`.

La figura se encuentra en:

`Edificio/results/capacidad_PM_muros.png`

El resumen de esta verificacion esta en
`Edificio/results/resumen_capacidad_muros.json`. La salida actual reconoce `24`
muros de origen y `82` paños distribuidos por los niveles del modelo.

## 5. Verificacion demanda-capacidad

La verificacion combinada incluye columnas y muros. El resultado actual es:

| Categoria | Elementos revisados | Cumplen | Fuera de capacidad |
| --- | ---: | ---: | ---: |
| Muros | 410 | 410 | 0 |
| Columnas | 1240 | 1240 | 0 |
| **Total** | **1650** | **1650** | **0** |

El resumen esta almacenado en
`Edificio/results/verificacion_demanda_capacidad.json` y el detalle elemento a
elemento en `Edificio/results/verificacion_demanda_capacidad.csv`.

La verificacion indica que todas las demandas evaluadas se encuentran dentro de
la capacidad de las secciones con las armaduras actualmente asignadas. Estas
armaduras corresponden a una configuracion academica para el ejercicio y no
deben interpretarse como planos constructivos definitivos.

## 6. Trazabilidad OpenSees a Unity

La trazabilidad se mantiene mediante los siguientes identificadores y archivos:

1. El ID estructural se conserva desde la generacion del modelo hasta el objeto
   visual en Unity.
2. Los nodos y elementos exportados mantienen sus coordenadas y conectividades.
3. Los paños de muro incorporan `source_wall_id` y `floor` para recuperar su
   origen y posicion.
4. Los resultados numericos se guardan en `Edificio/results/` y se consultan
   desde el postprocesador.
5. Las figuras y tablas se generan desde esos resultados, de modo que la
   visualizacion pueda contrastarse con los archivos CSV y JSON.

Este esquema permite seleccionar un objeto en Unity y relacionarlo con el
elemento que aparece en las tablas de demanda, capacidad y respuesta.

## 7. Verificaciones realizadas

Se revisaron las siguientes condiciones:

- existencia y lectura del modelo JSON exportado;
- conteo de nodos, elementos y paños de muro;
- conservacion de la transferencia de cargas;
- comparacion de superposicion contra corrida explicita;
- generacion de curvas P-M para columnas y muros;
- verificacion demanda-capacidad de columnas y muros;
- integracion de seleccion por ID y reinicio de camara en los scripts Unity;
- compatibilidad de los validadores con los identificadores de paños por piso.

Los resultados Python disponibles son consistentes con el estado reportado. La
comprobacion final pendiente es abrir la escena en Unity y confirmar en la
consola del editor que no existan errores de compilacion o referencias faltantes
despues de reimportar los scripts.

## 8. Limitaciones y supuestos

- El modelo estructural es lineal elastico y de caracter academico.
- La losa se representa mediante cargas tributarias, no mediante elementos
  finitos de losa.
- Las curvas P-M dependen de las secciones y armaduras adoptadas en los archivos
  de datos; no constituyen un diseno final.
- La deformada se escala para visualizacion y debe distinguirse del valor
  numerico original.
- La inspeccion visual de Unity depende de que el proyecto se abra con la
  version de Unity indicada para el repositorio.
- La evidencia numerica se considera verificable desde los archivos generados;
  la evidencia visual de la escena debe completarse con una ejecucion del
  proyecto en el editor.

## 9. Conclusiones

La etapa P1A4 integra el modelo estructural y sus resultados con un visor Unity
orientado a inspeccion. La escena dispone de una fuente de datos exportada,
seleccion por ID, inspector, navegacion orbital, representacion de deformada y
acceso a resultados de capacidad para columnas y muros.

La corrida documentada contiene `1650` elementos evaluados en demanda-capacidad,
con `1650` elementos conformes. Ademas, la superposicion reproduce las
respuestas explicitas con errores relativos menores que `4e-7` en las
respuestas comparadas.

El siguiente paso operativo es abrir
`Edificio/visualization/unity/UnityVisualization/` en Unity, cargar
`Assets/Main.unity`, revisar la consola y registrar capturas de la seleccion de
un elemento, la deformada y al menos una curva P-M como evidencia visual del
entregable.
