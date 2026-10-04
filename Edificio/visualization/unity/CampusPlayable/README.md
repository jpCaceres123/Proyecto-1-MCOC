# Campus de Ingeniería — recorrido jugable

Proyecto independiente para Unity 6000.5.11f1. Creado el 4 de octubre de 2026.

## Jugar en Windows

Abrir `Build/Windows/CampusIngenieria.exe`, o extraer el ZIP Windows y abrir el ejecutable en su carpeta. Mantener al lado del EXE `CampusIngenieria_Data`, `MonoBleedingEdge` y `UnityPlayer.dll`. Presionar **Entrar / continuar** en la pantalla inicial.

- WASD: caminar; mouse: mirar.
- Shift: correr; Espacio: saltar.
- E: abrir/cerrar puertas, elegir piso en el ascensor, leer placas e inspeccionar miembros estructurales próximos.
- F: linterna; Esc: pausa y sensibilidad del mouse; R: volver al acceso.

El ascensor del extremo LT2 permite llegar a todos los niveles. La escalera exterior del LT1 sigue tres tramos por la fachada, unidos por dos descansos abiertos; el tramo desde el patio sube desde la izquierda al descanso inferior, sin prolongarse hasta el extremo derecho. Las placas de cada piso completan la ruta de cinco espacios.

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
