# Semana 5: controles, variantes y alcance de reanálisis

La escena del viewer es `Edificio/visualization/unity/UnityVisualization/Assets/Main.unity` (Unity 6000.5.10f1). Ejecutar estos comandos desde la raíz del checkout con los paquetes de `Edificio/requirements.txt` instalados.

## Verificaciones reproducibles

```powershell
python Edificio/verification/interactive/verificar_semana05.py
python Edificio/verification/interactive/verificar_modificaciones.py
```

`verificar_semana05.py` contrasta superposición con soluciones OpenSees explícitas para S1=(1,1,0,0), S2=(1.2,1.4,0.8,-0.3) y S3=(1,0.5,-1,0.7). Compara traslaciones [m], rotaciones [rad], acciones locales de extremo separadas en fuerza [kN] y momento [kN·m], diagramas por estación y demandas de muros P [kN] y M [kN·m]. Repite con αG=0.8, αQ=0.25. La tolerancia relativa por magnitud/componente es 1e-5; el detalle se escribe en `Edificio/documentation/semana05_evidencias/superposicion_verificacion.json`.

`verificar_modificaciones.py` guarda dos entradas de variante: una intensidad de carga viva uniforme de 4 kN/m² y una sección rectangular de la viga ID 207 de 0.80×0.60 m a 0.75×0.45 m. Regenera el modelo, ejecuta casos y exporta Unity; compara R antes/después en desplazamiento y en Vzi/Myi de la misma viga; luego reconstruye y analiza el estado base. Compara la respuesta y SHA-256 de los recursos antes y después de restaurar. El flujo se ejecutó en una copia temporal completa del checkout para proteger resultados locales: restauración exacta confirmada. Detalle y entrada: `Edificio/documentation/semana05_evidencias/modificaciones_ejecucion.json` y `mod_seccion_viga_207_geometria.json`.

| Caso R | |uz|max [m] | Vzi, viga 207 [kN] | Myi, viga 207 [kN·m] |
|---|---:|---:|---:|---:|
| Base | 0.0336096580 | 348.879910 | −745.476764 |
| Q = 4 kN/m² | 0.0328676641 | 336.593528 | −711.709899 |
| Sección 207 = 0.75×0.45 m | 0.0336097043 | 340.259958 | −694.973953 |

## Controles del viewer

Los cuatro deslizadores λG, λQ, λEX y λEY actualizan R al arrastrar; los campos aceptan coma o punto para introducir valores precisos. G/Q muestran rango 0–2 y EX/EY −2–2. Si el visor estaba en otro caso, el cambio selecciona R. La deformada y los resultados se forman a partir de las bases compatibles cargadas; no se ejecuta OpenSees desde Unity.

El gráfico de interacción del muro actualiza el punto cuando cambia P/M aunque el nombre permanezca R. Se combinan demandas firmadas y luego se compara |M| con la envolvente. La lectura de capacidad es nominal y uniaxial; no es una comprobación normativa completa.

## Modificaciones y reanálisis

| Cambio | Reanálisis |
|---|---|
| λ sobre bases del mismo modelo lineal | No; superposición de resultados compatibles. |
| αG/αQ en masa | Puede superponerse con EXG/EXQ/EYG/EYQ si representan exactamente esa formulación; es análisis pseudoestático y no respuesta dinámica. |
| Sección, rigidez de material, apoyos o activación estructural | Sí; regenerar geometría, análisis y resultados afectados. |
| Patrón de carga o área tributaria | Sí, salvo que se demuestre que bases compatibles representan exactamente el cambio. |
| Resistencia sin cambio de rigidez | Recalcular capacidad; si cambia rigidez, reanalizar también demanda. |
| Ocultar una capa o amplificar una deformada | No modifica el modelo ni la carga física. |

El flujo de sección acepta `element_section_overrides` en un JSON de geometría. El ID es el del elemento analítico generado, no un tamaño cosmético: el override actualiza A, Iy, Iz, J, el peso propio G, la masa sísmica, el elemento `elasticBeamColumn` y la geometría que Unity dibuja. Los valores fuente y derivados se registran en el JSON de ejecución. No hay capacidad de viga exportada por esta variante, por lo que no se debe inferir una verificación P–M de viga.

`semana5_modelo_hash.txt` identifica los recursos exportados. SQ4 incluye el hash del modelo y Unity lo bloquea cuando no coincide. Las 312 bases se regeneraron para el modelo vigente (652 paneles, 10 686 controles); su hash ahora coincide con `modelo_3d_manual.json`. Después de una modificación estructural, regenerar SQ4 con `python Edificio/analysis/load_cases/carga_movil.py` antes de usarlo. G/Q no cambian por modificar solamente αG/αQ; EX/EY sí se reconstruyen desde sus bases de masa.

## Estado comprobado en esta revisión

| Requisito | Estado |
|---|---|
| Sliders λ con actualización inmediata y restauración | Implementado en código; falta prueba visual en Unity. |
| Coherencia de masa, fuerzas, diagramas y demanda P–M | Verificada numéricamente; 120 errores por componente bajo 1e-5. |
| Invalidez del gráfico P–M para demanda/capacidad actuales | Implementada; falta comprobar punto y texto en ejecución visual. |
| Segunda categoría: sección real de viga | Ejecutada en copia temporal; viga 207 cambió A/I/J y esfuerzos, y la restauración de recursos/respuesta fue exacta. |
| SQ4 | 312 bases regeneradas; 652 paneles y 10 686 controles aprobados, hash coincidente; 29 pruebas unitarias pasan. Falta recorrido visual Unity. |
| Navegación, selección y restauración de capas en Unity | No probadas en esta revisión. |

La prueba de superposición escribió `superposicion_verificacion.json`. Las 29 pruebas unitarias pasaron después de regenerar SQ4. No se afirma compilación de Unity ni prueba visual de sliders, selección, capas o SQ4 en esta revisión. El editor batch de Unity 6000.5.9f1 terminó durante el inicio/licencia, sin un registro de compilación.
