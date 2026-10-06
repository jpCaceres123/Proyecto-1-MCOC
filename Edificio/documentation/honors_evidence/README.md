# Evidencia de software — 2026-10-06

No sustituye pruebas en teléfono o terreno.

- Python: **51 tests aprobados** (incluye controles existentes de transferencia, equilibrio de cortes, masas, ejes, capacidad y variantes).
- Unity 6000.5.9f1: **26 comprobaciones aprobadas**: 24 de preview/control/diagramas y 2 de HTTP real contra OpenSees/capacidad. Preview explícitamente no nativo. Pasos de 2 m y rechazo de obstáculo/vacío probados con colliders aislados.
- Android: `assembleDebug lintDebug testDebugUnitTest` exitosos; **5 pruebas Java aprobadas**, 0 errores de lint. Existen advertencias de orientación/códigos deprecados; no se afirma compatibilidad futura Gradle 10.
- APK AR Honors: firma v2 comprobada con apksigner, todas las imágenes/instantánea y geometría de overlays comparadas byte a byte con fuentes. Calidad de detección de marcadores y prueba física pendientes. Ver `dist/delivery_honors.json`.
- Backend: dos trabajos reales por HTTP, viga 207 Q adicional 2 kN/m y columna 1 con cinco barras por cara Ø32 mm. Corrida final con entorno fijado: UUIDs `00dad9c2-18ea-42bc-a209-9f7feeb69e0d` y `78fecdd4-1aef-46d7-aacf-2f10284619c4`. Servidor de prueba cerrado al terminar. Configuración/token temporal no se incorpora al repositorio.
- Comparación independiente: corrida explícita en carpeta distinta, cinco casos G/Q/EX/EY/R, desplazamientos/rotaciones, acciones locales y estaciones. Tolerancia relativa 1e−5; absolutos 1e−8 m, 1e−9 rad, 1e−5 kN/kN·m. Ver `direct_comparison.json`.
- Publicación final del contrato: 23 archivos, incluyendo manifiesto de análisis y controles globales; controles `REVISAR` impiden publicación. Originales no sobrescritos.
- Capacidad editable: rectángulo/áreas, barras no solapadas, refinamiento de fibras y estado contrastado con sección OpenSees independiente. No es capacidad biaxial ni normativa.
- Auditoría de base histórica: diferencia EX/693 encontrada en OpenSees 3.7/NumPy 2.4; **resuelta al reproducir 3.8/2.5**, sin ampliar tolerancias. Informe `baseline_audit.json`: cinco casos, ninguna violación, hashes de entradas verificados, cargas regeneradas idénticas. Dependencias fijadas y servidor de consola protegido contra versiones diferentes.

Logs completos locales: `%TEMP%/MCOC-unity-http-0icboxlq/unity.log`; Android en `app/build/reports/` y `app/build/test-results/`. Los scripts permiten repetir las pruebas sin depender de esos temporales.

Pendientes de aceptación: módulo Android de Unity .9 e instalación autorizada; APK y recorrido real Cardboard; levantamiento de sector/tres marcadores; tres registros AR y 60 s de deriva; pruebas Wi-Fi en Redmi; grabaciones y aprobación docente de relocalización en lugar de persistencia autónoma.
