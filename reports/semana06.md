# Semana 6 — AR para Android

Implementación: `Edificio/visualization/android-ar/`. Plataforma objetivo: Xiaomi Redmi Note 9 Pro, Android ARM64, ARCore. El visor Unity conserva sus recursos y escena; el APK usa el mismo contrato de resultados en una aplicación Android nativa.

## Flujo implementado

Imagen impresa de 20 cm → reconocimiento ARCore → pose de la imagen → anchor → transformación de los ejes locales OpenSees → eje y sección de viga/columna → ID, desplazamiento, esfuerzos N/V/T/M, área tributaria, carga aplicada y curva P-M cuando corresponde.

Los resultados se calculan previamente en el computador. El teléfono carga los datos, detecta imágenes, sigue la pose y dibuja la representación. La consulta del catálogo es manual y se identifica como tal.

## Transformación

Sea `c` el punto medio de la barra y `B=[xlocal ylocal zlocal]`. El punto global OpenSees se transforma mediante `pAR=Tanchor * (scale * C * Bᵀ * (p−c) + offset)` usando coordenadas homogéneas. Para vigas `C(x,y,z)=(x,z,−y)`; para columnas `C(x,y,z)=(y,−z,−x)`. Las dos matrices son rotaciones derechas. El offset normal al marcador se ajusta tocando la fila de coordenadas. La imagen debe colocarse con la orientación i→j que indica la guía.

Unity conserva el mapeo `(X,Z,Y)` existente para su visor. La aplicación Android no usa ese mapeo intermedio: ARCore es derecho y recibe la base local OpenSees directamente.

## Correspondencia y evidencia

Se exportan 619 vigas/columnas y los casos G, Q, EX, EY y R. Los marcadores iniciales son la columna 1 y las vigas 241 y 246. `markers/imprimir.html` presenta sus IDs, coordenadas, dimensiones e imágenes. La correspondencia numérica se comprueba contra cada estación del contrato fuente. Los hashes están en `app/src/main/assets/structural_data.json` y `verification.json`.

La identificación de un elemento **real**, las capturas desde el Redmi y el error de alineamiento están pendientes de la demostración física. Las imágenes generadas son referencias imprimibles; no son evidencia de detección realizada.

## QA

| Prueba | Estado |
| --- | --- |
| Equilibrio G/Q y corte basal EX/EY | OK en los resultados estructurales disponibles; consultar `Edificio/results/verificaciones_globales.csv` |
| Superposición | OK en los resultados estructurales disponibles |
| M-phi y P-M columna/muro | Resultados del análisis previo; esta aplicación no recalcula capacidad |
| IDs y estaciones del contrato Unity → Android | Verificación automática en `verification.json` |
| Calidad de imágenes | Evaluación con herramienta oficial arcoreimg en `verification.json` |
| Compilación, lint y firma APK | Log de compilación en la carpeta Android |
| Detección AR en Redmi y precisión física | Pendiente de prueba en dispositivo |

## Errores conocidos y validación de precisión

El programa identifica imágenes y depende de su asignación correcta a elementos. La alineación supone un marcador de dimensiones exactas, centrado y orientado i→j; requiere corregir la distancia entre cara física y eje analítico. El anclaje puede derivar con movimiento, mala luz u oclusiones. Reanclar permite restablecerlo. Cada sesión admite 24 imágenes seleccionadas para reducir el tiempo inicial y el trabajo de búsqueda.

Para estimar precisión: medir tres veces la diferencia en cm entre cada extremo virtual y la referencia física, registrar error horizontal/vertical y repetir a distintas distancias. No se ha atribuido precisión numérica sin estas mediciones.

## Plan final

Núcleo implementado: permiso de cámara, sesión ARCore, imagen, pose, anchor, transformación, ID, casos de carga, esfuerzos y diagramas; catálogo y selección de imágenes; compilación APK.

Polish: pruebas de legibilidad y rendimiento en el Redmi, medición de alineamiento, capturas y ajustes de ergonomía.

Honors: calibración mediante varios marcadores, gestión de plantas y sincronización de nuevas instantáneas de resultados.
