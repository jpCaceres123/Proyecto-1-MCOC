# Campus Cardboard — aplicación independiente para celular

Abrir esta carpeta como un proyecto nuevo en Unity Hub, usando Unity **6000.5.9f1**. No abrir CampusPlayable para probar esta aplicación. Escena: `Assets/CampusCardboard.unity`.

Al pulsar Play en el editor se inicia la previsualización. En Android se inicia el SDK Cardboard real, con vista estéreo, lentes del visor y orientación de la cabeza. La previsualización no acredita el funcionamiento físico en el teléfono.

- Mirar un elemento o botón **2 segundos** para seleccionarlo; el aro indica el progreso. El botón del visor confirma inmediatamente de forma explícita.
- **AVANZAR / RETROCEDER**: mantener la mirada después de los 2 segundos. Apartarla detiene el desplazamiento. Ambos usan el eje frontal del menú; cambiar entre ellos cancela el movimiento y exige una nueva confirmación.
- Botón del visor mirando un espacio vacío: recolocar el menú frente a esa dirección.
- Caso / Diagrama / i-x-j: consultar resultados originales de OpenSees, sin recalcular ni modificar signos o unidades.
- **Diagrama** abre un selector explícito: N, Vy, Vz, T, My, Mz, Deformada u Ocultar. La curva se dibuja sobre la barra seleccionada, con extremos i/j, caso, unidades y escala. Un marcador amarillo sigue la estación consultada. Cambiar de caso actualiza el diagrama del mismo elemento.
- **Deformada** muestra una interpolación cúbica Hermite de las traslaciones y giros nodales exportados, amplificada para verla; no son resultados OpenSees calculados directamente en puntos interiores. Se conserva la posición inicial como referencia. La ficha muestra magnitud de desplazamiento en mm y la etiqueta indica el factor de amplificación.
- Estructura / Otro piso / Recentrar / Soltar ficha / Salir VR: controles dentro del visor.
- Solo en el editor: clic derecho + mouse para mirar; clic izquierdo confirma; M recoloca el menú; F7 o Escape sale.

No incluye menú de pausa del juego PC, rifle ni tercera persona. El juego Windows permanece separado en `../CampusPlayable`. Las geometrías, recursos y resultados se copiaron conservando identificadores; son una instantánea, no una regeneración estructural.

## APK

Instalar Android Build Support, SDK/NDK y OpenJDK para **6000.5.9f1**. Luego `Campus > Cardboard > Configurar Android`, reiniciar si lo pide y `Campus > Cardboard > Construir APK`. Salida esperada tras una compilación exitosa: `Build/Android/CampusCardboard.apk`. El sistema de entrada de este proyecto es **Input System Package (New)**, independiente del juego PC.

## Pruebas

`Campus > Cardboard > Probar interacción en Play` comprueba la consulta de datos, ambos sentidos de recorrido, selección a 2 segundos, detención, traslado seguro de piso y ciclos de entrada/salida. Revisar `CAMPUS_VR_CHECK_COMPLETE passed=True` en Console. Sigue siendo necesaria la prueba de APK y visor en el Redmi Note 9 Pro.
