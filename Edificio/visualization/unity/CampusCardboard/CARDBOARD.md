# Separación de la aplicación móvil — 6 de octubre de 2026

Este proyecto es independiente de CampusPlayable. La versión móvil arranca en Cardboard y no instancia el menú PC, el rifle ni el avatar de tercera persona. Las configuraciones, paquetes, escena, cachés y futura APK pertenecen a esta carpeta.

Se conservó el diseño del panel espacial y se añadieron AVANZAR/RETROCEDER. Selección automática por mirada: **2 segundos**. La pulsación explícita del visor sigue permitiendo confirmación inmediata. Al cambiar de objetivo se reinicia el temporizador; al mirar fuera del control se detiene el desplazamiento. El eje del menú evita que la posición izquierda/derecha de los botones cambie accidentalmente la dirección de marcha.

El SDK oficial Google Cardboard 1.35.0 se mantiene fijado al commit `36ac9815b8f191fe11e149b7f323368fa86655a6`. Fuente de configuración: [guía oficial](https://developers.google.com/cardboard/develop/unity/quickstart). Referencia de experiencia, no copia de controlador: [Trabajo-MCOC](https://github.com/Santiago411323/Trabajo-MCOC/blob/main/revisiones/actualizacion_android_vr.md).

Los CSV/JSON se copiaron sin modificaciones: mismo contrato geométrico, identificadores, signos, unidades y valores previamente calculados. No se ejecutó un nuevo análisis OpenSees. Las tarjetas son acciones nodales locales y los diagramas esfuerzos de sección según su exportador original. Escalas de diagramas son solo visuales; se conservan las estaciones exportadas.

La selección de 2 segundos, ambos sentidos, detención y consultas se verifican en el editor. La vista previa PC no constituye una prueba de render estéreo ni seguimiento físico. La APK requiere Android Build Support, SDK/NDK y OpenJDK de Unity **6000.5.9f1**, y la aceptación H1 requiere probar el Redmi Note 9 Pro con el visor real. No se declara H1 completo ni se acredita rendimiento/comfort sin esa prueba.

Consultar [README.md](README.md) para abrir la escena y construir la APK. No se hizo push de esta revisión.

## Verificación de la separación

- Prueba en Play con Unity 6000.5.9f1: **21/21 comprobaciones correctas**, incluidas ausencia de avance antes de confirmar, avance, retroceso, espera de 2 s y parada al apartar la mirada, estaciones/valores del diagrama y extremos/signos de la deformada.
- Configuración Android ejecutada en el proyecto móvil: Input System New, ARM64, IL2CPP, Activity, OpenGLES3 y paisaje. La APK no se generó: aún falta el módulo Android de esta versión.
- Compilación C# de la rama nativa Android con DLL reales Cardboard/XR/Input System: correcta; no equivale a compilar o probar la APK.
- SHA-256 de CSV y JSON móviles idénticos a CampusPlayable: no se cambiaron resultados ni geometría.
- Scripts del juego PC devueltos a su versión anterior a la integración VR; sin referencia al controlador ni dependencia Cardboard. Sus otros ajustes previos se conservan.
- En las ejecuciones de editor apareció una excepción interna de `UnityEditor.Search.SearchDatabase` al iniciar su indexador. No procede del controlador VR y no impidió las pruebas; queda registrada, no se considera una prueba de estabilidad del editor.

Log final: `C:/Users/2005j/AppData/Local/Temp/MCOC-cardboard-commit-validation-20261006.log`. Capturas: `CampusCardboardPreview.png`, `CampusBeamMoment.png` y `CampusBeamDeformation.png` en `C:/Users/2005j/AppData/Local/Temp/MCOC/Campus Cardboard/`. Son evidencias de editor, no de estéreo nativo.

## Diagramas sobre la barra seleccionada

El selector permite N, Vy, Vz, T, My, Mz, Deformada y Ocultar. Conserva el identificador del elemento, sus extremos geométricos, caso y estaciones originales. Las etiquetas indican unidades, escala y pico muestreado (no un extremo analítico garantizado). El marcador amarillo sigue la estación consultada. Cambiar de caso actualiza la curva; soltar la ficha o salir elimina la superposición.

La deformada se interpola con Hermite cúbico a partir de desplazamientos y giros nodales, evaluados en coordenadas OpenSees antes de convertir XYZ a XZY. Es una representación nodal amplificada, no una solución interior recalculada ni una corrección por carga distribuida. Se verifican ambos extremos, traslación rígida y signo de giro.

Los diagramas de esfuerzos utilizan todas las estaciones exportadas, sin forzar parábolas: su forma depende del caso de carga y de los datos existentes. Para evitar amplificar ruido numérico, la normalización visual tiene un rango mínimo de 1 kN o 1 kNm, sin alterar ningún valor. El shader permite ver las líneas a través del hormigón y admite estéreo; las etiquetas se orientan al observador y permanecen ancladas al elemento al caminar.

Pendiente: instalar Android Build Support de Unity 6000.5.9f1, generar APK y comprobar estéreo, seguimiento, legibilidad y rendimiento en el Redmi Note 9 Pro con Cardboard. H1 no se declara completo todavía.
