# Laboratorio estructural LT1 + LT2

Actualización de apoyos: las 29 bases de columnas están empotradas en sus seis grados de libertad, incluidos todos los arranques elevados. `fix_all_column_bases` en `config.json` conserva esta condición en cada reanálisis. El detalle de coordenadas y nodos está en `resultados/apoyos_columnas.json`.

Aplicación Unity para explorar el modelo de los dos edificios y resultados calculados en OpenSeesPy. Fuente geométrica y de cargas: `../modelo_estructural_completo.xlsx`. El libro original no se modifica.

Corrección de geometría confirmada por el usuario: los tres pilares del eje I, X=40 m, en Y=0; 7,25 y 16,15 m, comienzan empotrados en Z=3,96 m. Se eliminan sus tramos inferiores originales (fuentes 4, 8 y 12). Esta corrección se guarda en `config.json`, en `column_base_corrections`, y se aplica sobre el Excel en cada cálculo, incluidos los reanálisis solicitados desde Unity.

## Abrir y usar

1. Ejecutar `Abrir_laboratorio.cmd` o `Aplicacion/LaboratorioEstructural.exe`. No hace falta abrir Unity para usar el laboratorio.
2. Seleccionar ambos edificios, LT1 o LT2; elegir un nivel para inspeccionar una planta.
3. Hacer clic en una barra o activar **Nodos** y seleccionar un nodo. También se puede buscar un `elementTag` en **Inspección**.
4. Ajustar G, Q, EX y EY. La aplicación combina los cuatro casos sin llamar al solver. **Prueba** usa la combinación contrastada con OpenSees.
5. Activar deformada, cargas, ejes locales, diafragmas o losas. La amplificación es solamente visual.
6. En **Cargas**, inspeccionar los aportes y polígonos tributarios de la viga de origen seleccionada.
7. En **Sección**, elegir columna o muro: aparece la curva P-M o M-phi y la demanda del elemento de referencia. El botón de selección identifica ese elemento en 3D.
8. En **Cambios**, modificar E, dimensiones de vigas/columnas, rigidez de muros o coeficiente lateral. **Guardar cambios y ejecutar Python** recalcula, verifica y recarga los resultados. El proceso tarda varios segundos; los errores quedan en `resultados/reanalisis.log`.

Navegación: arrastre derecho para orbitar, rueda para acercar, botón central para desplazar. WASD mueve la vista; R/F sube/baja; Shift aumenta velocidad. **3D** restablece el encuadre; **Planta** cambia la orientación. **Centrar elemento** enfoca la selección.

## Archivos principales

| Ubicación | Contenido |
|---|---|
| `Aplicacion/` | Ejecutable Windows y sus archivos de datos; conservar juntos |
| `Unity/` | Proyecto editable de Unity 6000.5.9f1; escena `Assets/Scenes/Laboratorio.unity` |
| `python/model.py` | Importación Excel, geometría analítica y trazabilidad de cambios |
| `python/solver.py` | Dominio 3D, restricciones, cargas, análisis y fuerzas internas |
| `python/capacity.py` | Secciones de fibras, P-M, M-phi y comparación independiente |
| `python/run_project.py` | Flujo completo de cálculo y exportación |
| `python/test_project.py` | Pruebas mecánicas y de consistencia de resultados |
| `config.json` | Supuestos base, propiedades, cargas y armado didáctico |
| `overrides_unity.json` | Últimas modificaciones solicitadas desde Unity |
| `resultados/proyecto.json` | Geometría, cuatro casos, curvas y datos para la interfaz |
| `resultados/INFORME.md` | Resultados numéricos y limitaciones del modelo |
| `resultados/validacion.json` | Equilibrio, superposición y otras verificaciones |
| `resultados/cambios_geometria.json` | Registro de transformación del Excel |
| `documentacion/` | Decisiones de modelación, referencia de intercambio y bitácora IA |

## Recalcular fuera de Unity

Ejecutar `Calcular.cmd`. La configuración base está en `config.json`; los cambios guardados por Unity no alteran ese archivo. Ejecutar sin `--overrides` restaura el escenario base.

Desde una terminal con OpenSeesPy instalado:

```powershell
python python/run_project.py
python python/test_project.py
python python/run_project.py --overrides overrides_unity.json
```

El cálculo exporta resultados a `resultados/`, al proyecto Unity y a las carpetas StreamingAssets de la aplicación ya compilada. No es necesario recompilar para cambiar resultados. Cada archivo JSON se reemplaza mediante un archivo temporal al finalizar correctamente el cálculo.

La ruta del intérprete usada en este equipo está en `Calcular.ps1` y se registra en el JSON del proyecto. Para trasladar el proyecto a otro computador, instalar las dependencias de `python/requirements.txt`, recalcular allí y copiar toda la carpeta de la aplicación. `requirements-tested.txt` registra las versiones verificadas aquí.

## Editar o recompilar Unity

Abrir la carpeta `Unity` desde Unity Hub. Abrir `Assets/Scenes/Laboratorio.unity` y pulsar Play. Se usa el pipeline gráfico integrado y el sistema de entrada clásico. El proyecto no requiere servicios externos.

Para recompilar Windows, ejecutar `Compilar_Unity.ps1`. La versión instalada y utilizada es Unity 6000.5.9f1 con soporte Windows. Si el proyecto ya está abierto en el Editor, cerrarlo antes de compilar por línea de comandos. El método de compilación crea la escena e incorpora los shaders usados en tiempo de ejecución.

## Qué está calculado

- Modelo lineal elástico 3D, seis grados de libertad por nodo; sin efectos P-Delta.
- Vigas y columnas elásticas y muros como barras equivalentes verticales, con brazos de rigidez finita sin peso.
- Diez diafragmas independientes: cinco por edificio, sin restricciones entre LT1 y LT2.
- Gravedad, carga viva y sismo pseudoestático idealizado en X e Y.
- Cargas de piso mediante aportes tributarios; peso propio de miembros agregado una sola vez.
- Desplazamientos, reacciones, fuerzas locales y diagramas.
- Superposición numérica contrastada con una corrida combinada explícita.
- P-M para una columna y un muro, M-phi a axial constante y comparación con cálculo independiente.

Las losas se muestran como geometría de referencia y áreas tributarias, **sin elementos finitos de losa**. Los nodos de sus polígonos no se añaden como grados de libertad libres al solver.

## Hipótesis que deben contrastarse con el curso

El Excel requería decisiones adicionales: muros dibujados como diagonales, conexiones sobre la junta, nodos gráficos y cargas asignadas a la viga más cercana. La versión analítica registra sus transformaciones; no se presenta como reproducción ya validada de la estructura real.

La junta nominal de 10 cm se verifica entre las caras de los muros de borde en la geometría base. Cambiar secciones puede cambiar separaciones físicas; la interfaz conserva independencia analítica, pero no verifica interferencias geométricas de todas las secciones modificadas ni suficiencia sísmica de la junta.

El patrón sísmico usa `V = C W` con `C = 0,10`, editable; no sustituye un procedimiento normativo. Faltan las instrucciones particulares del profesor para confirmar rigideces, diafragmas, apoyos, muros equivalentes y patrón lateral.

**Las armaduras de las curvas son didácticas.** Deben reemplazarse por el armado de los planos antes de interpretar la capacidad del edificio. No se verifican corte, adherencia, pandeo de barras, inestabilidad ni interacción biaxial. La aplicación muestra capacidad nominal, sin factor de reducción.

## AR básica

La pestaña **AR** permite usar una cámara conectada y superponer la geometría con alineación manual de giro y posición. La cámara solo se activa al pulsar el botón. Esc o **Detener cámara** la detiene.

Es una experiencia de cámara con registro manual en un punto fijo: no incluye SLAM, reconocimiento de marcadores, oclusión del entorno ni anclajes persistentes. La prueba física en el edificio, con referencias conocidas y una cámara, queda pendiente. No se ha generado una aplicación Android/iOS.

## Verificación

Las pruebas comprueban una ménsula frente a su solución analítica, la integral de una carga triangular, destinos de carga, separación entre edificios, compatibilidad de diafragmas y equilibrio de fuerzas/diagramas. Las curvas de fibras se contrastan con compresión uniforme independiente.

La prueba automática de Unity carga el JSON, combina casos, selecciona elementos y captura vistas. Con `--smoke-reanalysis` también cambia E al 80%, verifica que el desplazamiento de gravedad aumente a 1,25 veces y restaura el escenario base. La evidencia visual está en `resultados/unity_smoke.txt` y la prueba de reanálisis se conserva en `resultados/unity_reanalysis.txt`. La cámara AR y el uso en el edificio requieren una prueba presencial.
