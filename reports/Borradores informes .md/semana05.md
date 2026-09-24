# Semana 5 — Laboratorio estructural interactivo v1

Fecha de comprobación: 14 de septiembre de 2026.

> **Revisión de código 23-09-2026.** Esta carpeta no contiene metadatos Git, así que no fue posible verificar el commit indicado en el prompt ni separar cambios previos del usuario. En esta revisión los scripts `verification/interactive/verificar_semana05.py` y `verificar_modificaciones.py` no estaban presentes; ahora el primero se ejecutó y registró 120 errores por componente (traslaciones, rotaciones, acciones, diagramas y P–M, para tres combinaciones y dos formulaciones α), todos bajo tolerancia relativa 1e−5. El segundo se ejecutó en una copia temporal completa del checkout; carga y sección se analizaron y la restauración devolvió respuesta y hashes de Resources idénticos. En el checkout original se regeneraron las 312 bases SQ4: 652 paneles y 10 686 controles aprobados; luego pasaron las 29 pruebas unitarias. El hash que antes era incompatible ahora coincide.
>
> Se incorporaron sliders λ inmediatos, invalidación P–M por valores de demanda/capacidad, resolución sísmica con α, modificación real de sección por ID y bloqueo de SQ4 si el hash del modelo no coincide. La incompatibilidad inicial de SQ4 quedó corregida al regenerar las bases y exportar el hash vigente. Se ejecutó `verificar_modificaciones.py` en copia temporal: la viga 207 cambió de 0.80×0.60 m a 0.75×0.45 m, varió Vzi de 348.879910 a 340.259958 kN y Myi de −745.476764 a −694.973953 kN·m; la restauración fue exacta. Se intentó abrir una copia temporal del proyecto con Unity 6000.5.9f1; el editor solo emitió el inicio del registro/licencia y no produjo una compilación verificable. El recorrido visual y la captura siguen pendientes. El estado actualizado y los comandos están en [SEMANA5_VARIANTES.md](../../Edificio/documentation/SEMANA5_VARIANTES.md). Los resultados narrados más abajo corresponden a evidencias históricas ya presentes en el borrador y no sustituyen esas comprobaciones actuales.


**Actualización de interfaz:** posteriormente se rediseñaron los controles móviles con paneles que se pueden cerrar, navegación inferior, escala de pantalla, área segura y pestañas del inspector. También se corrigió la restauración de visibilidad al volver a todos los niveles. La evaluación de interfaz original de este informe corresponde al estado previo; consultar [interfaz_celular.md](../../Edificio/documentation/interfaz_celular.md) para el estado actualizado y sus límites de comprobación.

Proyecto utilizado: `C:\Users\nico0\OneDrive\Desktop\Proyecto-1-MCOC-main`.
Modelo y análisis: `Edificio/`. Visor: `Edificio/visualization/unity/UnityVisualization`, escena `Assets/Main.unity`.

El laboratorio permite explorar resultados elásticos y capacidad de secciones. Se verificaron tres combinaciones mediante soluciones OpenSees independientes y dos cambios de datos mediante el proceso completo de cálculo y exportación. La preparación móvil apunta al **iPhone 15 con iOS 26**, confirmado por el usuario. La creación del build iOS quedó bloqueada por falta del módulo iOS Build Support; no hay aplicación instalada ni proyecto Xcode generado.

## 1. Funciones implementadas

“Implementada” describe la función existente en código; no equivale a una prueba de uso en el teléfono. Esta revisión comprende código, cálculo y compilación C# en escritorio. No se realizó una sesión visual ni una medición de rendimiento en iPhone.

Las rutas de scripts C# de esta tabla son relativas a `Edificio/visualization/unity/UnityVisualization/Assets/Scripts/`.

| Función | Estado | Implementación y alcance | Limitación identificada |
|---|---|---|---|
| Navegación | Escritorio implementado; gestos móviles iniciales | `OrbitCamera.cs`: órbita, desplazamiento y zoom con ratón. Se agregaron órbita con un dedo y zoom/desplazamiento con dos. | Gestos todavía sin prueba física; paneles de tamaño fijo. |
| Selección | Implementada | `ElementInspector.cs`: raycast, resaltado, filtros por tipo y búsqueda/lista. El arrastre cancela la selección. | Oclusión de elementos interiores; se inhibió la selección durante gestos de dos dedos. |
| Apoyos | Visualización implementada | `BuildingVisualizer.cs` lee restricciones del modelo y crea símbolos. El modelo OpenSees aplica las condiciones de borde. | Símbolo visual no sustituye una tabla de los seis grados de libertad. |
| Ejes | Implementados | `StructuralPostprocessor.cs` y `BuildingVisualizer.cs` muestran ejes locales exportados. | Convención OpenSees XYZ; representación Unity XZY. Interpretar esfuerzos según ejes locales. |
| Cargas | Implementadas para los casos exportados | `Semana3Visualizer.cs`: G, Q, EX, EY, R; fuerzas de piso y centros de masa. `ElementInspector.cs`: pesos y receptores de losas. | No es un editor libre de cargas nodales en tiempo real. |
| Áreas tributarias | Parcial | Cálculo y conservación numérica; inspector con receptores exportados. | El dibujo `UpdateTributary()` usa rectángulos/triángulos aproximados y criterio de relación de lados; no reproduce todos los recortes y vacíos del reparto numérico. |
| Deformada | Implementada | Traslaciones y rotaciones exportadas; interpolación de barras y bordes de shells en `StructuralPostprocessor.cs`; escala configurable. | La amplificación visual no es desplazamiento físico; no es un análisis no lineal del edificio. |
| Diagramas | Implementados para barras | Inspector de esfuerzos de extremo y diagramas N, Vy, Vz, My, Mz. | Reconstrucción acorde al modelo con cargas nodales: no agrega cargas distribuidas que no estén modeladas. |
| Superposición | Implementada y comprobada numéricamente | Campos λ en R; cambios de ponderadores de masa con bases EXG/EXQ/EYG/EYQ. | Válida para la rigidez y condiciones de borde del modelo lineal exportado. Cambiar geometría exige recalcular. |
| P–M | Implementada para secciones de columna y muro, con demanda del muro seleccionado | `SectionGraphs.cs`, `WallSectionGraphs.cs`; capacidad en `analysis/capacity/` y resultante del corte Shell en `analysis/load_cases/casos.py`. | La comparación es uniaxial y nominal; no incluye factores φ, esbeltez ni interacción biaxial. |
| Modificación del modelo | Flujo manual reproducible | JSON → Python/OpenSees → resultados → Resources → reinicio de Play/rebuild. Dos variantes ejecutadas en esta entrega. | Unity no ejecuta OpenSees al editar geometría; cambiar λ combina respuestas existentes. |

La malla Shell inserta ahora estaciones exactamente en cada cruce, encuentro y
nodo de marco que cae sobre el eje de un muro. Los paños que comienzan sobre un
piso se apoyan mediante esos nodos compartidos. Se eliminó el amarre de los seis
GDL de toda la base a un solo extremo, porque generaba pares axiales artificiales
en muros de transferencia. En ejes arquitectónicos desplazados sólo se conserva
el vínculo vertical al nodo de marco cercano; la cinemática horizontal proviene
del diafragma rígido.

Para el muro origen 10 (`M-3-3'-E/F`) se adoptaron **10Ø22 adicionales en
cada extremo**, además de la doble malla Ø8@20. Es un supuesto académico porque
el antecedente disponible señala los refuerzos de borde como “por confirmar”.
Con la demanda corregida del paño 1002, la utilización nominal es 0,771 para EX
y 0,621 para R; estos valores no incluyen factor φ ni comprobación de detalle o
confinamiento del elemento de borde.

Para el muro origen 14 (`M-1''-Eb/Ec`) se adoptaron **3Ø22 adicionales en
cada extremo**, además de su doble malla Ø10@20. El paño crítico 1403 alcanza
utilizaciones nominales de 0,840 en EX, 0,800 en EY y 0,439 en R. La armadura
de borde también queda identificada como supuesto académico pendiente de plano.

Para los retornos cortos de los muros origen 2 (`M-LT2-1-A'`) y 4
(`M-LT2-3-A'`) se adoptaron **20Ø22 en cada extremo** en todos sus pisos. La
cantidad es elevada y debe verificarse por congestión, confinamiento y posible
tratamiento como sección compuesta con los muros perpendiculares. Dentro del
modelo rectangular solicitado, todos sus paños quedan dentro para EX, EY y R;
la utilización máxima es 0,823 para el muro 2 y 0,827 para el muro 4.

## 2. Dos categorías de modificación verificadas

### 2.1 Intensidad de carga viva

Entrada base: `q_Q_kN_m2 = null`. Variante: `q_Q_kN_m2 = 4.0 kN/m²`. Se conserva geometría y rigidez, y se regeneran los casos con OpenSees.

| Respuesta R | Base | Q = 4 kN/m² |
|---|---:|---:|
| Máximo |uz| [m], nodo 900116 | 0.0336096580 | 0.0328676641 |
| Vzi, elemento 207 [kN] | 348.879910 | 336.593528 |
| Myi, elemento 207 [kN·m] | −745.476764 | −711.709899 |

Entrada: [mod_Q_parametros.json](../../Edificio/documentation/semana05_evidencias/mod_Q_parametros.json).

### 2.2 Cambio de sección de viga

El dato fuente de geometría selecciona la viga analítica ID 207 (`BEAM_X`, nodos 900000–601002): sección heredada 0.80×0.60 m; variante rectangular 0.75×0.45 m. La cadena recalcula A=0.3375 m², Iy=0.0158203 m⁴, Iz=0.00569531 m⁴ y J=0.0142629 m⁴, usa el override en el `elasticBeamColumn`, actualiza peso propio/masa y exporta dimensiones para la geometría Unity. Las salidas de `R` de la misma viga cambian a Vzi=340.259958 kN y Myi=−694.973953 kN·m. El máximo |uz| del edificio cambia de 0.0336096580 a 0.0336097043 m.

Entrada: [mod_seccion_viga_207_geometria.json](../../Edificio/documentation/semana05_evidencias/mod_seccion_viga_207_geometria.json). El flujo se verificó en una copia temporal del checkout; no se presentó capacidad P–M de viga porque esta variante no genera esa capacidad.

### 2.3 Ejecución y restauración

Desde la raíz del proyecto, con `Edificio/requirements.txt` instalado:

```powershell
python Edificio/verification/interactive/verificar_modificaciones.py
```

El script crea las entradas de variante, regenera la geometría y resuelve las bases/casos, recoge desplazamientos y acciones de viga en `R`, vuelve a generar el modelo base y comprueba la respuesta base más el SHA-256 de los 667 archivos de Resources, incluidos los nodos binarios SQ4. El registro de esta ejecución es [modificaciones_ejecucion.json](../../Edificio/documentation/semana05_evidencias/modificaciones_ejecucion.json); `restored_exactly` es true.

Los resultados de variante se escribieron en una copia temporal para no sustituir archivos generados locales. Para ver una variante en Unity, correr el script en una copia de trabajo, abrir `Assets/Main.unity`, y regenerar SQ4 con `python Edificio/analysis/load_cases/carga_movil.py` antes de usarlo con el nuevo hash. El estado base queda restaurado con el mismo comando de verificación.

## 3. Superposición interactiva: tres estados

Mantener los ponderadores de masa **αG = 1 y αQ = 0.5**. Seleccionar R, introducir los cuatro λ en orden G, Q, EX, EY y pulsar **Aplicar λ**.

| Estado | λG | λQ | λEX | λEY |
|---|---:|---:|---:|---:|
| S1 | 1 | 1 | 0 | 0 |
| S2 | 1.2 | 1.4 | 0.8 | −0.3 |
| S3 | 1 | 0.5 | −1 | 0.7 |

Comprobador: `Edificio/verification/interactive/verificar_semana05.py`. Lee los CSV usados por Unity, reconstruye EX/EY desde EXG/EXQ/EYG/EYQ con los ponderadores de masa y después combina los casos. Usa precisión float32 para traslaciones como `Vector3` y double para esfuerzos locales. Las reacciones se combinan desde los NPZ G/Q/EX/EY. Para cada estado ejecuta una solución **nueva** `casos.solve()` con la carga equivalente. Las reacciones se verifican numéricamente aunque no exista una tabla equivalente para todos los apoyos en la interfaz.

El intercambio XYZ → XZY es una permutación de componentes para dibujar; los valores y errores de esta tabla se expresan en **coordenadas OpenSees**. No se comparan píxeles de pantalla. La prueba cubre traslaciones nodales, reacciones y las doce acciones locales de barras; no constituye una validación de todas las curvas de dibujo, rotaciones o fuerzas de shells.

Error relativo utilizado: `max(abs(superpuesta − explícita)) / max(abs(explícita))` sobre cada conjunto de respuestas. Tolerancia: **1e−5**. Para fuerzas/reacciones se consideran conjuntamente componentes de fuerza y momento, además de conservar los valores por componente en la evidencia; no interpretar ese máximo como una única magnitud física.

| Estado | Error relativo traslaciones | Error relativo reacciones | Error relativo fuerzas locales | Resultado |
|---|---:|---:|---:|---|
| S1 | 3.583e−8 | 3.113e−8 | 3.952e−8 | Cumple |
| S2 | 9.824e−8 | 1.282e−7 | 3.142e−7 | Cumple |
| S3 | 3.117e−7 | 3.279e−7 | 9.983e−7 | Cumple |

Ejemplos directamente contrastables con los resultados:

| Estado | Respuesta | Superposición | OpenSees explícito |
|---|---|---:|---:|
| S1 | Nodo 900116, uz [m] | −0.027240596712 | −0.027240596223 |
| S2 | Nodo 900116, uz [m] | −0.034979511052 | −0.034979511690 |
| S3 | Nodo 900116, uz [m] | −0.022422445938 | −0.022422446950 |
| S1 | Barra 15, Ni [kN] | 5875.709110 | 5875.709108 |
| S2 | Barra 15, Ni [kN] | 7378.855645 | 7378.855638 |
| S3 | Barra 15, Ni [kN] | 5049.985072 | 5049.985057 |

Los signos de Ni corresponden a `localForce`; no se cambian para forzar una convención gráfica. La capacidad P–M usa su propia convención explícita de compresión positiva.

Reproducir con:

```powershell
python Edificio/analysis/load_cases/ejecutar.py
python Edificio/verification/interactive/verificar_semana05.py
```

Evidencia completa, incluidas reacciones, identificadores y errores absolutos: [superposicion.csv](../../Edificio/documentation/semana05_evidencias/superposicion.csv) y [superposicion.json](../../Edificio/documentation/semana05_evidencias/superposicion.json). El caso S2 coincide con la combinación base; S1 y S3 amplían la verificación a otro reparto y cambio de sentido sísmico.

## 4. Sidequest: carga móvil

**Actualización 23-09-2026:** vista en primera persona y personaje FBX
Among Us; los 167 paneles con cuatro bordes definidos ahora transfieren P a
cuatro vigas. Ver [reporte de esta actualización](../2026-09-23_primera_persona_cuatro_vigas.md).

**Actualización 22-09-2026:** la restricción inicial queda eliminada: ahora se
puede seleccionar cualquiera de los 652 paneles, con transferencia de fuerza
y momento para apoyos excéntricos y voladizos. Incluye selección por ID/planta/clic,
28 pruebas y 10.680 controles aprobados. Ver
[reporte actualizado](../2026-09-21_carga_movil_todas_losas.md). El párrafo siguiente
describe el alcance histórico de la primera implementación.

**Implementada en la actualización del 21-09-2026 para escritorio.** Incluye regla física explícita, panel de control, reparto a dos vigas, conservación de fuerza y momento de la transferencia, y respuesta estructural incremental con diagramas y deformada. El recorrido está limitado a 20 paneles completos de cinco niveles, sin atravesar vacíos.

La carga localizada P se reparte como `P(1−η)` y `Pη` sobre dos bordes opuestos. Sus efectos se reconstruyen mediante bases de OpenSees del edificio completo y se contrastan con soluciones explícitas fuera de las posiciones base. No se confunde el movimiento de cámara con movimiento de carga. El modo muestra sólo ΔSQ4, sin sumar G/Q/sismo, y no representa efectos dinámicos.

La actualización tiene 148 controles SQ4 aprobados, 25 pruebas unitarias aprobadas y comprobación de ejecución/capturas del visor Windows. La limitación heredada de momento global por vínculos equalDOF no coincidentes se mantiene documentada; no se afirma validación estructural integral del edificio ni validación iPhone.

Ver [reporte de entrega y capturas](../2026-09-21_vigas_y_carga_movil.md) y [regla física, controles y uso](../../Edificio/documentation/CARGA_MOVIL.md). Los demás apartados de este informe conservan la evaluación histórica original salvo indicación expresa.

## 5. UX estructural

Evaluación por inspección del código y trazabilidad de los datos. Pendiente prueba de uso con un usuario y con el iPhone.

| Pregunta | Cómo responde hoy el viewer | Evaluación / mejora necesaria |
|---|---|---|
| ¿Dónde está el elemento? | Selección directa, ID, filtro por tipo, resaltado y órbita. | Útil en escritorio. Falta centrar automáticamente la cámara en el seleccionado. El control de nivel actúa como cota mínima; no es aislamiento de un piso. |
| ¿Cómo está apoyado? | Capa de apoyos derivada de restricciones exportadas. | Parcial: conviene mostrar los seis DOF y explicar apoyos a diferentes cotas. |
| ¿Qué lo carga? | Inspector de losa con G/Q y receptores, flechas sísmicas y centros de masa. | Permite seguir parte del camino de carga. El dibujo tributario aproximado no debe utilizarse para medir reparto exacto. |
| ¿Cómo se deforma? | Geometría original y deformada amplificada, valor numérico y escala. | Útil si se consulta el valor real. Mantener visible la escala; una línea muy desplazada no equivale a una flecha física de ese tamaño. |
| ¿Qué fuerzas tiene? | Acciones locales y diagramas de barras; para muros muestra P, M principal, cortes en/fuera del plano y torsión vertical en la base de cada paño. | Las resultantes de muro se reducen desde las fuerzas nodales resistentes ShellMITC4 al centro del corte inferior. |
| ¿Cuánta capacidad tiene? | Curvas P–M y momento–curvatura; el P–M de muro superpone en rojo la demanda del caso seleccionado e informa dentro/fuera y `|M|/Mcap(P)`. | Es una comparación nominal uniaxial. Revisar asignación de armadura y eje principal antes de usarla para diseño. |

Incidencias concretas pendientes:

- `BuildingVisualizer.ApplyVisibility()` retorna antes de restaurar objetos cuando el nivel vuelve a −1: algunos elementos ocultos pueden seguir ocultos hasta reiniciar. No se declara este control como validado.
- Los paneles de 265 y 388 píxeles y sus áreas de bloqueo son fijos; faltan adaptación a área segura del iPhone, tamaño táctil y lectura cómoda de gráficos.
- `UpdateTributary()` no es la fuente de los valores numéricos de reparto: consultar el inspector exportado y las verificaciones.
- La trazabilidad axial muestra `P del tramo − suma de P superiores` como aporte neto. Esa resta ayuda a leer resultados, pero **no es una prueba independiente del equilibrio del nudo**. Para esa prueba se deben sumar cargas y acciones de todos los elementos concurrentes con signos y ejes consistentes.

## 6. Preparación móvil: iPhone 15 / iOS 26

El usuario confirmó este dispositivo. El iPhone 15 figura entre los modelos compatibles con iOS 26 en la [lista oficial de Apple](https://support.apple.com/en-nz/guide/iphone/iphe3fa5df43/ios). La compatibilidad del sistema operativo no demuestra todavía rendimiento ni legibilidad del visor.

### Código preparado

- `Assets/Editor/BuildMobile.cs`: menú **Build → Edificio Viewer → Exportar iOS (iPhone 15)** y método batch `BuildMobile.BuildIOS`.
- Escena `Assets/Main.unity`, identificador `cl.mcoc.edificio.viewer`, backend IL2CPP, iPhone, orientación horizontal y deployment mínimo iOS 16.0. El dispositivo de prueba ejecuta iOS 26; el mínimo es un umbral de instalación, no la versión del SDK.
- `Assets/Scripts/OrbitCamera.cs`: un dedo orbita; dos dedos hacen zoom y desplazan. No inicia cámara desde los paneles laterales.
- `Assets/Scripts/ElementInspector.cs`: cancela selección durante dos toques para evitar seleccionar durante el zoom.
- Encabezado del visor actualizado a “LABORATORIO · SEMANA 5”.

### Intento realizado y bloqueo

Se ejecutó Unity en batch con `-executeMethod BuildMobile.BuildIOS`. El editor compiló los scripts C# y llegó a la comprobación de plataforma. Terminó con:

```text
BuildFailedException: Falta iOS Build Support en este editor.
No se creó proyecto Xcode ni IPA.
```

No se encontró `unity_ios.log` en el checkout revisado; consultar el estado del build en `Edificio/visualization/unity/UnityVisualization/Build/iOS/estado.txt`. No hay un archivo de estado de build iOS en el checkout revisado.

El proyecto declara Unity **6000.5.10f1**. Para comprobar compilación se utilizó el editor disponible **6000.5.9f1**; se restauró la declaración original de versión después del intento. La comprobación no sustituye la compilación con la versión declarada y módulo iOS en un Mac.

**Estado del entregable móvil: preparación realizada; build inicial pendiente por entorno.** No se generó una IPA ni se probó en el teléfono. Unity produce primero un proyecto Xcode, y Xcode compila la aplicación; la compilación local requiere macOS, según el [proceso oficial de Unity](https://docs.unity3d.com/6000.0/Documentation/Manual/iphone-BuildProcess.html).

### Flujo para completar el build

1. En un Mac, instalar Unity 6000.5.10f1 con iOS Build Support y Xcode compatible con el iOS del teléfono.
2. Copiar/abrir este proyecto Unity con sus Assets, Packages y ProjectSettings. Regenerar Resources antes si cambió el modelo.
3. Usar el menú de exportación indicado. Se generará `Build/iOS/` con el proyecto Xcode; comprobar `estado.txt` y el log.
4. Abrir el proyecto Xcode, elegir el equipo de firma disponible y el iPhone 15 conectado. Compilar e instalar. No hay credenciales ni equipo de firma inventados en el script.
5. Probar órbita, zoom, selección, scroll, teclado de λ, los tres estados, gráficos y recuperación al pausar. Verificar área segura y legibilidad. Registrar tiempos de carga y FPS; todavía no se midieron.

## 7. Verificaciones ejecutadas y archivos de entrega

| Comprobación | Resultado observado |
|---|---|
| `python Edificio/analysis/load_cases/ejecutar.py` | Estado global OK; regeneración de resultados y recursos. |
| `python Edificio/verification/interactive/verificar_modificaciones.py` | Dos variantes resueltas; razón EX 1.249999665; restauración de resultados/hashes idéntica. |
| `python Edificio/verification/interactive/verificar_semana05.py` | 9 comparaciones dentro de tolerancia, tres estados. |
| `python Edificio/verification/load_transfer/verificar_reparto_combinacion.py` | 652 losas conservan G/Q, sin receptores duplicados; R reproduce archivos exportados. |
| `python -m unittest discover -s Edificio/verification/tests -p "test_*.py"` | 18 pruebas, todas OK. |
| Unity batch `BuildMobile.BuildIOS` | Scripts compilados; exportación detenida por falta de iOS Build Support. |

La conservación por piso de Q se registra en `Edificio/results/conservacion_Q.csv`: error absoluto máximo aproximado **4.855e−5 kN**, inferior a 0.002 kN. Las verificaciones anteriores no corrigen por sí mismas una idealización estructural incorrecta ni sustituyen revisar conectividad y datos de entrada.

Archivos nuevos principales:

- Este informe `reports/semana05.md`.
- `Edificio/verification/interactive/verificar_semana05.py`.
- `Edificio/verification/interactive/verificar_modificaciones.py`.
- `Assets/Editor/BuildMobile.cs` dentro del proyecto Unity.
- Entradas de variantes, resultados de contraste, hashes y logs en `Edificio/documentation/semana05_evidencias/`.

Al cerrar esta entrega, los parámetros y resultados numéricos activos corresponden al estado base, no a las variantes de ensayo.
