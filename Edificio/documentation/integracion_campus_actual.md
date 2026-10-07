# Integración del recorrido con el modelo actual

El proyecto `visualization/unity/CampusPlayable` incorpora las mejoras del recorrido anterior: fachada con dos voladizos, escaleras exteriores corregidas, conexión interior entre módulos, salas y laboratorios, cafetería y terraza amueblada, núcleo de escaleras y ascensor, exclusión del laboratorio 2-7, avatar, cámaras F5 en tres vistas, láser 1, rifle 2 y terremoto visual P.

Se mantiene la corrección del láser para mostrar esfuerzos de sección del diagrama, con N positivo en compresión. La animación del terremoto no calcula una respuesta dinámica ni cambia las demandas guardadas.

La geometría del recorrido y la del visor estructural usan ahora el mismo `model_3d.csv`. El inspector se regenera completo desde el modelo y la corrida actuales: 616 barras, 82 muros, 654 losas, nueve casos, incluyendo las nuevas continuaciones 680 y 682. Incluye nodos, movimientos, fuerzas, diagramas, cargas tributarias y demandas de muro cuando están disponibles; no conserva respuestas de IDs de otra topología.

`visualization/exports/exportar_inspeccion_campus.py` exporta la geometría y la inspección a CampusPlayable y CampusCardboard. La rutina principal `analysis/load_cases/ejecutar.py` lo llama al completar los resultados. Los recursos generados contienen hashes de procedencia.

**Qué se actualiza:** el análisis, las exportaciones del visor estructural y los datos fuente de los dos proyectos de campus. **Qué requiere reconstrucción:** los ejecutables de Windows, ZIP y APK. La geometría por sí sola no modifica una aplicación ya instalada. En esta integración se reconstruye Windows y se actualiza `C:/Users/nico0/OneDrive/Desktop/CampusIngenieria`. La APK de Android sigue requiriendo su compilación independiente.

El proyecto conserva su edición local en la carpeta que indicó el usuario. Las copias anteriores son historial; no son la fuente del análisis actual.
