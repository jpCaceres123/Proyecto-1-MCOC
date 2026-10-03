# Edificio AR para Android

Aplicación nativa Android con ARCore, creada para identificar las vigas y columnas del modelo mediante imágenes impresas. El Redmi Note 9 Pro aparece en la [lista oficial de ARCore](https://developers.google.com/ar/devices). Se requiere Android 7 o superior y Google Play Services for AR actualizado. El APK incluye ARM64 y resultados OpenSees precalculados; el teléfono no ejecuta Python ni recalcula la estructura.

## Instalación y primera prueba

1. Copiar `dist/EdificioAR.apk` al Redmi e instalarlo. Autorizar la instalación desde la aplicación usada para abrir el archivo si Android lo solicita.
2. Abrir Edificio AR, permitir la cámara e instalar/actualizar Google Play Services for AR cuando se solicite.
3. Abrir `markers/imprimir.html` en un navegador del computador. Inicialmente muestra los IDs **1, 241 y 246**: una columna y las dos vigas interiores del último cambio. Confirmar sus coordenadas impresas antes de asignar las etiquetas físicas.
4. Imprimir al **100%**, sin ajustar a página. La imagen completa debe medir **200 × 200 mm**; verificar con una regla. Se puede imprimir cada ID del catálogo introduciendo su número.
5. Colocar la imagen fija y plana, con `ARRIBA` hacia arriba, sobre la cara del elemento a identificar. Centrarla en el punto medio longitudinal de la barra analítica. En vigas, el lado derecho de la imagen debe apuntar de **i a j**; en columnas, `ARRIBA` apunta de **i a j**.
6. En **Escanear IDs**, activar los IDs impresos (máximo 24 simultáneos). Apuntar a la imagen completa con iluminación uniforme; evitar reflejos. La aplicación informa cuando la imagen se detecta y crea un anclaje.
7. Elegir **G, Q, EX, EY o R**, y **N, Vy, Vz, T, My o Mz**. Mover el control de posición para consultar cualquier punto entre i y j. N/V se muestran en kN; T/M en kN·m. Los valores conservan los signos de los diagramas OpenSees. **Resultados +** abre los desplazamientos `Ux/Uy/Uz`, área tributaria, losas asociadas, cargas aplicadas y curva P-M cuando corresponde.
8. Cambiar entre **Maqueta 1:10** y **Escala real 1:1**. Tocar la fila de coordenadas para ajustar el offset normal del eje respecto a la imagen. La profundidad del centro suele ser la mitad del espesor perpendicular a la cara de montaje, hacia el interior. La aplicación sugiere un valor para la cara normal al eje local z. Ajustar según la cara efectiva y las cotas del elemento; al cambiar escala el offset se escala automáticamente.
9. Usar **Reanclar** para volver a detectar y establecer la pose. **Catálogo** permite consultar resultados sin identificación AR; la pantalla lo señala expresamente.

El catálogo inicial abre las dos vigas pedidas por sus IDs actuales, pero el vínculo definitivo siempre es ID + coordenadas + hash. Los IDs pueden variar al reconstruir la geometría: regenerar los datos AR y volver a imprimir las imágenes necesarias cuando cambie el modelo.

## Relación entre coordenadas

OpenSees y ARCore usan sistemas derechos. Unity usa el mapeo existente `(X,Z,Y)` del visor. Esta aplicación Android usa directamente ARCore y transforma la base local derecha del elemento, evitando introducir la reflexión de Unity.

Para un punto global `p`, el punto medio de la barra es `c=(pi+pj)/2`. La matriz `B` contiene en sus columnas los ejes locales `x,y,z` exportados por OpenSees. Primero `plocal=Bᵀ(p−c)`.

La orientación del marcador convierte el punto local a `pmarker=scale*C*plocal+(0,offset,0)`:

- Viga: `C(x,y,z)=(x,z,−y)`.
- Columna: `C(x,y,z)=(y,−z,−x)`.

Ambas matrices `C` son ortonormales y tienen determinante +1. La pose del anclaje lleva este punto a coordenadas AR; la matriz de vista y la de proyección lo llevan a la pantalla. El ancho físico del marcador permanece en 0,20 m. La escala afecta a la representación del miembro y a su offset, no al tamaño de la imagen detectada.

El diagrama 3D se dibuja con amplitud normalizada de 0,35 m para facilitar su lectura. Esa amplitud **no representa una longitud física del esfuerzo**. El diagrama 2D y los valores numéricos muestran magnitudes y signos del resultado, con interpolación lineal entre las estaciones exportadas.

El desplazamiento en una estación se interpola con la formulación cúbica Euler-Bernoulli usada por el visor Unity, a partir de traslaciones y giros nodales del caso OpenSees. El área tributaria es la suma de las áreas asignadas directamente a esa viga en `beam_load_cases`; una columna normalmente muestra cero porque recibe reacciones de vigas y no una losa repartida directamente. Las cargas G/Q informadas son las resultantes verticales distribuidas sobre la barra: peso propio, carga muerta de losas y carga viva de losas. EX/EY son fuerzas nodales de diafragma y su carga distribuida directa se informa como cero.

La curva P-M se muestra únicamente para columnas HA del tipo de referencia 70 × 70 cm, usando `PM_puntos.csv`. El punto de demanda usa `P=N` y la resultante `√(My²+Mz²)` de la estación seleccionada. Esta superposición es referencial: compara una demanda biaxial resumida con una curva uniaxial y no reemplaza una verificación biaxial de capacidad. Vigas y columnas tubulares indican que no hay una curva P-M verificada aplicable.

El contorno rectangular recupera las dimensiones en los ejes locales del contrato analítico: `dy=√(12 Iz/A)`, `dz=√(12 Iy/A)`. Así se respeta la orientación efectiva de Iy/Iz incluso donde la sección base y las secciones editadas emplean convenciones b/h diferentes. Los perfiles SHS conservan el ancho exterior exportado. El contorno de un perfil tubular representa su envolvente exterior.

Un marcador por barra supone conocer su colocación y orientación. El reconocimiento de la imagen acredita su ID; no reconoce automáticamente el hormigón ni distingue elementos sin etiqueta. Un marker movido o asignado al elemento equivocado rompe la correspondencia física. La pose se mantiene mediante el anclaje al salir la imagen del encuadre y se indica como última pose conocida.

## Regenerar y compilar

Desde la raíz del repositorio:

```powershell
python Edificio/visualization/android-ar/tools/export_ar_data.py
python Edificio/visualization/android-ar/tools/verify_ar_data.py
powershell -NoProfile -ExecutionPolicy Bypass -File Edificio/visualization/android-ar/build-apk.ps1
```

La exportación lee `Edificio/results/modelo_3d_manual.json` y el contrato `semana4_resultados.json` utilizado por Unity. No escribe resultados estructurales. El hash de ambos archivos se conserva en el paquete AR. Al cambiar el modelo, ejecutar antes el flujo de generación y análisis del edificio descrito en `Edificio/README.md`.

El script de compilación usa JDK 17, Gradle 8.10.2 y Android SDK en `%USERPROFILE%/.codex/android-ar-tools`. Se pueden instalar con `tools/bootstrap_toolchain.py`; después instalar `platform-tools`, `platforms;android-35` y `build-tools;35.0.0` con sdkmanager. También puede abrirse esta carpeta como proyecto en Android Studio. El APK entregado está firmado para desarrollo e instalación directa; no es una publicación en Google Play. No se usa Unity para compilar este APK ni se modifica su escena existente.

## Verificación y prueba física

`verification.json` contiene la identidad de los datos y, cuando está disponible `arcoreimg.exe`, la calidad de todas las imágenes. El resumen de compilación, lint y firma está en `dist/delivery.json`. El umbral del verificador de marcadores es 75 puntos, siguiendo la [recomendación de Google](https://developers.google.com/ar/develop/augmented-images/arcoreimg).

Tras regenerar desde las fuentes estructurales vigentes, el contrato incluye **612** vigas y columnas y **652** paneles SQ4. El script de compilación también comprueba que el APK contenga exactamente la instantánea y los marcadores de los archivos fuente y reconstruye `dist/delivery.json`; un hash antiguo bloquea la entrega. Tres imágenes nuevas (IDs 344, 431 y 653) requieren evaluación de calidad con la herramienta oficial `arcoreimg.exe`: mientras no esté disponible, `verification.json` y `dist/delivery.json` indican calidad **PENDING**, sin reutilizar puntuaciones de otro juego de marcadores. El equipo confirmó que usó el Redmi Note 9 Pro con los marcadores **1, 241, 246 y otros**, y que observó resultados iguales a los del visor Unity. Esta confirmación manual no está codificada en `physical_device_test`, que permanece `PENDING` en los manifiestos automáticos; no se comunicaron medidas del error espacial.

Para validar en el Redmi:

- Confirmar permiso, cámara y detección de los tres marcadores iniciales.
- Comparar ID, coordenadas y esfuerzos G/Q/R con los archivos del computador.
- Medir la distancia entre extremos virtuales y físicos en escala 1:1. Registrar error lateral/vertical en centímetros y repetir tres anclajes.
- Ocultar el marcador y comprobar continuidad del anclaje; cambiar de etiqueta y usar Reanclar.
- Probar pausa/reanudación, permiso rechazado, catálogo sin cámara y cambio de IDs activos.

La precisión en obra y el comportamiento de la cámara no se pueden certificar únicamente compilando ni comparando resultados estructurales con Unity. Se informó una prueba funcional en el Redmi; siguen pendientes la **medición del error de alineamiento** y un registro reproducible de las condiciones de detección y seguimiento. No se fabricaron capturas ni mediciones en terreno.

## Referencias

- [Augmented Images para Android](https://developers.google.com/ar/develop/java/augmented-images/guide).
- [Anclajes ARCore](https://developers.google.com/ar/develop/anchors).
- [ARCore SDK Android](https://github.com/google-ar/arcore-android-sdk).
- [Compilar Android por línea de comandos](https://developer.android.com/build/building-cmdline).
