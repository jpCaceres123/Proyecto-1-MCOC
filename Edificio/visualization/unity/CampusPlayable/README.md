# Integración con el proyecto actual

Se integra el recorrido anterior (terrazas, cafetería, interiores, F5 en tres vistas, láser 1, AK47 2 y terremoto P) con la geometría y los nueve casos de la ampliación de vigas 240 y 341. Las nuevas vigas 680 y 682 se incluyen. El laboratorio 2-7 queda libre. Las tarjetas usan esfuerzos de sección de los diagramas, N positivo en compresión.

La exportación automática está en Edificio/visualization/exports/exportar_inspeccion_campus.py y se llama desde ejecutar.py. Actualiza geometría, barras, muros, losas, cargas y movimientos en las fuentes de CampusPlayable y CampusCardboard. Cada ejecutable/ZIP/APK requiere reconstrucción después; no se modifica por cambiar un archivo analítico.

Entrega actual: ejecutable Windows reconstruido con Unity 6000.5.11f1, sin errores de compilación, y carpeta del escritorio actualizada. No se ejecutaron pruebas de recorrido. Los apartados siguientes conservan el historial; sus advertencias sobre entregas anteriores o licencias no describen esta compilación.

# Campus de Ingeniería — recorrido jugable

## Distribución interior por columnas

Corrección de accesos: las fachadas se generan únicamente en el perímetro exterior, excluyendo los huecos de los núcleos y la junta entre edificios. El pasillo continúa entre LT1 y LT2 sin vidrio ni aletas interiores. La entrada lateral de nivel 2 se centra en Y=8,50 m, alineada con el pasillo, y el acceso de planta baja del LT2 abre en su prolongación.

La distribución actual tiene laboratorios hacia la fachada frontal y salas al lado posterior, intercambiando la versión anterior. En las salas, pizarra y pupitres se orientan hacia el tabique transversal del eje de columnas, con las sillas giradas junto con sus mesas. Se mantiene la cafetería y sus accesos. Estas revisiones son arquitectónicas; el CSV y los resultados analíticos permanecen intactos.

El pasillo longitudinal conecta LT1 y LT2, con laboratorios hacia la fachada frontal y salas de clases al otro lado. Los tabiques y el ancho de cada recinto siguen los ejes X de las columnas de la fila Y=7,25 m del CSV original, según el nivel; las habitaciones se ajustan a las superficies de piso disponibles y no ocupan los núcleos. Las puertas dan al pasillo. La cafetería de nivel 1 y su entrada a la terraza se conservan.

El núcleo delantero grande (hueco aproximadamente X=3,40–6,70 m, Y=2,405–6,95 m) ahora contiene escaleras interiores de dos tramos y descanso intermedio; el núcleo posterior pequeño contiene el único ascensor, con selección de nivel mediante E. El pasillo cruza la junta entre módulos mediante una losa arquitectónica de paso. Donde el muro de unión atraviesa el paso se dibuja un vano visual; conserva el ID del paño y no modifica el CSV ni los resultados analíticos. La asignación de núcleos y la planta son una interpretación arquitectónica de la indicación del usuario.

Esta distribución se compiló para Windows. No se ejecutaron pruebas de recorrido; las comprobaciones históricas de dos ascensores corresponden a la distribución anterior.

## Actualización del 6 de octubre de 2026

La escalera inferior exterior del LT1 se amplió de 2,04 a 4,08 m hacia el edificio, sumando el ancho marcado en rojo y manteniendo el borde exterior, su dirección y la subida de un piso. La losa al pie se amplió hacia el mismo lado a 5,44 m, conservando el acceso al paseo. Es geometría arquitectónica del recorrido; el contrato y resultados estructurales no se modificaron.

F5 alterna entre primera persona, tercera persona desde atrás y tercera persona de frente; la siguiente pulsación vuelve a primera persona. El menú de pausa permite el mismo ciclo. El avatar se mantiene visible en ambas vistas de tercera persona y la cámara conserva su protección contra paredes. Esta revisión se compiló para Windows; no se ejecutaron pruebas de juego.

Proyecto independiente para Unity 6000.5.9f1 (versión actual guardada). Creado el 4 de octubre de 2026.

## Aplicación Cardboard separada

Este proyecto conserva el juego de Windows y no activa VR con F7. La aplicación móvil está en `../CampusCardboard`, tiene configuración independiente y se abre como otro proyecto en Unity Hub. Allí se consulta su README y se construye la APK.

## Jugar en Windows

Abrir `Build/Windows/CampusIngenieria.exe`, o extraer el ZIP Windows y abrir el ejecutable en su carpeta. Mantener al lado del EXE `CampusIngenieria_Data`, `MonoBleedingEdge` y `UnityPlayer.dll`. Presionar **Entrar / continuar** en la pantalla inicial.

- WASD: caminar; mouse: mirar.
- Shift: correr; Espacio: saltar.
- E: abrir/cerrar puertas, elegir piso en el ascensor, leer placas e inspeccionar miembros estructurales próximos.
- F: linterna; Esc: pausa y sensibilidad del mouse; R: volver al acceso.
- F5: alternar primera/tercera persona; el visitante usa el mismo AmongUs.fbx de la carga móvil.
- F6: alternar arquitectura/modo estructural, también disponible desde Esc. En modo estructural se muestran únicamente losas, columnas y muros; se ocultan vigas, fachadas, tabiques, mobiliario y paisaje. Los apoyos de recorrido se mantienen invisibles para conservar el desplazamiento.

La revisión de código del 5 de octubre reemplaza el ascensor exterior LT2 por dos interiores en huecos existentes de las losas. E abre la selección de piso del ascensor próximo y conserva ese mismo ascensor al llegar. Es una transición de piso, no una simulación dinámica de una cabina. La escalera exterior del LT1 sigue tres tramos por la fachada, unidos por dos descansos abiertos; el tramo desde el patio sube desde la izquierda al descanso inferior, sin prolongarse hasta el extremo derecho. Las placas de cada piso completan la ruta de cinco espacios.

## Revisión local del 5 de octubre: estado de entrega

Los cambios F5/F6 y ascensores están en el código fuente, **no en los ZIP ni ejecutables entregados previamente**. La compilación C# contra las referencias de Unity 6000.5.9f1 pasó; únicamente quedó la advertencia preexistente de `CampusLaser.details` sin uso. Unity rechazó generar el Player por falta de licencia válida (salida 198). Por tanto no se afirma prueba de juego, precisión de cámara ni accesibilidad final de los ascensores.

La implantación usa la intersección de los huecos exportados que contienen los puntos OpenSees (X,Y)=(5,4) y (5,11): primer hueco X=3.40–6.70, Y=2.405–6.95 m; segundo X=3.60–6.30, Y=10.15–12.15 m. Las medidas comunes evitan invadir las losas entre niveles. Las paradas se derivan de los niveles con huecos reales: 3.96, 7.92, 11.88 y 15.84 m (más el acabado de recorrido). No se habilita el piso enterrado ni se perfora la cubierta de 19.80 m para inventar otra parada. La asignación de estos dos huecos a ascensores responde a la petición del usuario; debe contrastarse con los planos si existe otra designación. No se crean huecos nuevos ni se modifican cálculos, IDs o resultados OpenSees.

Las losas visuales del modo estructural siguen las superficies y vacíos del CSV. Su espesor representado es 0.15 m de referencia, no una verificación de espesor por paño ni una placa FE. Los pisos de circulación conservan el acabado anterior a +0.45 m: no confundir esos apoyos de recorrido con la cota analítica. La arquitectura puede restaurarse sin perder las colisiones originales. La cámara de tercera persona limita su distancia frente a obstáculos y oculta el avatar si queda demasiado cerca. El AK47 en primera persona se oculta en tercera persona; el láser de consulta se conserva.

Tras activar la licencia y generar el nuevo build con `Campus > Construir Windows`, ejecutar `CampusIngenieria.exe -campus-feature-check -campus-output <carpeta>` para comprobar estado reversible de las capas, ocho puntos de apoyo en los ascensores y disponibilidad del FBX, y producir dos capturas. La prueba está añadida, pero **no se ha ejecutado**. Completar además pruebas manuales de F5/F6, recorrido y salida/entrada de ambas cabinas antes de sustituir los ZIP de entrega.

## Abrir el proyecto editable

En Unity Hub, agregar esta carpeta como proyecto y elegir Unity **6000.5.11f1**. Abrir `Assets/Campus.unity`, entrar a Play y pulsar Entrar. El menú **Campus > Construir Windows** reconstruye el ejecutable. No necesita paquetes externos ni assets comerciales.

La escena se genera desde `CampusWorld.cs`; el movimiento, menú e interacción están en `CampusPlayer.cs`. El constructor está en `Assets/Editor/CampusBuilder.cs`.

## Relación con la estructura principal

`Assets/Resources/estructura_principal.csv` es una copia exacta del contrato `UnityVisualization/Assets/Resources/model_3d.csv` del proyecto de trabajo. Contiene 1256 nodos, 619 barras, 82 paños de muro y 656 losas. Se conservan identificadores, extremos de barras y coordenadas, con la transformación OpenSees (X,Y,Z) a Unity (X,Z,Y). Las plantas y sus vacíos se obtienen de las losas originales. Los niveles están separados por 3,96 m.

La fachada naranja, el vidrio, las particiones, mobiliario, escaleras exteriores, ascensor, terraza y entorno son una interpretación arquitectónica de las fotografías; el interior es inventado. Se introduce un acabado de piso 0,45 m sobre el plano analítico para cubrir las vigas. Estas piezas no añaden ni modifican acciones, apoyos o rigidez en OpenSees. Los miembros permanecen en sus coordenadas originales. Las fotos de referencia se conservan en `References/`.

## Corrección del LT1

Se distinguen dos volúmenes acristalados: inferior X=9,70–17,79 m entre cotas 7,92 y 11,88 m; superior X=19,70–30,30 m entre cotas 15,84 y 19,80 m. Los descansos de la escalera son las losas X=19,70–30,30 m a cota 11,88 m y X=9,70–12,50 m a cota 15,84 m. Permanecen abiertos con antepechos naranjas. Las coordenadas provienen de geometria_manual.json y Cargas_losas.txt. Los anchos de paso, acceso al terreno y acabados son una interpretación de las fotografías y de la anotación del usuario.

La compilación Windows de esta revisión se registra en build-unity.log. No se ha realizado una nueva prueba de juego. Validation/04_LT1_corregido.png muestra esta revisión; las capturas y comprobaciones anteriores corresponden al diseño previo.

Corrección del tramo de acceso: arranca en el patio (X=16 m, Y=-5 m) y llega al descanso inferior (X=39,20 m, Y=-1,50 m), siguiendo la segunda anotación morada.

## Losas de acceso y terraza posterior

Se agregó una losa horizontal al pie de la escalera del LT1, conectada al paseo, y se amplió su llegada lateral. La cara opuesta incorpora un podio con terraza saliente, antepechos claros, zócalo de ladrillo, equipos de ventilación y escalera lateral de acceso. Dimensiones y equipamiento son una interpretación de las dos fotografías nuevas, sin modificar la geometría analítica. La captura 05_terraza_posterior.png muestra esta cara.

## Escalera ancha y terreno en pendiente

El tramo inferior nace del borde de la losa lateral y ocupa sus 7,50 m de ancho completo. Desciende hacia el patio con una llegada del mismo ancho. El costado exterior de la losa conecta a nivel con un terreno elevado y un sendero que descienden hacia la cancha. La topografía es aproximada a partir de la fotografía marcada en morado. No se ejecutaron pruebas nuevas de recorrido.

## Entrada lateral y acceso a terraza

Se completó el terreno junto al costado del LT1 hasta la cota de entrada, cubriendo el vacío señalado. El acceso lateral incorpora puerta interactiva y marquesina. La terraza posterior se conecta al terreno alto mediante una escalera de 4 m de ancho, con descansos y apertura del antepecho. La franja de equipos deja libre esta circulación.

## Terraza de cafetería

La escalera al terreno alto tiene dos tramos y una losa horizontal intermedia. Los equipos de la terraza se reemplazaron por nueve mesas y 36 sillas. La cafetería interior está junto a la terraza, con acceso acristalado interactivo aproximadamente en X=34,5 m, barra, máquina de café y mesas. El vano se representa únicamente en el muro visual del recorrido; no se cambia el contrato estructural CSV ni el cálculo analítico.

## Corrección final del tramo inferior LT1

En la cara de los voladizos, el tramo inferior baja únicamente 3,96 m: desde la cota de acabado 8,37 m hasta una nueva losa a 4,41 m. Su ancho es 2,04 m y queda alineado con el tramo superior (Y horizontal=-1,50 m). Se retiró la extensión ancha anterior. El nivel inferior del LT1 queda cubierto por relleno y terreno hasta esta cota, con talud hacia el paseo. Esta modificación afecta solo al recorrido arquitectónico; se conserva el CSV analítico.

## Escalera exterior corregida e inspector láser

El tramo de un piso se reubicó al lado exterior de la losa (Y=-6,35 m), correspondiente a la zona roja de la última referencia. La losa inferior y su conexión acompañan esta posición.

El láser se activa al entrar al recorrido. L: activar/desactivar; Q: cambiar caso de carga; clic derecho: fijar/liberar elemento; Tab: mostrar todas las estaciones de los diagramas; rueda: desplazar la ficha. El escáner busca estructura detrás de los acabados hasta 45 m. El punto rojo identifica el objetivo y el panel muestra sus datos.

La base Resources/inspeccion_estructural.json contiene 1357 elementos originales: 619 barras, 82 paños de muro y 656 losas. Se importaron fuerzas locales, diagramas, desplazamientos nodales, reacciones disponibles y cargas tributarias de los archivos existentes en Edificio/results. Casos: G, Q, EX, EY, R, EXG, EXQ, EYG, EYQ. Las fuerzas están en kN y momentos en kNm; desplazamientos en m y giros en rad. Se distinguen los esfuerzos locales de las reacciones globales. En losas se muestran carga, reparto y bordes; no existen esfuerzos de placa exportados. Los datos ausentes se identifican como no disponibles. Es una consulta de resultados guardados, no un cálculo en vivo. Las piezas arquitectónicas añadidas no tienen demandas analíticas.

Esta revisión se compiló, sin nuevas pruebas de juego.

## Equipo del personaje

1 (o teclado numérico 1): equipar láser de inspección. 2 (o teclado numérico 2): equipar AK-47 representado con geometría procedural en primera persona. Mantener clic izquierdo dispara en automático; T recarga el cargador de 30 cartuchos. Incluye sonido sintetizado, retroceso, destello, trazas y marcas temporales de impacto. Cambiar al rifle oculta el láser y su panel. El disparo se detiene en pausa o al abrir el ascensor. R conserva la función de volver al acceso. Esta revisión se compiló; no se realizaron nuevas pruebas de juego.

## Panel de inspección rediseñado

El inspector usa tarjetas con valores y unidades, encabezado por elemento y casos de carga, y cuatro secciones: Resumen, Cargas, Diagramas y Movimiento. Tab cambia de sección; Q cambia de caso; I alterna los extremos i/j; clic derecho fija el elemento; rueda desplaza la ficha. Los diagramas usan todas las estaciones guardadas y muestran mínimos y máximos. Los desplazamientos se presentan en mm y los giros en rad. El diseño reemplaza el texto JSON y las tablas crudas del panel anterior. Se conservan resultados de cálculo y controles 1/2 del personaje. Compilación realizada; sin nuevas pruebas de juego.


El antiguo laboratorio 2-7 se deja libre: no se generan su cerramiento, puerta, mobiliario ni letrero. Los identificadores de los demás recintos se conservan.

## Terremoto con P
Durante el recorrido, P inicia un terremoto visual de 25 segundos; otra pulsación lo detiene con una transición suave. Incluye balanceo gradual de las superficies, vibración de cámara en las tres vistas, retumbo y contador. El menú y el ascensor suspenden el efecto. Las mallas de colisión, el CSV y los esfuerzos guardados del láser no cambian; no es un análisis sísmico ni se calcula daño estructural.

Corrección: se aplica explícitamente la exclusión del recinto 2-7 antes de generar cualquier objeto. El terremoto ahora muestra un desplazamiento lateral exagerado de hasta aproximadamente 1,65 m en las plantas superiores, con base fija y terreno/vegetación inmóviles. La vibración de cámara es menor para apreciar el movimiento relativo del edificio. Es una animación visual, sin modificación de los resultados analíticos.


