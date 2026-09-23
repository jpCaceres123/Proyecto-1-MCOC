# Carga móvil: cuatro vigas y vista en primera persona

Fecha: 23 de septiembre de 2026.

## Entrega

La carga móvil se representa con el modelo FBX **Among Us** proporcionado por
el usuario. La vista exterior muestra cuerpo rojo, contorno oscuro y visor
celeste; el botón **Entrar en primera persona** pasa la cámara a la posición
de sus ojos. En esa vista W/A/S/D sigue la orientación de la mirada y el
botón derecho gira la cámara. La vista exterior se recupera con un botón.

El modo SQ4 continúa permitiendo elegir cualquiera de los 652 paneles por
nivel, ID, plano general o clic, y bloquea los vacíos al caminar.

## Reparto a cuatro vigas

En los **167 paneles** con cuatro bordes de vigas definidos en el contrato
estructural, cada borde recibe una fracción de P. Para ξ=(x−xmin)/(xmax−xmin)
y η=(y−ymin)/(ymax−ymin):

| Viga del borde | Carga aplicada |
|---|---:|
| Inferior | P(1−η)/2 |
| Superior | Pη/2 |
| Izquierdo | P(1−ξ)/2 |
| Derecho | Pξ/2 |

Por ejemplo, en el centro cada borde recibe **P/4**. Con P=50 kN son
12,5 kN por borde. Al moverse hacia una esquina cambian las cuatro fracciones.
Su suma permanece P y el primer momento se conserva en X e Y. Si hay más de
una barra en un borde, se elige el tramo más cercano y se añade el par de
proyección para mantener el momento de la fuerza aplicada. El motor mantiene
los apoyos, rigideces y ejes existentes, y vuelve a calcular la respuesta
incremental ΔSQ4.

Los otros 485 paneles no contienen cuatro vigas perimetrales declaradas:
476 son paneles explícitos con un apoyo asignado y 9 son voladizos. Allí se
mantiene la regla de transferencia de fuerza y par al apoyo real del modelo.
El visor lo rotula como «Apoyo asignado». No se atribuyen cargas a vigas
inexistentes. Las cargas gravitacionales G/Q y su reparto siguen igual.

## Pruebas y límites

- 29 pruebas unitarias aprobadas, incluida una comprobación independiente de
  las cuatro fracciones y de su primer momento para los paneles pertinentes.
- 10.686 controles SQ4 aprobados, con 652 paneles y 312 nodos de influencia.
- Compilación Windows64 y comprobación interna del ejecutable con el personaje,
  el reparto y el cambio de cámara.

La regla de cuatro bordes es una idealización de transferencia de carga
localizada; las losas no se modelan como placas de elementos finitos. La
respuesta permanece lineal, cuasiestática e incremental. El modelo conserva
la limitación documentada de vínculos equalDOF no coincidentes, por lo que
la conservación del momento aplicado no se presenta como certificación de
equilibrio global por apoyos SP ni como validación de diseño.

Instrucciones y fórmula detallada:
[CARGA_MOVIL.md](../Edificio/documentation/CARGA_MOVIL.md).

## Capturas del ejecutable

![Among Us y cuatro vigas receptoras](assets/carga_movil_among_us/carga_movil_cuatro_vigas.png)

![Vista en primera persona](assets/carga_movil_among_us/carga_movil_primera_persona.png)

El archivo FBX original aportado por el usuario se guardó como
`Assets/Resources/AmongUs.fbx` en el proyecto Unity; los colores se aplican
en tiempo de ejecución a las piezas del cuerpo, contorno, visor y mochila.

Para probarlo sin abrir el editor, el ejecutable Windows quedó en
`Edificio/visualization/unity/UnityVisualization/Build/SQ4-AmongUs-2026-09-23/Edificio.exe`
(compilación local, no incluida en GitHub por su tamaño). En Unity se puede
abrir el proyecto `Edificio/visualization/unity/UnityVisualization` y ejecutar
la escena principal con el contrato SQ4 actualizado.

## Actualización visual del entorno

El visor ahora configura un cielo azul claro con iluminación ambiental más
natural, crea un terreno de pasto bajo la estructura con briznas finas, y aplica
a las vigas una textura de hormigón de grano fino. Las texturas se generan de
forma procedural al iniciar Unity, así que no dependen de archivos externos ni
cambian los datos ni el cálculo estructural. El código está en
`Edificio/visualization/unity/UnityVisualization/Assets/Scripts/BuildingVisualizer.cs`.
