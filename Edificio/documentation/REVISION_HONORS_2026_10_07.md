# Revisión de Honors — 7 de octubre de 2026

Requisitos contrastados con `Enunciados e Instrucciones/P1_Honors_Track.txt`. Revisión sobre main `9e332b1` más cambios locales conservados. **Todavía no corresponde afirmar que los cinco Honors estén completos.** Solo está disponible el Redmi; no hay visor Cardboard para una comprobación física.

| Honor | Evidencia y alcance | Pendiente para demostrar todo lo solicitado |
|---|---|---|
| H1 — Cardboard | Implementados estéreo, orientación, locomoción, selección, resultados y diagramas/capacidad. Validación desde Unity en PC aprobada. | Falta Android Build Support para Unity 6000.5.10f1, construir el APK y probarlo con visor. La simulación en PC no demuestra estéreo ni seguimiento en el teléfono. |
| H2 — AR avanzada | Registro común de sector con varios marcadores, consenso, anclaje y medición/exportación del error; pruebas Java aprobadas. | Elegir y medir el sector, comprobar alineación y robustez, medir error y deriva en Redmi. Solo persiste la configuración: el anclaje requiere detectar marcadores tras reiniciar; no hay persistencia autónoma del anclaje. La calidad de las imágenes no está medida con arcoreimg. |
| H3 — AR estructural | Diagramas, hasta cuatro barras, deformada Hermite amplificada, movimientos nodales de muros, áreas tributarias y P–Mz; datos exportados y APK verificados. Corregido el signo negativo de Mz en el gráfico AR. | Demostrar sobre elementos físicos reales con el Redmi. La deformada Hermite interpola DOF nodales; no es la flecha interior exacta bajo carga distribuida. Las losas se representan mediante reparto de cargas. |
| H4 — Reanálisis en vivo | Unity → backend Python/OpenSees → resultados, validación y manejo de errores. Prueba real HTTP aprobada; comparación independiente aprobada para G/Q/EX/EY/R, fuerzas locales y diagramas por estación. | Demostración en la red y dispositivo que se usarán en la evaluación. |
| H5 — Capacidad avanzada | Cambio de refuerzo y regeneración de interacción, una de las alternativas del enunciado. Regeneración comprobada por HTTP y pruebas numéricas. | Demostrar el cambio y explicar las hipótesis. Son curvas nominales uniaxiales de sección: no verifican interacción biaxial, segundo orden ni capacidad normativa del miembro. Cambiar refuerzo no cambia automáticamente la rigidez global. |

## Modelo actualizado y verificaciones

El modelo incluye 1.253 nodos, 616 barras y 654 losas. El análisis global terminó en OK; se comprobó conservación G/Q y superposición. La capacidad contrastada con la planilla reproduce sus entradas, lo cual no valida por sí solo la armadura activa del edificio. La carga móvil aprobó 10.722 controles sobre 654 paneles. Unity aprobó 33 controles; Android aprobó 6 pruebas, compilación, lint y firma del APK Honors.

La evidencia de esta versión está en `honors_evidence/auditoria_2026_10_07/main_9e332b1/`. Los archivos del directorio padre corresponden a la geometría anterior y no se deben presentar como verificación del modelo nuevo. El APK actualizado es `visualization/android-ar/dist/EdificioAR-Honors.apk`; no se instaló ni probó físicamente en el Redmi durante esta revisión.

## Orden práctico para cerrar la entrega

1. Usar el APK Honors actualizado y verificar AR básica en el Redmi.
2. Elegir un sector accesible, confirmar IDs y coordenadas, medir tres marcadores y puntos independientes; registrar errores y una prueba de deriva.
3. Grabar la consulta de varios elementos, deformada, áreas y P–Mz sobre la estructura real.
4. Demostrar reanálisis y cambio de refuerzo con el backend en la misma Wi-Fi.
5. Para H1, instalar el módulo Android del editor, construir y probar con un visor. Para H2, resolver expresamente el requisito de persistencia: guardar configuración no lo cumple por sí solo.

No se asigna puntaje estimado: corresponde a los evaluadores y exige evidencia funcional, incluida la AR básica del núcleo obligatorio.
