# Recreación arquitectónica del edificio de Ingeniería

Proyecto independiente del LaboratorioEstructural, creado con las tres fotografías proporcionadas por el usuario. El usuario confirmó que la fachada ocre y la fachada acristalada son caras opuestas del mismo conjunto, y que el diseño continúa en los dos bloques.

## Abrir

Ejecutar `Abrir_recreacion.cmd`. También puede abrirse `Aplicacion/RecreacionArquitectonica.exe` directamente.

Para editar: en Unity Hub, añadir la carpeta `Unity`, usar Unity 6000.5.9f1 y abrir `Assets/Scenes/Recreacion.unity`. La escena contiene objetos y materiales editables. `Assets/Prefabs/Edificio.prefab` contiene el conjunto y su entorno. No se necesita ejecutar un generador para visualizar la escena.

## Controles

- Botón derecho y arrastrar: girar la cámara.
- Rueda: acercar o alejar.
- Botón central y arrastrar: desplazar el encuadre.
- WASD: desplazarse; Q/E: bajar y subir; Shift: aumentar velocidad.
- 1–4: vistas de vidrio, fachada ocre, cubierta y acceso.
- F: alternar órbita y vuelo libre; Tab: ocultar o mostrar el panel.

## Qué se reprodujo

Muro cortina con perfilería y variación del vidrio, volumen acristalado saliente, revestimientos anaranjados y ocres, bandas claras, extremo superior en voladizo, escaleras exteriores en zigzag con peldaños y antepechos, plataformas, terraza con mesas y sillas, cubierta nervada y equipos. Entorno aproximado con cancha, pavimentos, estacionamiento y árboles.

Las tres fotografías originales se conservan en `Unity/Assets/Referencias`.

## Dimensiones y alcance

Se usaron como referencia las magnitudes del modelo existente: LT1 llega a X=50 m, LT2 a X=-32,05 m, ancho de ejes 16,15 m, niveles cada 3,96 m y altura de referencia 19,80 m. Unity usa metros; la coordenada vertical es Y (en el modelo de cálculo es Z).

La distribución exacta de paños, salientes, escaleras, mobiliario, colores, pendientes y equipos se interpreta de las fotos. No es un levantamiento por fotogrametría ni un modelo BIM validado. La topografía se simplifica. El interior no se reconstruyó; los vidrios son superficies reflectantes. La ubicación de la junta y los bloques sigue el esquema del modelo previo, mientras que el detalle de revestimientos requiere cotejo con planos de arquitectura para alcanzar exactitud constructiva.

Este proyecto es una visualización arquitectónica. No ejecuta OpenSees ni modifica el proyecto estructural existente.

## Código y regeneración

- `Unity/Assets/Editor/BuildArchitecture.cs`: crea la geometría, materiales, escena, prefab y ejecutable.
- `Unity/Assets/Scripts/ArchitectureViewer.cs`: cámara, controles, panel y captura de vistas.
- `Compilar.ps1`: ejecuta Unity en modo batch.
- `Capturas`: vistas renderizadas y registro de comprobación.

Regenerar sobrescribe la escena y el prefab generados. Guardar una copia con otro nombre antes de efectuar modificaciones manuales que se deseen conservar.
