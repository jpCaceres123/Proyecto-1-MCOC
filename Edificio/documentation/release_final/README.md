# Verificación de la entrega final

La revisión ejecutó 55 pruebas Python y todas terminaron en OK. El registro completo está en python-tests.txt. Se cotejaron las 16 entradas de manifest.json y se reconstruyó la verificación demanda-capacidad con 1650 comprobaciones interiores y ninguna fuera.

Unity 6000.5.10f1 reconstruyó el visor Windows mediante BuildProject.Build y registró BUILD COMPLETADO. verificacion_entrega.json conserva hashes de los archivos distribuidos. El ejecutable superó además una prueba de arranque automatizada, con salida 0 y carga de G, Q, EX, EY y R, documentada en viewer-startup.log.txt. No se realizó una nueva prueba interactiva ni una prueba física del teléfono. baseline-audit.json registra una reconstrucción independiente aislada con estado OK, sin violaciones ni cambios de cargas regeneradas.

La APK AR existente se cotejó con la instantánea, la geometría de overlays y cada marcador actual. Su SHA-256 coincide con el reporte de construcción y firma disponible. La reconstrucción Android no pudo iniciarse porque no se encontró el toolchain local. apk-verificacion_actual.json documenta el cotejo de recursos y no acredita una nueva firma ni compilación.

Siguen pendientes la calidad oficial de marcadores, las mediciones AR en terreno y la construcción y prueba física de Cardboard. No se distribuye una APK Cardboard.

El producto Windows se entrega en EdificioViewer_Windows.zip y debe extraerse completo antes de abrir EdificioViewer.exe. La APK EdificioAR-Honors.apk se instala en Android compatible con ARCore. Las instrucciones completas están en el README de la raíz del repositorio.
