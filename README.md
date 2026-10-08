# Proyecto 1 MCOC

Proyecto de modelación estructural de un edificio en OpenSeesPy, con análisis de cargas gravitacionales y sísmicas, capacidad de secciones y visualización mediante Unity, realidad aumentada y realidad virtual móvil.

El modelo global se encuentra en `Edificio/`. El benchmark de Semana 1 permanece separado en `P1L1/`. **Los comandos de esta guía se ejecutan en PowerShell para Windows, desde la raíz del repositorio**, donde está este README. No es necesario abrir Unity para ejecutar el análisis.

El informe final se entrega en PDF y está disponible en [Semana07.pdf](reports/Informes%20pdf/Semana07.pdf).

## Inicio rápido

Para ejecutar el edificio por primera vez, instalar Python 3.12 de 64 bits y situarse en la raíz del repositorio. Ejecutar cada comando en orden y comprobar su resultado antes de continuar. Este flujo reconstruye resultados y recursos del visor.

```powershell
py -3.12 -m venv .venv
$projectPython = Join-Path (Get-Location) '.venv/Scripts/python.exe'
& $projectPython -m pip install -r .\Edificio\requirements_honors.txt
& $projectPython -m pip check
& $projectPython .\Edificio\model\builders\convertir_cargas_losas.py
& $projectPython .\Edificio\model\builders\generar_modelo_manual.py
& $projectPython .\Edificio\verification\load_transfer\verificar_modelo.py
& $projectPython .\Edificio\analysis\load_cases\ejecutar.py --carga-movil
& $projectPython .\Edificio\verification\verificar_demanda_capacidad.py
& $projectPython -m unittest discover -s .\Edificio\verification\tests -p 'test_*.py' -v
```

Comprobar el estado de `Edificio/results/resumen_global.json`, `verificaciones_globales.csv`, `verificacion_demanda_capacidad.json` y `verificacion_carga_movil.json`. Resolver los estados `REVISAR` antes de presentar la corrida como verificada.

Después, agregar `Edificio/visualization/unity/UnityVisualization` en Unity Hub, abrir con **6000.5.10f1**, cargar `Assets/Main.unity` y pulsar Play.

Para ampliar cada paso, consultar [dependencias](#2-dependencias-y-versiones), [análisis y resultados](#3-ejecutar-el-análisis-y-generar-resultados), [apertura del viewer](#4-abrir-el-viewer-y-construir-las-aplicaciones-de-pc), [compilación móvil](#6-compilar-e-instalar-móvil) y [tests](#7-ejecutar-tests-y-verificaciones).

## 1. Organización del repositorio

| Ruta | Función |
| --- | --- |
| `Edificio/data/` | Entradas de geometría, cargas, parámetros y armadura. |
| `Edificio/model/builders/` | Conversión de cargas y generación del modelo. |
| `Edificio/analysis/` | Casos de carga, capacidad, sismo, carga móvil y backend. |
| `Edificio/results/` | Resultados numéricos, figuras, verificaciones y manifiesto de la corrida. |
| `Edificio/verification/` | Pruebas y verificadores reproducibles. |
| `Edificio/visualization/unity/UnityVisualization/` | Visor técnico de geometría, cargas, esfuerzos, deformadas y capacidad. |
| `Edificio/visualization/unity/CampusPlayable/` | Recorrido del campus para PC. |
| `Edificio/visualization/unity/CampusCardboard/` | Proyecto independiente de VR móvil. |
| `Edificio/visualization/android-ar/` | Aplicación Android nativa de AR y marcadores. |
| `Reports/` | Documentación e informes preparados para la entrega. |
| `Enunciados e Instrucciones/` | Requisitos de los laboratorios y de la entrega. |
| `P1L1/` | Benchmark independiente de Semana 1. |

Los planos y antecedentes originales se conservan en sus carpetas de referencia. Los resultados se reconstruyen desde las entradas y el código, sin corregir manualmente los JSON o CSV generados.

## 2. Dependencias y versiones

Para reproducir la base numérica se utiliza el entorno fijado en [`Edificio/requirements_honors.txt`](Edificio/requirements_honors.txt). El archivo general `Edificio/requirements.txt` contiene dependencias sin versiones fijas y no garantiza reproducir exactamente la misma corrida.

| Componente | Versión de referencia | Uso |
| --- | --- | --- |
| Python de 64 bits | **3.12.10**, dentro de la serie 3.12 | Versión registrada en `Edificio/results/manifest.json`. |
| OpenSeesPy | **3.8.0.0** | Análisis estructural y cálculo de secciones. |
| OpenSeesPyWin | **3.8.0.0**, en Windows | Distribución del motor utilizada en este sistema. |
| NumPy | **2.5.2** | Operaciones numéricas. |
| Matplotlib | **3.11.1** | Generación de figuras. |
| openpyxl | **3.1.5** | Lectura y escritura de planillas. |
| FastAPI, Uvicorn, Pydantic y HTTPX | **0.115.12**, **0.34.2**, **2.13.5** y **0.28.1** | Backend y pruebas de comunicación. |
| Unity Editor | **6000.5.10f1** | Versión guardada en los tres proyectos Unity actuales. |
| Unity Hub y Git | Instalados y disponibles | Apertura de proyectos y descarga de paquetes. |
| JDK | **17** | Compilación de AR nativa. |
| Gradle y Android Gradle Plugin | **8.10.2** y **8.7.3** | Construcción de AR. |
| Android SDK para AR | Plataforma **35**, Build Tools **35.0.0 o superior** y Platform Tools | El script selecciona la versión instalada más alta de Build Tools que cumple su umbral. |
| ARCore y JUnit | **1.56.0** y **4.13.2** | Dependencias descargadas por Gradle para AR y sus pruebas Java. |

Los paquetes Unity se restauran desde `Packages/manifest.json` y `Packages/packages-lock.json` de cada proyecto. Cardboard fija el plugin de Google mediante una revisión Git. Para construir VR móvil, instalar **Android Build Support, Android SDK & NDK Tools y OpenJDK para Unity 6000.5.10f1** desde Unity Hub. El SDK de AR nativa y el administrado por Unity pertenecen a flujos distintos.

Algunos documentos y registros históricos mencionan Unity 6000.5.9f1 o 6000.5.11f1. Para abrir el código actual, prevalece `ProjectSettings/ProjectVersion.txt` de cada proyecto, que actualmente indica 6000.5.10f1.

### Preparar el entorno Python

Instalar Python 3.12 de 64 bits con su lanzador `py`. Abrir PowerShell en la raíz del repositorio y crear un entorno aislado para no mezclar dependencias con otros proyectos.

```powershell
py -3.12 --version
py -3.12 -m venv .venv
$projectPython = Join-Path (Get-Location) '.venv/Scripts/python.exe'
& $projectPython -m pip install --upgrade pip
& $projectPython -m pip install -r .\Edificio\requirements_honors.txt
& $projectPython -m pip check
```

Si `py -3.12` no encuentra el intérprete, instalar esa serie o sustituirlo por la ruta del ejecutable Python 3.12. Para igualar también la versión de parche del manifiesto, utilizar 3.12.10. Revisar cualquier error de instalación antes de continuar.

Comprobar las versiones con el mismo intérprete que ejecutará el modelo.

```powershell
& $projectPython -c "import sys, importlib.metadata as m; import openseespy.opensees as ops; print(sys.version); print({p: m.version(p) for p in ('openseespy', 'numpy', 'matplotlib', 'openpyxl')}); print('Motor OpenSees', ops.version())"
```

Los ejemplos siguientes utilizan `$projectPython`. Al abrir otra consola, volver a definirla desde la raíz. Para los scripts PowerShell que llaman internamente a `python`, como la construcción AR, dar prioridad al entorno en la sesión actual.

```powershell
$projectPython = Join-Path (Get-Location) '.venv/Scripts/python.exe'
$env:PATH = "$(Split-Path -Parent $projectPython);$env:PATH"
python --version
```

## 3. Ejecutar el análisis y generar resultados

### Entradas del modelo

| Archivo | Qué se define |
| --- | --- |
| `Edificio/data/geometry/geometria_manual.json` | Coordenadas, elementos y geometría interpretada. |
| `Edificio/data/loads/Cargas_losas.txt` | Antecedentes de carga de losas que convierte el generador. |
| `Edificio/data/loads/cargas_losas.json` | Cargas estructuradas utilizadas por el modelo. |
| `Edificio/data/parameters/parametros.json` | Cargas, masa sísmica, combinación y sección de referencia. |
| `Edificio/data/reinforcement/` | Armadura y asignación de secciones de muros. |

El modelo emplea kN, m y radianes, con cargas superficiales en kN/m² y tensiones en kN/m². Los ejes globales, las restricciones y los ejes locales conservan su significado durante las modificaciones.

### Flujo completo

Detener Play en Unity antes de actualizar sus recursos. Ejecutar cada comando en orden y continuar únicamente si termina correctamente. En PowerShell, `$LASTEXITCODE` permite consultar el código de salida del último comando.

```powershell
& $projectPython .\Edificio\model\builders\convertir_cargas_losas.py
& $projectPython .\Edificio\model\builders\generar_modelo_manual.py
& $projectPython .\Edificio\verification\load_transfer\verificar_modelo.py
& $projectPython .\Edificio\analysis\load_cases\ejecutar.py --carga-movil
& $projectPython .\Edificio\verification\verificar_demanda_capacidad.py
```

1. La conversión interpreta `Cargas_losas.txt` y actualiza `cargas_losas.json`. Si se modifica el JSON directamente, volver a convertirlo sobrescribe esos cambios, por lo que la fuente y su conversión deben mantenerse coherentes.
2. El generador construye `Edificio/results/modelo_3d_manual.json`, exporta la geometría para Unity y prepara tablas geométricas. Este JSON es un contrato generado que no debe editarse a mano.
3. La verificación del modelo revisa geometría y conservación del reparto de cargas antes del cálculo global.
4. El ejecutable resuelve G, Q, EX, EY y R, además de las bases sísmicas EXG, EXQ, EYG y EYQ. Calcula capacidad de columnas y muros y genera figuras, controles y recursos de visualización. `--carga-movil` reconstruye también las bases SQ4 y su identidad para la geometría vigente.
5. El verificador de demanda-capacidad reconstruye su CSV y resumen desde los resultados recién generados. Debe ejecutarse después del análisis para evitar conservar una comprobación anterior.

El flujo sobrescribe archivos de `Edificio/results/`, recursos Unity y `Edificio/documentation/INFORME.md`. Conservar en Git la versión anterior antes de regenerar una entrega. La exportación del recorrido actualiza recursos de CampusPlayable y CampusCardboard; las aplicaciones compiladas necesitan una nueva construcción para incorporar esos cambios.

Para resolver solamente los casos base, ejecutar sin la opción de carga móvil.

```powershell
& $projectPython .\Edificio\analysis\load_cases\ejecutar.py
```

Si cambió la geometría o la base estructural, reconstruir SQ4 antes de usar carga móvil. Se admite otro archivo de parámetros mediante `--parametros`, aunque los resultados siguen escribiéndose en las rutas habituales. En el siguiente comando puede sustituirse la ruta por la configuración que se desea estudiar.

```powershell
& $projectPython .\Edificio\analysis\load_cases\ejecutar.py --parametros .\Edificio\data\parameters\parametros.json --carga-movil
```

### Revisar los archivos generados

Las rutas de resultados de la siguiente tabla son relativas a `Edificio/results/`.

| Resultado | Archivos y finalidad |
| --- | --- |
| Estado global | `resumen_global.json` y `verificaciones_globales.csv`, con controles de equilibrio y compatibilidad. |
| Reparto de losas | `transferencia_Q.csv` y `conservacion_Q.csv`. |
| Masa y sismo | `masas_y_sismo.csv` y `sismo_por_piso.csv`. |
| Respuesta por caso | `G.npz`, `Q.npz`, `EX.npz`, `EY.npz`, `R.npz` y archivos de fuerzas locales y pisos. |
| Capacidad de columnas | `resumen_capacidad.json`, `momento_curvatura.csv`, `PM_puntos.csv` y `capacidad_HA.png`. |
| Capacidad de muros | `resumen_capacidad_muros.json`, `PM_muros_envolvente.csv` y `capacidad_PM_muros.png`. |
| Demanda-capacidad | `verificacion_demanda_capacidad.json` y `.csv`. |
| Carga móvil | `verificacion_carga_movil.json`. |
| Trazabilidad | `manifest.json`, con versiones y hashes de entradas. |

Los datos del visor técnico se exportan a `Edificio/visualization/unity/UnityVisualization/Assets/Resources/`, incluidos `model_3d.csv` y `semana4_resultados.json`.

Revisar **todos los estados `REVISAR`**, aunque el proceso termine sin error. En particular, el verificador de demanda-capacidad informa el estado en el JSON y no convierte por sí solo un incumplimiento en un código de salida distinto de cero. El índice completo está en [`Edificio/results/README.md`](Edificio/results/README.md).

## 4. Abrir el viewer y construir las aplicaciones de PC

### Visor estructural UnityVisualization

1. Instalar Unity Hub y Unity **6000.5.10f1**, con licencia habilitada para ejecutar el editor y construir aplicaciones.
2. Agregar un proyecto existente y seleccionar `Edificio/visualization/unity/UnityVisualization`, la carpeta que contiene `Assets`, `Packages` y `ProjectSettings`.
3. Esperar la importación y restauración de paquetes. Consultar Console si aparecen errores.
4. Abrir `Assets/Main.unity` y pulsar Play. Generar previamente los resultados siguiendo la sección 3.
5. Usar los botones izquierdo o derecho del mouse para orbitar, el central para desplazar y la rueda para zoom. Activar capas y seleccionar un elemento para consultar ID, caso, esfuerzos y diagramas.
6. Para otra corrida, detener Play, regenerar datos, esperar su importación y volver a iniciar la escena. El visor transforma OpenSees `(X,Y,Z)` a Unity `(X,Z,Y)`.

La consulta de resultados guardados funciona sin backend. El reanálisis requiere el servicio de la sección 5. Las losas muestran reparto tributario y las deformadas tienen amplificación visual, que debe leerse junto con las unidades y escalas.

Para construir Windows, detener Play y seleccionar `Build > Edificio Viewer > Construir EXE`. La salida es `Edificio/visualization/unity/UnityVisualization/Build/EdificioViewer.exe`. Distribuir la carpeta `Build` completa, incluidos los archivos auxiliares.

### CampusPlayable y preview Cardboard

| Proyecto | Escena | Cómo abrir y construir |
| --- | --- | --- |
| `Edificio/visualization/unity/CampusPlayable` | `Assets/Campus.unity` | Abrir con Unity 6000.5.10f1, pulsar Play y entrar al recorrido. `Campus > Construir Windows` genera la aplicación PC. |
| `Edificio/visualization/unity/CampusCardboard` | `Assets/CampusCardboard.unity` | Abrir como proyecto independiente con Unity 6000.5.10f1. Play permite revisar controles en PC; la construcción Android está en la sección 6. |

En CampusPlayable se utiliza WASD para caminar, mouse para mirar, Shift para correr, Espacio para saltar y E para interactuar. F5 alterna la cámara y F6 la representación arquitectónica o estructural. Más controles en su [README](Edificio/visualization/unity/CampusPlayable/README.md).

## 5. Iniciar el backend de reanálisis

El servicio usa las dependencias de la sección 2. En otra consola, desde la raíz, definir el intérprete y ejecutar el servidor local.

```powershell
$projectPython = Join-Path (Get-Location) '.venv/Scripts/python.exe'
.\Edificio\analysis\backend\start-lan.ps1 -Address 127.0.0.1 -PythonInterpreter $projectPython
```

Dejar la consola abierta y copiar la URL y el token temporal impresos en la configuración del cliente. El puerto predeterminado es 8765. El servicio ejecuta trabajos aislados de forma serial y entrega una revisión cuando supera sus controles.

Para conectar un teléfono, consultar la IPv4 privada del PC con `ipconfig`. Con ambos equipos en la misma red privada, reemplazar la dirección del ejemplo por la del computador.

```powershell
.\Edificio\analysis\backend\start-lan.ps1 -Address 192.168.1.20 -PythonInterpreter $projectPython
```

Introducir esa dirección del PC, puerto y token en el teléfono. `127.0.0.1` apunta al propio dispositivo y solo sirve para pruebas locales. Si corresponde, permitir el puerto en el firewall de la red privada. Detener con Ctrl+C. El token es temporal y no debe incorporarse a Git ni a la entrega pública.

[`Edificio/HONORS.md`](Edificio/HONORS.md) describe los cambios de carga, sección y armadura. Una combinación de resultados precalculados no constituye un nuevo análisis OpenSees.

## 6. Compilar e instalar móvil

### AR Android nativa

Esta aplicación se construye con Gradle y JDK 17. Primero completar el análisis y revisión de la sección 3 para que la instantánea móvil corresponda al modelo vigente.

El instalador del repositorio descarga JDK, Gradle y Android Command-line Tools en una carpeta del usuario. Requiere conexión a Internet.

```powershell
& $projectPython .\Edificio\visualization\android-ar\tools\bootstrap_toolchain.py
$arTools = Join-Path $env:USERPROFILE '.codex/android-ar-tools'
$arJdk = Get-ChildItem (Join-Path $arTools 'jdk') -Directory | Select-Object -First 1
$env:JAVA_HOME = $arJdk.FullName
$arSdk = Join-Path $arTools 'sdk'
$arSdkManager = Join-Path $arSdk 'cmdline-tools/bin/sdkmanager.bat'
& $arSdkManager "--sdk_root=$arSdk" --licenses
& $arSdkManager "--sdk_root=$arSdk" 'platform-tools' 'platforms;android-35' 'build-tools;35.0.0'
```

Leer y aceptar las licencias aplicables. Después, exportar los resultados, verificar los datos y construir la APK.

```powershell
$env:PATH = "$(Split-Path -Parent $projectPython);$env:PATH"
& $projectPython .\Edificio\visualization\android-ar\tools\export_ar_data.py
& $projectPython .\Edificio\visualization\android-ar\tools\verify_ar_data.py
powershell -NoProfile -ExecutionPolicy Bypass -File .\Edificio\visualization\android-ar\build-apk.ps1 -Honors
```

La opción de ejecución de PowerShell se aplica a ese proceso. El script construye, ejecuta lint y pruebas Java, comprueba firma y verifica la correspondencia entre APK, instantánea y marcadores fuente.

La salida se encuentra en `Edificio/visualization/android-ar/dist/EdificioAR-Honors.apk`, con `delivery_honors.json`. Sin `-Honors`, se generan `EdificioAR.apk` y `delivery.json`. Ambas salidas se construyen desde el código actual, por lo que el nombre de una APK existente no basta para identificar su contenido.

Copiar la APK al teléfono e instalarla, permitiendo la instalación desde la aplicación utilizada si Android lo solicita. Se requiere Android 7 o superior, ARM64, dispositivo compatible con ARCore, Google Play Services for AR y permiso de cámara. La firma es de desarrollo para instalación directa.

Abrir [`imprimir.html`](Edificio/visualization/android-ar/markers/imprimir.html), seleccionar IDs e imprimir al 100 %. Verificar que la imagen completa mida 20 × 20 cm. Activar esos IDs en la aplicación y comparar identificación, coordenadas, caso y valores con el PC. La prueba AR requiere detectar el marcador y comprobar su colocación física.

Cuando está disponible `arcoreimg`, el verificador evalúa la calidad de imágenes. Si el reporte indica `PENDING`, revisar la causa y completar esa evaluación. Compilar no demuestra precisión de alineamiento. Más detalles en el [README de AR](Edificio/visualization/android-ar/README.md).

### VR móvil CampusCardboard

1. En Unity Hub, agregar Android Build Support, Android SDK & NDK Tools y OpenJDK a la instalación **6000.5.10f1**.
2. Abrir `Edificio/visualization/unity/CampusCardboard` y esperar la restauración del plugin y paquetes. Git debe estar disponible para descargar la dependencia fijada.
3. Detener Play y ejecutar `Campus > Cardboard > Configurar Android`. Reiniciar si se solicita. Se configura ARM64, IL2CPP, Android mínimo 8.0, API 26, y destino API 35.
4. Ejecutar `Campus > Cardboard > Construir APK`. Esperar importaciones y comprobar el resultado en Console. Si el cambio de plataforma lo requiere, repetir la construcción tras finalizar la importación.
5. Recuperar `Edificio/visualization/unity/CampusCardboard/Build/Android/CampusCardboard.apk` y `delivery.json`.
6. Instalar, configurar el perfil QR del visor y comprobar estéreo, orientación, recentrado, selección por mirada y locomoción.

La APK AR corresponde a otra aplicación. El preview de PC permite revisar interacción, pero las pruebas de ambos ojos, orientación y rendimiento requieren teléfono y visor. Estas instrucciones no afirman una nueva construcción o prueba física durante esta revisión documental.

## 7. Ejecutar tests y verificaciones

### Suite Python

Después de preparar el entorno y los resultados, ejecutar desde la raíz.

```powershell
& $projectPython -m unittest discover -s .\Edificio\verification\tests -p 'test_*.py' -v
```

La suite revisa sismo, capacidad, postproceso, benchmarks de vigas, carga móvil, contratos y backend. Esperar el cierre y comprobar `OK`, revisando también fallos, errores y pruebas omitidas. El número depende de la revisión del código y no debe asumirse a partir de un reporte anterior.

### Verificadores estructurales adicionales

```powershell
& $projectPython .\Edificio\verification\load_transfer\verificar_reparto_combinacion.py
& $projectPython .\Edificio\verification\capacity\verificar_pm_excel.py
& $projectPython .\Edificio\verification\interactive\verificar_semana05.py
```

Revisan el reparto combinado, contrastan la formulación P-M con la planilla de referencia usando entradas compatibles y comprueban superposición frente a soluciones explícitas. Interpretar sus resultados junto con los controles globales.

Para comprobar variantes de carga y sección, ejecutar el siguiente verificador. Regenera resultados y recursos Unity durante la prueba e incluye restauración de la base. Detener Play y revisar la salida final antes de distribuir archivos.

```powershell
& $projectPython .\Edificio\verification\interactive\verificar_modificaciones.py
```

Si cambió el contrato o las bases SQ4 quedaron asociadas a una geometría anterior, regenerar con `--carga-movil`. El detalle está en [`SEMANA5_VARIANTES.md`](Edificio/documentation/SEMANA5_VARIANTES.md).

### Integración real entre Unity y backend

Cerrar CampusCardboard para que la prueba pueda abrir ese proyecto con otro proceso del editor. Indicar la ruta real de Unity instalado.

```powershell
$unityEditor = 'C:/Program Files/Unity/Hub/Editor/6000.5.10f1/Editor/Unity.exe'
& $projectPython .\Edificio\verification\interactive\verify_unity_backend.py --unity $unityEditor
& $projectPython .\Edificio\verification\interactive\audit_honors_base.py
```

El primero inicia un servidor local temporal y CampusCardboard automatizado y comprueba solicitudes HTTP de reanálisis y capacidad. Requiere una licencia que permita iniciar Unity. Imprime dónde quedaron sus evidencias y cierra su servidor al terminar.

El segundo reconstruye una base en un trabajo aislado y compara desplazamientos, giros y fuerzas locales con los resultados guardados. No sustituye los originales. Si informa `REVISAR`, consultar diferencias y versiones registradas.

En el editor también puede ejecutarse `Campus > Cardboard > Probar interacción en Play`. Revisar `CAMPUS_VR_CHECK_COMPLETE passed=True` en Console. Los [registros históricos](Edificio/documentation/honors_evidence/README.md) documentan pruebas anteriores y no equivalen a una nueva ejecución.

### Android y dispositivo

`build-apk.ps1` incluye `assembleDebug`, `lintDebug` y `testDebugUnitTest`. En Android Studio puede abrirse `Edificio/visualization/android-ar` con JDK 17 y ejecutar esas tareas en el módulo `app`. Consultar salida de Gradle y el reporte de entrega del script.

Completar las pruebas automáticas en el teléfono con detección, comparación de valores, medición del alineamiento AR, pausa y reanudación, conexión al PC y funcionamiento VR. Registrar dispositivo, condiciones, versión del modelo y medidas. Compilación y preview no acreditan esas comprobaciones físicas.

## 8. Benchmark independiente de Semana 1

P1L1 tiene dependencias y resultados propios. Utilizar otro entorno para no modificar las versiones del edificio.

```powershell
py -3.12 -m venv .venv-p1l1
$benchmarkPython = Join-Path (Get-Location) '.venv-p1l1/Scripts/python.exe'
& $benchmarkPython -m pip install -r .\P1L1\requirements.txt
& $benchmarkPython .\P1L1\model.py
& $benchmarkPython .\P1L1\plot_model.py
```

Revisar `P1L1/results/verification.md` y `verification.json`, prestando atención a cada `REVISAR`. El modelo regenera `P1L1/results/model.json` y la figura se guarda en `P1L1/results/geometry.png`.

Agregar `P1L1/UnityProject` en Unity Hub con Unity 6000.5.10f1 y abrir `Assets/Scenes/Frame3D.unity`. Este visor utiliza el contrato de Semana 1 y permanece separado del edificio global.

## 9. Salidas de aplicaciones y reportes de verificación

Las rutas siguientes parten de la raíz del repositorio. Las salidas esperadas describen lo que produce una construcción correcta y no implican que exista un build actualizado para cada revisión del código.

| Aplicación | Salida de construcción | Reporte o evidencia que se debe revisar |
| --- | --- | --- |
| UnityVisualization para Windows | `Edificio/visualization/unity/UnityVisualization/Build/EdificioViewer.exe` y toda la carpeta `Build/`. | Console o log del editor con `BUILD COMPLETADO`. Para la corrida visualizada, revisar también `Edificio/results/manifest.json` y los controles globales. El constructor no genera un manifiesto de entrega propio. |
| CampusPlayable para Windows | `Edificio/visualization/unity/CampusPlayable/Build/Windows/CampusIngenieria.exe` y toda la carpeta `Build/Windows/`. | Console o log del editor con `CAMPUS_BUILD_RESULT Succeeded`. Completar una prueba de recorrido del ejecutable construido. |
| AR Android Honors | `Edificio/visualization/android-ar/dist/EdificioAR-Honors.apk`. | `Edificio/visualization/android-ar/dist/delivery_honors.json`, con hashes, comprobación de recursos, firma y estados pendientes. |
| AR Android, salida sin opción Honors | `Edificio/visualization/android-ar/dist/EdificioAR.apk`. | `Edificio/visualization/android-ar/dist/delivery.json`. El comando sin `-Honors` selecciona estos nombres de salida. |
| CampusCardboard Android | `Edificio/visualization/unity/CampusCardboard/Build/Android/CampusCardboard.apk`. | `delivery.json` en la misma carpeta, con versión Unity, commit, hashes y estado de prueba física. Se escribe tras una construcción exitosa. |

Conservar junto a la entrega los reportes que corresponden al artefacto distribuido. Los hashes permiten comprobar su identidad, mientras que los logs de construcción y las pruebas de uso documentan aspectos distintos. Regenerar y volver a compilar cuando cambien los datos que deben incorporar las aplicaciones.

## 10. Estado de validación documentado

Esta tabla distingue los registros disponibles de las comprobaciones que deben completarse para una nueva entrega. La actualización de este README no ejecuta nuevamente las pruebas ni certifica aplicaciones recién construidas.

| Componente | Evidencia disponible | Alcance y comprobación pendiente |
| --- | --- | --- |
| Modelo global y carga móvil | Resúmenes y controles en `Edificio/results/`, junto con `manifest.json`. | Revisar sus estados y correspondencia con las entradas después de regenerar. El equilibrio numérico no sustituye la revisión de las hipótesis estructurales. |
| Suite Python | [Registro de auditoría con 55 pruebas aprobadas](Edificio/documentation/honors_evidence/auditoria_2026_10_07/main_9e332b1/python-tests.txt). | Resultado de la revisión identificada en esa carpeta. Repetir la suite sobre el commit definitivo. |
| Interacción Unity y comunicación con backend | [Comprobaciones de auditoría Unity](Edificio/documentation/honors_evidence/auditoria_2026_10_07/main_9e332b1/unity_checks.json) y [guía de evidencias de software](Edificio/documentation/honors_evidence/README.md). | Distinguir los controles de editor de las pruebas HTTP documentadas. Repetir la integración si cambia cliente, backend o contrato. |
| Comparación numérica independiente | `Edificio/documentation/honors_evidence/direct_comparison.json` y `baseline_audit.json`. | Corresponden a las bases y versiones identificadas en esos registros. Repetir la auditoría para comprobar una base nueva. |
| AR Android | APK y reporte `Edificio/visualization/android-ar/dist/delivery_honors.json`, además del [registro de pruebas Java](Edificio/documentation/honors_evidence/auditoria_2026_10_07/main_9e332b1/android-tests.xml). | El reporte de entrega conserva estados `PENDING` de calidad de marcadores, prueba física y registro en terreno. La prueba funcional comunicada por el equipo no reemplaza medidas de alineamiento y deriva. |
| VR Cardboard | Código, preview y verificadores de interacción del proyecto. | No se encuentra una APK en la ruta de salida indicada. Construirla y comprobar en teléfono estéreo, orientación, selección, locomoción y rendimiento. |
| Recorrido PC | Constructor Windows y registros de revisiones previas. | Probar el ejecutable que se distribuya, incluidos controles, cambios de cámara y acceso a pisos. Un ZIP anterior puede contener otro estado del proyecto. |

Para las comprobaciones físicas, registrar dispositivo, versión de la aplicación, identidad del modelo, condiciones y resultados medidos. Actualizar los estados cuando exista evidencia y conservar los registros anteriores como historial.
