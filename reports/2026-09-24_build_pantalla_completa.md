# Build de Unity a pantalla completa

Fecha: 24 de septiembre de 2026.

El ejecutable Windows ahora inicia en **pantalla completa sin bordes**, con la
resolución nativa del monitor. El menú **Build → Edificio Viewer → Construir EXE**
usa ese modo. El proyecto también lo declara en Player Settings y, al arrancar,
el visor lo restablece si Unity había guardado una preferencia antigua de modo
ventana. Una opción explícita de lanzamiento (`-screen-fullscreen` o
`-window-mode`) conserva su prioridad; `Alt+Enter` permite cambiar de modo
durante el uso.

Se generó una compilación Windows64 nueva con Unity 6000.5.9f1. El registro
finalizó con `Build Finished, Result: Success`. El ejecutable y sus archivos
adjuntos están en
`../Edificio/visualization/unity/UnityVisualization/Build/Fullscreen-2026-09-24-final/`.
La carpeta `Build/` es una entrega local y no se incluye en GitHub por su
tamaño. Los cambios de código y de configuración sí quedan versionados.
