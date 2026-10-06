# Pendientes para cerrar la entrega y demostrar H1–H5

Estado revisado: **6 de octubre de 2026**. Este archivo separa funcionalidades implementadas de pruebas físicas y entregas todavía pendientes. Una prueba de editor no sustituye una prueba de teléfono.

Guías relacionadas: [ejecución y entrega](GUIA_EJECUCION_Y_ENTREGA.md), [detalle H1–H5](HONORS.md) y [evidencia de software](documentation/honors_evidence/README.md).

## 1. Prioridad inmediata: Cardboard en el Redmi — H1

- [ ] Instalar **Android Build Support, SDK/NDK y OpenJDK de Unity 6000.5.9f1** mediante Unity Hub y completar la autorización de Windows.
- [ ] Configurar Android y generar `CampusCardboard.apk` desde `Campus > Cardboard > Construir APK`.
- [ ] Guardar el manifiesto `delivery.json`, identificar commit y hashes e instalar esa APK en el Redmi Note 9 Pro.
- [ ] Configurar el perfil QR del visor y comprobar imagen estereoscópica, orientación de cabeza y recentrado.
- [ ] Probar mirada de 2 segundos, avance, retroceso, izquierda/derecha, detención al apartar la mirada y modos continuo/por pasos.
- [ ] Confirmar en teléfono que Diagrama y Backend sustituyen al menú principal, que Volver restaura movimiento y que no hay superposición.
- [ ] Seleccionar viga/columna, cambiar caso y estación, mostrar esfuerzos, deformada y capacidad con valores legibles.
- [ ] Registrar un recorrido de cinco minutos: FPS, comodidad y temperatura de batería si Android la expone. El objetivo inicial es 60 FPS; no hay medición real todavía. Optimizar si la prueba muestra problemas.

**Cierre:** APK identificada + prueba grabada con teléfono y visor + registro de rendimiento. El seguimiento implementado es de orientación, no posición 6 DOF. Actualmente no está instalado el módulo Android .9 ni existe esa APK en su ruta de entrega.

## 2. Sector físico y precisión AR — H2

- [ ] Elegir una crujía accesible y comprobar que los IDs corresponden a los elementos físicos; no asumir que los IDs de ejemplo identifican el sector correcto.
- [ ] Imprimir al menos tres marcadores de 20 × 20 cm, medir su tamaño real e instalarlos en posiciones permitidas.
- [ ] Medir centros, orientaciones y relación con coordenadas OpenSees; documentar instrumento, método e incertidumbre.
- [ ] Completar el manifiesto JSON del sector con origen, elementos, poses y hash del modelo. Establecer `surveyed:true` solo después del levantamiento.
- [ ] Importar mediante `Sector JSON` y probar la transformación común, rechazo de observaciones discordantes y estabilidad del anclaje.
- [ ] Medir puntos físicos independientes de los utilizados para calibrar y exportar error por punto, RMS y máximo.
- [ ] Repetir tres registros independientes y una prueba de deriva de 60 segundos con marcadores ocultos; exportar antes de reanclar.
- [ ] Probar pérdida/recuperación de seguimiento y relocalización tras reiniciar la aplicación.
- [ ] Confirmar con el docente si la relocalización por marcador satisface su requisito condicional de persistencia.

**Cierre:** levantamiento y JSON reales + reportes de tres registros/deriva + video en terreno. Objetivos iniciales del proyecto: RMS ≤5 cm y máximo ≤10 cm, no umbrales oficiales. No se implementó persistencia autónoma ni Cloud Anchors; guardar configuración no guarda el anchor del mundo.

## 3. Resultados sobre la estructura real — H3

Depende del registro medido de H2. La APK disponible es `visualization/android-ar/dist/EdificioAR-Honors.apk`.

- [ ] Verificar alineamiento sobre vigas/columnas reales y conservación del `elementTag`.
- [ ] Seleccionar hasta cuatro elementos sin perder el registro y consultar caso, componente y estación.
- [ ] Mostrar esfuerzos, deformada de barras y desplazamientos nodales de muros; mantener visibles unidades y amplificación.
- [ ] Mostrar P–Mz junto a una columna con sección/refuerzo compatibles y demanda del mismo eje.
- [ ] Mostrar áreas tributarias, área y carga asociadas a sus receptores; comprobar que las superficies no saturen la pantalla.
- [ ] Comparar valores mostrados contra la corrida fuente y grabar la demostración.

**Cierre:** video y comparación trazable de valores/IDs en el sector medido. La deformada de barra es Hermite interpolada; las losas tributarias no son placas analizadas. Una demo con marcador en mesa no demuestra alineamiento con el edificio.

## 4. Reanálisis desde el teléfono — H4

Las pruebas de software ya incluyen trabajos HTTP reales y comparación contra corrida directa. Falta verificar el recorrido completo desde el Redmi.

- [ ] Conectar PC y teléfono a una misma LAN privada y comprobar que la Wi-Fi permita comunicación entre dispositivos.
- [ ] Iniciar `analysis/backend/start-lan.ps1` con la IPv4 privada del PC y configurar URL/token temporal en los clientes. No publicar tokens ni exponer el servidor a internet.
- [ ] Desde AR solicitar Q adicional en una viga y recibir una revisión validada; repetir desde Cardboard cuando su APK esté disponible.
- [ ] Comparar valores antes/después y registrar solicitud, revisión, hashes y resultado. No confundir suma de casos guardados con reanálisis.
- [ ] Probar desconexión Wi-Fi, servidor detenido, solicitud inválida y cancelación; comprobar mensaje de error y conservación de la última revisión válida.
- [ ] Si se modifica el backend o el entorno numérico, repetir la comparación independiente con la corrida directa.

**Cierre:** solicitud desde el teléfono + resultados nuevos verificables + evidencia de manejo de fallos. Las variantes parten del modelo base; un hash nuevo invalida el sector AR anterior. Demostrar H2/H3 antes de cambiar el modelo.

## 5. Capacidad editable en la demostración — H5

Ya existe regeneración uniaxial y validación automática por fibras/OpenSees; falta completar la demostración de interfaz y documentar el caso utilizado.

- [ ] Elegir una columna compatible e identificar si dimensiones, materiales y refuerzo están documentados o son hipótesis académicas.
- [ ] Modificar barras por cara/diámetro, recalcular y mostrar curva anterior, nueva y demanda del mismo eje.
- [ ] Comprobar en la interfaz el rechazo de geometrías inválidas, recubrimiento insuficiente o barras solapadas.
- [ ] Guardar entradas, revisión y evidencia del antes/después; demostrar desde el teléfono si se presenta esa interfaz.
- [ ] Explicar que la capacidad es nominal de sección uniaxial y que el cambio de refuerzo no modifica automáticamente EI del modelo global.

**Cierre:** demostración reproducible y explicación de hipótesis. P–Mx–My, segundo orden y capacidad de miembro no son pendientes obligatorios de este objetivo H5 elegido; no se deben afirmar como implementados.

## 6. Ejecutables PC y entrega final

- [ ] Generar ejecutables Windows actuales si se presentará fuera del editor. No asumir que los ZIP/EXE antiguos contienen los últimos cambios.
- [ ] Probar CampusPlayable: F5/AmongUs/láser oculto, F6 reversible, recorrido, puertas y acceso/salida de ambos ascensores interiores.
- [ ] Probar UnityVisualization: carga móvil/reparto, selección, diagramas, conexión al backend y capacidad en el ejecutable actual.
- [ ] Ensayar todas las partes en el equipo y red de presentación; preparar proyección del teléfono y grabación de respaldo.
- [ ] Reunir APKs y ejecutables identificados, dependencias, entradas, logs, reportes de medición y videos sin credenciales.
- [ ] Actualizar estas casillas y el reporte según evidencia real; separar implementado, verificado automáticamente y probado físicamente.

**Orden recomendado:** APK Cardboard → prueba H1 → levantamiento H2 → superposición H3 → prueba móvil H4 → demostración H5 → ensayo integrado.

No hace falta rehacer las funciones aprobadas solo por figurar aquí: la mayor parte de los pendientes son compilación, medición y validación física. Corregir únicamente los problemas que esas pruebas revelen, preservando unidades, ejes, apoyos, equilibrio y conservación de cargas.
