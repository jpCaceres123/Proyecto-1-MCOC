# Visualizador Unity

1. Abrir esta carpeta como proyecto Unity `6000.5.10f1`.
2. Abrir `Assets/Main.unity`.
3. Ejecutar la escena. El panel izquierdo conserva los controles de Semana 2.
   El panel derecho agrega Semana 3: casos G, Q, EX, EY y R, deformada
   amplificada, fuerzas en centros de masa, giros de piso y capacidad HA.

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

El CSV es generado por `P1L2/scripts/generar_modelo_manual.py` desde
`P1L2/data/geometria_manual.json`. No editarlo manualmente: modificar la fuente
de geometría y volver a ejecutar el generador.

## Datos de Semana 3

Desde la raíz del repositorio, ejecutar `python P1L3/ejecutar.py`. El comando
actualiza automáticamente los CSV `semana3_*.csv` en `Assets/Resources/`.
Después, abrir o reconstruir este mismo proyecto Unity.

La sección HA usa los resultados no lineales de OpenSees generados por P1L3.
Sus materiales y armadura están documentados en `P1L3/parametros.json` y
`P1L3/INFORME.md`.
