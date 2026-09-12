# Visualizador Unity

1. Abrir esta carpeta como proyecto Unity `6000.5.10f1`.
2. Abrir `Assets/Main.unity`.
3. Ejecutar la escena. El panel permite activar/desactivar nodos, vigas, columnas, muros, apoyos, diafragmas, IDs, ejes locales y el inspector de area tributaria.

Controles: arrastrar con LMB/RMB para orbitar, MMB para desplazar y rueda para zoom. La seleccion de losa en el panel muestra una primera aproximacion del reparto tributario hacia sus cuatro bordes.

## Crear el EXE

1. Abre este proyecto en Unity.
2. En el menu selecciona `Build > Edificio Viewer > Construir EXE`.
3. El ejecutable quedara en `Build/EdificioViewer.exe` junto con sus carpetas de datos. Distribuye la carpeta `Build` completa, no solo el `.exe`.

La herramienta de build reutiliza la cache incremental de Unity. Si el proyecto
se encuentra dentro de OneDrive, la primera compilacion puede verse ralentizada
por la sincronizacion de `Library/`; para builds repetidos se recomienda trabajar
en una carpeta local fuera de OneDrive.

Si el ejecutable falla, el registro se encuentra en `%USERPROFILE%/AppData/LocalLow/MCOC/Edificio Viewer/Player.log`.

El CSV es generado por `Edificio/model/builders/generar_modelo_manual.py` desde
`Edificio/data/geometry/geometria_manual.json`. No editarlo manualmente: modificar la fuente
de geometría y volver a ejecutar el generador.
