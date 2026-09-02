# Visualizador Unity

1. Crear un proyecto Unity 2022.3 LTS o posterior.
2. Copiar `Assets/Scripts/BuildingVisualizer.cs` y `Assets/Resources/model_3d.csv`.
3. Crear una escena, agregar un `GameObject` vacio y adjuntar `BuildingVisualizer`.
4. Ejecutar la escena. El panel permite activar/desactivar nodos, vigas, columnas, muros, apoyos, diafragmas, IDs, ejes locales y el inspector de area tributaria.

Controles: arrastrar con LMB/RMB para orbitar, MMB para desplazar y rueda para zoom. La seleccion de losa en el panel muestra una primera aproximacion del reparto tributario hacia sus cuatro bordes.

## Crear el EXE

1. Abre este proyecto en Unity.
2. En el menu selecciona `Build > Edificio Viewer > Construir EXE`.
3. El ejecutable quedara en `Build/EdificioViewer.exe` junto con sus carpetas de datos. Distribuye la carpeta `Build` completa, no solo el `.exe`.

Si el ejecutable falla, el registro se encuentra en `%USERPROFILE%/AppData/LocalLow/MCOC/Edificio Viewer/Player.log`.

El CSV es generado por `generar_modelo_desde_excel.py` usando
`nodos_modelo_opensees.xlsx`. No editarlo manualmente: modificar la hoja de
nodos y volver a ejecutar el generador.
