# Reporte general e instrucciones de ejecución

Actualización: 6 de octubre de 2026. Todas las rutas siguientes parten de la raíz del repositorio.

## 1. Qué contiene la entrega

Hay **cuatro aplicaciones**. Cardboard y VR son la misma aplicación, no dos proyectos distintos. Python/OpenSees es un servicio de cálculo en el PC, no una quinta aplicación de recorrido.

| Aplicación | Proyecto / entrega | Uso |
|---|---|---|
| CampusPlayable | `Edificio/visualization/unity/CampusPlayable` | Recorrer el campus en PC, arquitectura/estructura y primera/tercera persona |
| UnityVisualization | `Edificio/visualization/unity/UnityVisualization` | Inspección estructural, carga móvil, diagramas, reanálisis y capacidad |
| CampusCardboard | `Edificio/visualization/unity/CampusCardboard` | VR móvil: mirada, locomoción y consulta de resultados |
| Android AR | `Edificio/visualization/android-ar` | Cámara del teléfono, marcadores y superposición de resultados |

Los tres proyectos Unity tienen actualmente `ProjectVersion.txt` en **6000.5.9f1**. Algunos README antiguos mencionan otras versiones: usar esta guía y la versión guardada del proyecto para esta entrega. No se cambió la licencia de Unity.

## 2. Resumen de lo realizado

- Recorrido PC separado de VR, avatar AmongUs en tercera persona y láser oculto en tercera persona; modo estructural y dos ascensores interiores en los huecos existentes. La arquitectura visual no cambia rigidez, apoyos ni resultados OpenSees.
- Consulta de esfuerzos, cargas y desplazamientos conservando `elementTag`, estaciones, casos y unidades. La carga móvil y el recorrido siguen siendo funcionalidades distintas del reanálisis remoto.
- Contrato de datos versionado, manifiestos y hashes SHA256. Los resultados incompletos, incompatibles o con controles `REVISAR` no sustituyen la revisión válida.
- Benchmarks independientes de vigas con carga uniforme, puntual y voladizo. No se fuerzan momentos parabólicos: la forma depende de la carga real y del equilibrio.
- H1: proyecto Cardboard independiente, selección por mirada de 2 segundos, locomoción continua o por pasos de 2 m, colisiones, consulta de casos/estaciones, diagramas sobre barras y capacidad junto a columnas.
- H2: sector métrico con varios marcadores medidos, transformación común, rechazo de observaciones discordantes, anclaje compartido y exportación de error en puntos independientes.
- H3: selección de hasta cuatro barras, esfuerzos, deformada interpolada, desplazamientos nodales de muros, áreas tributarias y P–Mz junto a una columna compatible.
- H4: backend Python/OpenSees en red local, autenticación temporal, validación, cola serial, trabajos aislados, cancelación, límite de tiempo y descarga de revisiones verificadas. Clientes Unity y Android.
- H5: refuerzo editable y regeneración de capacidad nominal uniaxial P–My/P–Mz con fibras, comparación anterior/nueva y demanda del mismo eje.
- Dependencias numéricas fijadas para reproducir la base: Python 3.12, OpenSeesPy 3.8.0.0 y NumPy 2.5.2. Se resolvió una discrepancia histórica reproduciendo esas versiones, sin ampliar tolerancias ni editar los resultados originales.

### Última corrección de Cardboard

- Menú principal compacto, únicamente botones; se quitaron los textos explicativos y la ficha del panel.
- `Diagrama` y `Backend` reemplazan el menú principal: ya no se superponen con movimiento. `Volver` recupera los controles.
- Se agregaron `IZQUIERDA` y `DERECHA`, además de avanzar/retroceder. Apartar la mirada detiene el movimiento; cambiar de página reinicia la selección y detiene la locomoción.
- Los valores no se eliminaron de la consulta: permanecen junto al diagrama de la barra, con caso, unidades, escala y valor de la estación en amarillo.
- La revisión aprobó **31 comprobaciones de interacción en editor** y se inspeccionaron capturas de las tres páginas. Esto no equivale a prueba física de VR.

## 3. Qué está validado y qué falta

La evidencia de software está en [documentation/honors_evidence/README.md](documentation/honors_evidence/README.md). Antes de esta última corrección se aprobaron 51 pruebas Python, 5 Java, compilación/lint/firma de la APK AR y pruebas Unity de conexión real al backend. También se compararon cinco casos contra una corrida directa independiente. Las 31 comprobaciones recientes corresponden al menú/control Cardboard; no son 31 pruebas de teléfono ni una repetición de las pruebas HTTP.

**No se declaran completos los cinco honors en terreno.** Pendientes:

1. Instalar Android Build Support de Unity 6000.5.9f1, generar e instalar la APK Cardboard.
2. Probar estéreo, perfil del visor, seguimiento de orientación, locomoción y comodidad en el Redmi Note 9 Pro; medir rendimiento durante cinco minutos.
3. Medir las posiciones/orientaciones de tres marcadores y puntos independientes del sector físico; confirmar IDs contra el modelo.
4. Probar registro AR, tres registros independientes y deriva durante 60 segundos. Objetivos del proyecto: RMS ≤5 cm y máximo ≤10 cm, no límites oficiales del enunciado.
5. Probar conexión Wi-Fi y errores de red desde el teléfono; grabar evidencia.
6. Confirmar con el docente la aceptación de relocalización mediante marcadores: no hay persistencia autónoma del anchor ni Cloud Anchors.

Los ZIP/EXE antiguos de CampusPlayable pueden contener versiones anteriores. Para presentar el código actual usar Play en Unity o construir un ejecutable nuevo; no asumir que un ZIP anterior incorpora estos cambios.

## 4. Abrir y ejecutar las aplicaciones de PC

En Unity Hub, agregar la **carpeta del proyecto** indicada en la tabla (no el ZIP ni una carpeta `My project` creada accidentalmente). Abrir con 6000.5.9f1, esperar la importación y luego abrir la escena.

### CampusPlayable

- Escena: `Assets/Campus.unity`. Pulsar Play y `Entrar / continuar`.
- WASD caminar, mouse mirar, Shift correr, Espacio saltar.
- E interactuar con puertas/ascensores; Esc pausa; R volver al acceso.
- F5 primera/tercera persona; F6 arquitectura/modo estructural.
- Inspector: L activar/desactivar, Q cambiar caso, clic derecho fijar elemento, Tab cambiar sección, I extremos i/j y rueda desplazar ficha.
- Para una entrega Windows actual: `Campus > Construir Windows`. Distribuir toda la carpeta de salida, no solo `CampusIngenieria.exe`.

### UnityVisualization

- Escena: `Assets/Main.unity`. Pulsar Play.
- Arrastrar con botones izquierdo/derecho para orbitar, botón central desplazar y rueda zoom.
- Activar las capas necesarias y seleccionar un elemento para consultar sus datos, diagramas y cargas. Las losas muestran reparto tributario, no esfuerzos de placa FE.
- Para un EXE: `Build > Edificio Viewer > Construir EXE`. Distribuir la carpeta `Build` completa.
- Reanálisis/capacidad requieren el backend de la sección 7; abrir Unity no inicia ese servidor automáticamente.

## 5. Cardboard: preview y APK

Escena: `Assets/CampusCardboard.unity` en el proyecto **CampusCardboard**. Play inicia la previsualización de PC.

- Botón derecho mantenido + mouse: mirar; M: recolocar menú; clic izquierdo: confirmar explícitamente; F7/Esc: salir.
- Mirar un botón o elemento durante **2 segundos** para seleccionarlo.
- Menú principal: izquierda, avanzar, derecha, retroceder, modo de marcha, recentrar, diagrama, caso, estación i/x/j, estructura, otro piso, soltar ficha, backend y salir VR.
- Diagrama: N, Vy, Vz, T, My, Mz, deformada, ocultar y volver.
- Backend: configurar, Q adicional +2/+5 kN/m, refuerzo 5 barras por cara Ø28/32/36, cancelar, modelo base y volver.
- Los pasos y el movimiento continuo conservan controles de obstáculos/vacíos. No moverse físicamente por el recinto mientras se usa el visor sin supervisión.

**Estado de esta entrega:** no hay una APK Cardboard generada; falta el módulo Android de Unity .9. No instalar la APK AR esperando obtener VR.

Para generar Cardboard:

1. Unity Hub → Installations → 6000.5.9f1 → Add modules.
2. Instalar Android Build Support, Android SDK & NDK Tools y OpenJDK. Completar la autorización de Windows.
3. Abrir CampusCardboard → `Campus > Cardboard > Configurar Android`; reiniciar si Unity lo solicita.
4. `Campus > Cardboard > Construir APK`.
5. Tras una compilación correcta: `Edificio/visualization/unity/CampusCardboard/Build/Android/CampusCardboard.apk`, junto a `delivery.json`.
6. Copiar la APK al Redmi, instalar, configurar el perfil QR del visor y probar ambos ojos, giro de cabeza y recentrado.

El seguimiento es de **orientación**, no seguimiento posicional 6 DOF. El preview no acredita el SDK nativo del teléfono. Prueba de editor: `Campus > Cardboard > Probar interacción en Play`; revisar `CAMPUS_VR_CHECK_COMPLETE passed=True` en Console.

## 6. APK AR e instrucciones de marcadores

La APK disponible para esta entrega es:

**[EdificioAR-Honors.apk](visualization/android-ar/dist/EdificioAR-Honors.apk)**

Es una compilación de depuración para pruebas; no una publicación en tienda. No confundirla con la APK anterior `EdificioAR.apk`.

1. Copiarla al Redmi Note 9 Pro y abrirla desde el administrador de archivos. Si Android lo solicita, habilitar la instalación desde esa aplicación.
2. Aceptar el permiso de cámara y disponer de Google Play Services for AR compatible.
3. Abrir [imprimir.html](visualization/android-ar/markers/imprimir.html), imprimir al 100 % y medir que cada imagen completa tenga **20 × 20 cm**.
4. Activar el ID correspondiente en `Escanear IDs`; mantener el marcador plano, visible y bien iluminado.
5. Mostrar el elemento, `elementTag`, caso, componente, estación y unidades. El catálogo sin detección no demuestra AR.

### Sector avanzado H2/H3

Se necesitan medidas reales, no únicamente fotos de una viga. Seguir [HONORS.md](HONORS.md), sección «Preparar H2 en terreno».

- Crear y completar un manifiesto de sector: hash del modelo, origen común, IDs, tamaño y poses medidas de al menos tres marcadores. No marcar `surveyed:true` sin medir.
- Copiar el JSON al teléfono e importar mediante `Sector JSON`; mostrar al menos dos marcadores compatibles hasta estabilizar el registro.
- `Catálogo` y `Añadir/quitar` permiten comparar hasta cuatro barras sin sustituir el registro.
- Mostrar deformada, áreas tributarias y P–Mz solo para elementos compatibles.
- Medir controles independientes con `Medir error`; usar `Exportar` **antes de Reanclar**, porque un nuevo registro limpia las mediciones previas.
- Reiniciar exige detectar nuevamente un marcador: guardar configuración no conserva automáticamente el anchor del mundo.

Un marcador sobre una mesa puede demostrar detección y visualización, pero no demuestra alineamiento con una viga física ni precisión de un sector completo.

## 7. Backend Python/OpenSees y pruebas H4/H5

En este PC ya está preparado el entorno aislado `%USERPROFILE%/.mcoc-honors/Python312`. En otro equipo instalar Python 3.12 y, desde la raíz del repositorio, preparar las dependencias:

```powershell
.\Edificio\analysis\backend\setup-environment.ps1 -PythonInterpreter 'C:\ruta\a\Python312\python.exe'
```

Reemplazar esa ruta por el ejecutable real. No usar las dependencias del laboratorio P1L1 para este backend.

### Servidor

Para probar únicamente desde el PC:

```powershell
.\Edificio\analysis\backend\start-lan.ps1 -Address 127.0.0.1
```

Para conectar el teléfono, PC y Redmi deben estar en la misma red privada de confianza. Consultar la IPv4 del PC con `ipconfig` y usarla en lugar del ejemplo:

```powershell
.\Edificio\analysis\backend\start-lan.ps1 -Address 192.168.1.20
```

Dejar la consola abierta. Copiar **la URL y el token temporal impresos** al cliente. El teléfono no debe usar `127.0.0.1`: esa dirección apunta al propio teléfono. Si Windows solicita acceso, autorizar solo red privada conforme a las normas locales. Una Wi-Fi con aislamiento de clientes puede impedir la conexión.

No publicar el token, no abrir el servicio a internet y no subir credenciales al repositorio. Se usa HTTP con token en LAN de confianza, no TLS. Ctrl+C detiene el servidor. Ejecuta un trabajo a la vez, con carpetas aisladas y timeout de diez minutos; ante fallo permanece la última revisión válida.

### Demostración H4

En UnityVisualization seleccionar una viga (la 207 fue usada en pruebas automáticas), abrir configuración de carga/sección, activar el backend del PC por Wi-Fi e introducir URL/token. Solicitar Q adicional de 2 kN/m y `Aplicar elemento y reanalizar`. Esperar la revisión validada; comparar el caso y el diagrama antes/después. Una suma de casos guardados no equivale a reanálisis.

En AR usar `PC Wi-Fi` con URL/token y la solicitud Q adicional de la barra seleccionada. Cardboard móvil usa `Backend > Configurar`. Para el procedimiento detallado y límites consultar [HONORS.md](HONORS.md).

Las variantes parten del modelo base, no acumulan solicitudes anteriores. En AR una revisión nueva invalida un sector asociado al hash anterior: demostrar primero H2/H3, luego reanalizar; volver al modelo base antes de reutilizar ese levantamiento.

### Demostración H5

Restaurar el modelo base. Seleccionar una columna HA compatible (la 1 fue usada en pruebas), abrir refuerzo editable/capacidad, modificar barras por cara y diámetro y regenerar P–My/P–Mz. Comparar curva anterior, nueva y demanda del mismo eje. La configuración de prueba fue cinco barras por cara Ø32 mm, contando esquinas sin duplicarlas.

Es capacidad **nominal de sección uniaxial** con hipótesis académicas: no verificación normativa, miembro, biaxial ni segundo orden. Cambiar refuerzo no modifica automáticamente EI del modelo global lineal-elástico.

## 8. Presentación en vivo y comprobaciones

Orden recomendado: recorrido PC → visor/diagramas → Cardboard → AR → reanálisis → capacidad. Preparar proyectos, marcadores, Wi-Fi y servidor antes de presentar. Proyectar la pantalla del teléfono si se dispone de esa opción y llevar grabaciones de respaldo; no asumir salida HDMI del Redmi.

Para cada demostración mostrar: ID, caso, unidades, revisión y distinción entre valor calculado y escala visual. Los diagramas conservan datos exportados; la deformada de barra es interpolación Hermite amplificada, no una flecha interior exacta por carga distribuida. Las superficies de losas son tributarias, no placas analizadas.

Pruebas reproducibles desde la raíz:

```powershell
$honorsPython = Join-Path $env:USERPROFILE '.mcoc-honors/Python312/Scripts/python.exe'
& $honorsPython -m unittest discover -s Edificio/verification/tests -v
& $honorsPython Edificio/verification/interactive/verify_unity_backend.py --unity 'C:/Program Files/Unity/Hub/Editor/6000.5.9f1/Editor/Unity.exe'
& $honorsPython Edificio/verification/interactive/audit_honors_base.py
```

Las pruebas no sustituyen las de teléfono. No regenerar ni editar resultados originales para ocultar discrepancias. Consulte [HONORS.md](HONORS.md) para reconstrucción Android, contrato, medición de error y límites físicos, y [documentation/honors_evidence/README.md](documentation/honors_evidence/README.md) para evidencia histórica de software.

## 9. Alcance del commit y publicación

Esta revisión incorpora la corrección del menú Cardboard y esta guía. Las etapas H0–H5 están en los commits previos del historial. No se incorporan `Library`, `Temp`, proyectos accidentales, preferencias locales del editor ni builds Windows sin verificar. No se sobrescriben las fuentes de geometría, planos, resultados de análisis ni entregas previas.
