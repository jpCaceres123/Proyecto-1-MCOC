# Interfaz para celular

Se modificó el proyecto existente en `Edificio/visualization/unity/UnityVisualization`.
Las modificaciones corresponden a presentación, navegación y entrada táctil; no cambian los resultados OpenSees.

## Uso

La pantalla inicia con el edificio, el botón **Centrar** y cuatro accesos inferiores:

| Acceso | Uso |
|---|---|
| Modelo | Mostrar u ocultar vigas, columnas, muros, losas, apoyos, identificadores y ejes. |
| Cargas | Elegir G, Q, EX, EY o R; consultar desplazamiento real en mm y ajustar la ampliación visual. |
| Elemento | Consultar la selección, buscar por ID, centrarla, ver fuerzas y capacidad. |
| Ayuda | Consultar los gestos y la función de cada menú. |

Solo se abre un panel cada vez. Pulsar su acceso de nuevo o **Cerrar** libera la vista.
En vertical el panel aparece abajo; en horizontal se sitúa a la derecha. Los controles se escalan con la pantalla y respetan `Screen.safeArea`.

Tocar selecciona; arrastrar con un dedo orbita. Dos dedos permiten acercar/alejar y desplazar la vista.
Deslizar verticalmente dentro de un panel desplaza sus controles. La cámara no responde a los toques de los paneles.
La búsqueda se puede cerrar para priorizar los resultados del elemento seleccionado.

Las opciones avanzadas de masa sísmica se despliegan a petición. Las fuerzas y la capacidad de barras se consultan en pestañas separadas.
Se mantiene un acceso desplegable a fibras y momento–curvatura de la sección HA de referencia, identificado como tal.
Las condiciones/limitaciones de las curvas P–M siguen mostrándose con sus datos.

## Probar desde Windows

1. Abrir el proyecto Unity y `Assets/Main.unity`.
2. Entrar en Play.
3. Elegir **View → Vista para celular**. Volver a pulsarlo desactiva la vista forzada.
4. En Game, usar una resolución vertical (por ejemplo 430 × 900) o una horizontal (900 × 430).

También se generó una compilación Windows de comprobación en `Edificio/visualization/unity/UnityVisualization/Build/MobilePreview/Edificio.exe`.
Es un ejecutable de escritorio, no una IPA ni una publicación web. Se puede iniciar con:

```powershell
& .\Edificio\visualization\unity\UnityVisualization\Build\MobilePreview\Edificio.exe -screen-width 430 -screen-height 900 -screen-fullscreen 0
```

## Archivos modificados

Rutas C# relativas a `Assets/`:

- `Scripts/MobileViewerUI.cs`: distribución de paneles, escala, área segura, estilo táctil, ayuda y navegación.
- `Scripts/OrbitCamera.cs`: gestos con bloqueo compartido y reinicio de vista.
- `Scripts/BuildingVisualizer.cs`: menú de capas y restauración de objetos al quitar el filtro de nivel; IDs y nodos ocultos inicialmente en móvil.
- `Scripts/Semana3Visualizer.cs`: panel de cargas, valores en mm, combinación, opciones avanzadas y gráficos de referencia.
- `Scripts/ElementInspector.cs`: búsqueda opcional, selección táctil, enfoque y pestañas de fuerzas/capacidad.
- `Editor/BuildMobile.cs`: exportación iOS con autorrotación vertical/horizontal.
- `Editor/BuildMobilePreview.cs`: compilación de comprobación y menú de previsualización.
- `Scripts/MobilePreviewCapture.cs`: comprobaciones automáticas opcionales, activadas solo por argumento de prueba.

## Verificación y límites

La compilación Windows se comprobó con el editor disponible Unity 6000.5.9f1. Se conserva la versión declarada 6000.5.10f1 del proyecto.
Las ejecuciones de comprobación recorren los cuatro paneles, seleccionan la columna 15 y comprueban que la interfaz bloquea la cámara y deja libre el centro cuando está cerrada.
Los logs se guardan en `Edificio/documentation/semana05_evidencias/mobile_build.log`, `mobile_runtime.log` y `mobile_horizontal.log`.

Resultado: compilación exitosa y ejecuciones **430 × 900** y **900 × 430** terminadas con código 0 y `MOBILE_UI_CHECKS_DONE`, sin excepciones ni aserciones fallidas.

La captura automática en la ventana oculta produjo imágenes negras, por lo que no se utilizan como evidencia visual. Queda pendiente la revisión visual interactiva y la prueba física en Safari/iPhone, especialmente teclado, scroll y rendimiento.
El cambio no instala el módulo Web Build Support ni publica una versión web. El estado de la compilación iOS sigue condicionado al módulo y al proceso de firma descritos en `semana05.md`.
