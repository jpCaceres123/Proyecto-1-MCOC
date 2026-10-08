# H1–H5: implementación y validación

Guía unificada de aplicaciones, controles, APKs, backend y presentación: [GUIA_EJECUCION_Y_ENTREGA.md](GUIA_EJECUCION_Y_ENTREGA.md).

Estado de entrega: funcionalidades implementadas y pruebas de software realizadas; **los cinco honors todavía no están certificados en terreno**. No se han inventado medidas, FPS del Redmi, videos ni persistencia autónoma de anclajes.

## Estado y límites

| Etapa | Implementado | Verificación automática | Prueba física pendiente |
|---|---|---|---|
| Datos | Contrato v1, entregas inmutables con SHA256 y conservación de IDs | Contrato, equilibrio/reparto existentes y tres benchmarks de vigas | Correspondencia ID físico/modelo |
| H1 | Cardboard separado, mirada de 2 s, avanzar/retroceder, paso de 2 m o movimiento continuo, diagramas y capacidad junto a columna, registro de FPS | Pruebas de editor y conexión HTTP real | APK Cardboard, estéreo, QR del visor, seguimiento, comodidad y recorrido de 5 min en Redmi |
| H2 | Sector métrico medido, consenso de marcadores, anclaje común, rechazo de discrepancias, configuración guardada y reporte de errores | Validaciones del manifiesto y pruebas Java | Levantamiento de tres marcadores, tres registros independientes y deriva durante 60 s |
| H3 | Hasta cuatro barras, esfuerzos, Hermite ×100, muros con desplazamientos nodales, áreas translúcidas y P–Mz cercano a columna | Exportación/compilación Android y compatibilidad de hashes | Alineamiento con estructura real y video de selección/consulta |
| H4 | Backend LAN protegido, cola serial, cancelación, tiempo máximo, revisiones descargadas y verificadas en ambos clientes Unity y Android | Trabajo real OpenSees, comparación directa e integración Unity por HTTP | Solicitud desde Redmi en Wi-Fi y manejo de pérdida de conexión en dispositivo |
| H5 | Refuerzo editable, regeneración nominal P–Mz/P–My, comparación anterior/nueva y demanda compatible | Áreas, geometría, refinamiento de fibras y contraste independiente OpenSees | Demostración de interfaz y explicación de hipótesis |

El backend no modifica apoyos ni ejes locales. Las losas continúan representadas por cargas tributarias: las superficies AR no son placas analizadas. Un momento no se fuerza a ser parabólico: con carga uniforme es cuadrático; con una carga puntual es lineal por tramos. La deformada AR es interpolación Hermite de DOF nodales, **no una flecha interior exacta bajo carga distribuida**.

## Abrir las aplicaciones

- PC: mantener `visualization/unity/CampusPlayable` separado y sin sobrescribir su entrega anterior.
- Visor técnico: `visualization/unity/UnityVisualization`.
- Cardboard: abrir `visualization/unity/CampusCardboard` con Unity **6000.5.10f1**. Entrar al modo Cardboard; usar mirada 2 s y el botón de modo de locomoción. El movimiento se detiene al apartar la mirada. `PC Wi-Fi` permite solicitar Q adicional o capacidad de columna.
- AR: instalar `visualization/android-ar/dist/EdificioAR-Honors.apk`; aplicación `cl.mcoc.edificio.ar`. Es una APK de depuración para pruebas, no publicación en tienda.

Cardboard: menú compacto solo de botones. `Diagrama` y `Backend` sustituyen al menú principal; `Volver` restaura los controles de movimiento. `IZQUIERDA`/`DERECHA` desplazan lateralmente con la misma mirada de 2 s y el mismo modo continuo/por pasos. Los valores, caso, unidades y amplificación permanecen junto al diagrama sobre la barra; el botón `i / x / j >` mueve la estación y su valor amarillo. En preview PC, botón derecho para mirar y `M` para recentrar.

### Bloqueo actual de APK Cardboard

Falta Android Build Support (SDK/NDK/OpenJDK) de **6000.5.10f1**. El editor instalado en este PC no incluye ese módulo. En Unity Hub: Installations → 6000.5.10f1 → Add modules → Android Build Support, SDK/NDK y OpenJDK; aceptar la autorización de Windows. Luego menú `Campus > Cardboard > Construir APK`. Salida: `Build/Android/CampusCardboard.apk` y `delivery.json` con hashes/commit. No cambiar de editor ni licencia para conseguirlo.

Referencia de configuración: [Google Cardboard Unity](https://developers.google.com/cardboard/develop/unity/quickstart). Head tracking aquí significa orientación, no seguimiento posicional 6 DOF.

## Backend PC / misma Wi-Fi

Desde la raíz del repositorio:

```powershell
ipconfig
./Edificio/analysis/backend/start-lan.ps1 -Address 192.168.1.20
```

Reemplazar la IP de ejemplo por la IPv4 privada real del PC. La consola muestra la URL y un token temporal: copiarlos en `PC Wi-Fi` en el teléfono. No guardar el token en GitHub. El helper elimina su variable al cerrar. El servicio no se publica en internet; usa HTTP con token **solo en red local de confianza**, no cifrado TLS. Si Windows pregunta por el firewall, permitir únicamente la red privada según las normas de la institución; el programa no cambia el firewall.

Usar Python 3.12 y las dependencias fijadas en `requirements_honors.txt`, preferentemente en un entorno virtual. Si `python` apunta a otro entorno, especificar `-PythonInterpreter 'ruta/al/python.exe'` en el helper. No mezclar silenciosamente OpenSees 3.7/NumPy 2.4 con la base histórica 3.8/2.5: una auditoría estricta detectó 1.66e−5 kN de diferencia en el elemento 693 bajo EX. No se amplió la tolerancia ni se cambiaron originales para aprobarla.

**Resuelto:** la auditoría con OpenSees 3.8.0.0/NumPy 2.5.2 aprobó los cinco casos sin diferencias de cargas ni desplazamientos y sin acciones fuera de tolerancia. `setup-environment.ps1 -PythonInterpreter 'ruta/al/python312.exe'` instala esas dependencias en `%USERPROFILE%/.mcoc-honors/Python312`; `start-lan.ps1` lo usa automáticamente. El servidor lanzado desde consola rechaza otras versiones numéricas. No afecta otros Python instalados.

En este PC el entorno ya está preparado. En otro equipo, ejecutar primero el instalador de entorno con un Python 3.12 existente. Cardboard registra FPS y, cuando Android la expone, temperatura de batería (no temperatura del procesador) durante cinco minutos; no se incluyen valores inventados del Redmi.

Endpoints autenticados: `GET /health`, `POST /jobs`, `GET /jobs/{id}`, `DELETE /jobs/{id}`, `GET /jobs/{id}/delivery`, `GET /jobs/{id}/files/{name}`. Los trabajos se guardan en `%LOCALAPPDATA%/MCOCHonors/Jobs`. Cada UUID tiene carpeta, solicitud y registro independientes; cola limitada, un proceso activo y timeout de diez minutos. Tras reiniciar, trabajos interrumpidos se marcan fallidos; no se presentan como completados.

Las variantes parten siempre del modelo base, no acumulan solicitudes anteriores. Unity técnico acepta cargas/secciones dentro de límites documentados en `server.py`; Cardboard/AR ofrecen Q adicional en la barra seleccionada. Resultado fallido, incompleto, incompatible o con hash distinto no sustituye la revisión válida. En AR un modelo variante invalida el sector vinculado al hash anterior: regresar al modelo base para usar ese levantamiento; no reutilizar un registro incompatible.

## Preparar H2 en terreno

1. Elegir una crujía accesible. Confirmar **coordenadas e IDs**, no usar automáticamente los ejemplos históricos como sector correcto.
2. Generar formulario con tres IDs distintos y cercanos:

```powershell
python Edificio/visualization/android-ar/tools/sector_template.py --ids '1,241,246' --output sector-medido.json
```

Los IDs son solo ejemplo; el generador valida la distancia y puede rechazarlos. No inventa posiciones: entrega `surveyed:false` y poses sin completar. Guardar el formulario fuera de resultados originales.

3. Medir el centro de cada imagen de **20 × 20 cm**, su orientación y relación con coordenadas OpenSees. Completar `position:[X,Y,Z]` en metros y `quaternion:[x,y,z,w]` unitario del sistema local de la imagen hacia el modelo. Ejes locales de la imagen ARCore: +X derecha, +Y normal saliente y +Z hacia abajo de la imagen. Verificar esta orientación con una prueba real antes de aceptar el registro. Registrar método/instrumento/incertidumbre del levantamiento; luego establecer `surveyed:true`.
4. `origin` es un punto común del modelo. Todos los extremos incluidos deben estar a ≤8 m; no escalar para encajar una geometría discordante. `modelHash` debe coincidir exactamente con la instantánea activa.
5. Copiar el JSON al Redmi e importarlo con `Sector JSON`. Se activan automáticamente sus imágenes. Mostrar al menos dos marcadores compatibles para crear el anclaje tras estabilizarse. Los tres contribuyen cuando están visibles. Una discrepancia exige revisar el levantamiento y reanclar; no se salta automáticamente a otro marcador.
6. Seleccionar en `Catálogo` sin perder el registro, usar `Añadir/quitar` (máximo cuatro), componente/caso/estación, `Deformada`, `Áreas` y `P–Mz`. `Salir del sector medido` vuelve al marcador individual y elimina solo la calibración guardada de la aplicación.
7. Medir puntos independientes ≥25 cm del centro de los marcadores. Introducir etiqueta y X,Y,Z; apuntar el centro de cámara al punto real y usar `Medir error`. Solo se acepta un hit ARCore válido. El error también incluye la incertidumbre de esa medición: no equivale a un levantamiento topográfico preciso.
8. `Exportar` genera reporte en archivos externos de la app. Objetivos propios: RMS ≤5 cm y máximo ≤10 cm. Hacer tres registros, repetir controles después de ocultar los marcadores durante 60 s, exportar y grabar. **Exportar antes de Reanclar**: un nuevo registro limpia las mediciones de la sesión anterior para no mezclar sus errores. Un reporte sin controles tiene RMS/máximo nulos, no cero.

Al reiniciar se guarda **configuración**, no el anchor del mundo AR. Se debe detectar nuevamente el marcador y recuperar la transformación. Esto no es persistencia autónoma ni Cloud Anchor. Confirmar aceptación docente del requisito condicional. El anclaje común y la distancia limitada siguen la [guía oficial ARCore](https://developers.google.com/ar/develop/anchors).

## H5: alcance de capacidad

En el visor técnico seleccionar una columna HA compatible: el panel de refuerzo permite número de barras por cara y diámetro; `Recalcular` solicita curvas nuevas. Cardboard ofrece diámetros 28/32/36 mm con cinco barras por cara; AR un ejemplo Ø32. La cantidad total se calcula sin duplicar barras de esquina. Refuerzo/materiales son hipótesis académicas de `data/parameters/parametros.json`, salvo documentación expresa.

La sección debe pertenecer al modelo base; no asignar la curva de referencia a una sección modificada. Se valida recubrimiento, barras no solapadas y áreas. Se exportan envolventes uniaxiales muestreadas del modelo constitutivo, sin factores normativos. P–Mz usa Mz, P–My usa My; **no** se sustituye por √(My²+Mz²). No constituye verificación biaxial, capacidad de miembro ni cálculo de segundo orden. Cambiar refuerzo no cambia automáticamente EI en el modelo global lineal-elástico; cada revisión lo declara.

## Reproducir pruebas

```powershell
$honorsPython=Join-Path $env:USERPROFILE '.mcoc-honors/Python312/Scripts/python.exe'
& $honorsPython -m unittest discover -s Edificio/verification/tests -v
& $honorsPython Edificio/verification/interactive/verify_unity_backend.py --unity 'C:/Program Files/Unity/Hub/Editor/6000.5.10f1/Editor/Unity.exe'
```

La segunda prueba abre Unity en batch, lanza un servidor aislado de loopback y ejecuta dos trabajos reales; cierra su servidor al terminar. No demuestra Wi-Fi, estéreo ni AR físico. La comparación independiente `verify_backend_direct.py --help` ejecuta una variante en un árbol temporal distinto y contrasta desplazamientos/acciones/diagramas de G,Q,EX,EY,R (rtol 1e−5; absolutos por unidad).

Android: `assembleDebug lintDebug testDebugUnitTest`; verificar firma con `apksigner verify --verbose` y datos con `tools/verify_apk.py --honors --apk dist/EdificioAR-Honors.apk --report dist/delivery_honors.json`. Requiere SDK/build-tools disponibles; no editar el SDK original para corregir un lint.

Para reconstruir con las herramientas del proyecto: instalar mediante SDK Manager `platforms;android-35`, `build-tools;35.0.0` o superior y `platform-tools`, revisar sus licencias, y usar `build-apk.ps1 -Honors`. Esa opción conserva la APK anterior. La ejecución verificada utilizó OpenJDK de Unity 6000.3.22f1 y un SDK Android temporal aislado; **no** cambió el editor Cardboard de 6000.5.10f1.

`& $honorsPython Edificio/verification/interactive/audit_honors_base.py` crea una corrida base nueva, sin cambios, en una carpeta temporal. Comprueba hashes de sus entradas y equivalencia numérica con la instantánea histórica. El manifiesto histórico permanece intacto; una modificación de código no se oculta actualizando su hash a mano.

Cerrar la entrega solo con APKs identificadas, logs, entradas, reportes, medidas y videos. Mantener separados **implementado**, **verificado automáticamente** y **probado físicamente**; no sustituir evidencia de teléfono por editor.
