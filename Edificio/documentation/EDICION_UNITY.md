# Carga, sección y deformada por elemento — Editor y Windows

## Uso

1. Instalar Python y las dependencias desde la raíz: `python -m pip install -r Edificio/requirements.txt`.
2. Abrir `Edificio/visualization/unity/UnityVisualization` con Unity 6000.5.10f1, escena `Assets/Main.unity`, y entrar en Play. Para Windows, generar un **nuevo build** con esa escena incluida: los ejecutables anteriores no contienen estos controles.
3. Seleccionar un elemento por clic o ID. En el inspector, abrir **Configurar carga / sección**.
4. Marcar la propiedad que se quiere modificar, introducir valores y pulsar **Aplicar elemento y reanalizar**.
5. La primera vez, comprobar la raíz del repositorio (carpeta que contiene `Edificio`) y el ejecutable `python` o la ruta absoluta al `python.exe` del entorno instalado. El ejecutable Windows necesita también este repositorio y Python en el computador; no incluye OpenSees embebido.
6. Esperar el cálculo. La escena se recarga automáticamente con los recursos exportados solo si el proceso y sus controles terminan en `OK`. La selección se recupera. Los controles de combinación/visualización vuelven a sus valores iniciales de escena.
7. Seleccionar otro ID permite acumular cambios. Desmarcar una propiedad y reanalizar retira ese cambio. **Restaurar modelo base** vuelve a los recursos originales y elimina las modificaciones de la sesión.

Se aceptan coma o punto decimal. Los valores de carga son no negativos; las dimensiones deben ser positivas y finitas.

## Significado de los controles

| Selección | Carga Q | Sección |
|---|---|---|
| Cualquier viga | Carga **adicional** uniforme, vertical global −Z, kN/m | Rectángulo b/h en m |
| Cualquier columna HA | Carga **adicional** uniforme, vertical global −Z, kN/m | Rectángulo b/h en m |
| Columna tubular de acero | Carga adicional como arriba | Ancho exterior y espesor de pared SHS; conserva sección hueca y material |
| Cualquier paño de muro | Carga **adicional** vertical en borde superior, kN/m | Espesor del paño seleccionado, m |
| Cualquier losa | Intensidad superficial Q total, kN/m²; **reemplaza** la Q original de esa losa | Espesor, m; cambia peso propio y masa |

Las cargas adicionales de barras/muros pertenecen al caso Q y se suman a las cargas originales: introducir cero las elimina. No representan edición del peso propio G. En losas, el reparto tributario existente se conserva. Las cargas del borde superior de muro se distribuyen entre sus nodos de borde; se mantiene la idealización nodal de cargas de muro del modelo.

La sección rectangular usa la convención existente `Iy = h*b³/12`, `Iz = b*h³/12`; el mismo ID y sus ejes locales permanecen. El módulo de torsión rectangular es la aproximación de Saint-Venant usada en las variantes anteriores. El cambio de una sección reconstruye A/I/J, peso propio, masas, casos gravitacionales y sísmicos. Los muros actualizan rigidez Shell y capacidad con la armadura registrada. La capacidad de una columna con sección editada **no** se calcula con la rutina genérica de 0.70 × 0.70: sus pestañas de capacidad se bloquean para no mostrar la capacidad antigua como si fuera nueva; esfuerzos y deformada sí se recalculan.

Las losas son superficies tributarias, no elementos finitos de placa. Su espesor no introduce rigidez de placa artificial.

## Resultados → Deformada

Seleccionar viga, columna o muro y abrir la pestaña **Deformada**. En losas se muestra directamente el movimiento de los receptores.

- Gris: geometría original. Magenta: deformada del elemento seleccionado con la escala global del visor.
- Barras: gráfico de `|u|` en mm a lo largo de i → j, componentes globales OpenSees en los extremos y máximo muestreado.
- Interpolación cúbica de traslaciones y rotaciones nodales con 41 muestras, igual criterio cinemático de la deformada global. No se presenta como solución exacta de flecha interior bajo cargas distribuidas.
- Muros: contorno de los elementos Shell del paño y máximo desplazamiento nodal.
- Losas: movimiento de barras/muros receptores, rotulado explícitamente como **no** deformada de placa.
- La respuesta se actualiza con el caso, λ, ponderadores de masa y escala actuales.

## Reanálisis y archivos

Cada trabajo queda en `Application.persistentDataPath/AnalysisJobs/<id>/` con `request.json`, una copia selectiva del código/datos de `Edificio`, resultados, recursos y `analysis.log`. El original del repositorio no se sobrescribe. Las carpetas de trabajos se conservan para inspección.

`analysis/load_cases/unity_reanalizar.py` genera la geometría con las modificaciones, ejecuta OpenSees mediante `ejecutar.py` y publica `complete.json` después de comprobar éxito y archivos requeridos. Unity lee una instantánea completa desde disco, también en el Player Windows; nunca mezcla archivos numéricos faltantes con los del modelo base. Si hay error o `REVISAR`, conserva la última respuesta válida y muestra la ruta del registro.

SQ4 queda deshabilitado en variantes: sus bases corresponden a la geometría original. Restaurar base vuelve a habilitarlo. Los λ del mismo modelo siguen actualizando por superposición sin lanzar Python.

El control de conservación Q mantiene el benchmark de zonas originales, sustituyendo únicamente la Q original de la losa editada por `q_nueva × área_neta_geométrica`. No usa la suma de receptores nuevos como valor esperado ni cambia tolerancias. La carga adicional lineal se comprueba mediante los controles globales de equilibrio. Cambiar el peso de losas conserva el reparto y actualiza las resultantes y perfiles G de sus receptores.

## Verificación de esta implementación

- Compilación de todos los scripts C# contra las DLL de Unity 6000.5.10f1: sin errores (no sustituye una prueba visual en Play/Player).
- Trabajo aislado de viga 207 con sección 0.75 × 0.45 m y Q adicional 2 kN/m: OpenSees y exportación `OK`.
- Trabajo aislado combinado: columna 1 a 0.75 × 0.75 m con Q adicional 1 kN/m; muro 101 a espesor 0.65 m y Q adicional 2 kN/m; losa 1000000 a espesor 0.18 m y Q 4 kN/m²: `OK`, incluida conservación por piso.
- Cuatro pruebas nuevas comprueban entradas inválidas, sección tubular, independencia por ID y conservación del peso/carga de losas.
- Suite: 32/33 pruebas pasan. La prueba preexistente `test_generated_contract_and_audit` falla por discrepancia entre el hash SQ4 almacenado y los bytes del modelo base local; estos resultados originales no se han regenerado en esta tarea.
- Pendiente comprobación visual de botones, selección, recarga y gráficos en Unity y en un nuevo ejecutable Windows.
