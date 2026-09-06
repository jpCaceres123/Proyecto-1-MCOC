# Guía desde cero

## 1. Abrir Unity Hub

Abrir **Unity Hub** desde el menú Inicio de Windows.

## 2. Abrir el proyecto correcto

En **Projects**, elegir **Add > Add project from disk** y seleccionar exactamente:

```text
<clon-del-repositorio>\P1L1\UnityProject
```

Abrirlo con la versión indicada en `ProjectSettings/ProjectVersion.txt`.

## 3. Abrir la escena

En la ventana **Project**, navegar a:

```text
Assets > Scenes > Frame3D
```

Hacer doble clic en `Frame3D`.

## 4. Ver la geometría en Scene

En la ventana **Hierarchy**, seleccionar `OpenSees 3D Frame` y presionar `F` con el cursor sobre la ventana **Scene**. Activar el botón **Gizmos** de la esquina superior derecha.

La geometría gris es la no deformada, la celeste es la deformada amplificada, las zonas semitransparentes representan el área tributaria de la losa y las flechas/diagramas rojos representan la descarga bidireccional sobre las vigas.

## 5. Ver la geometría en Game

Presionar el botón triangular **Play** arriba al centro. Luego seleccionar la pestaña **Game**. El script crea automáticamente columnas, vigas, nodos, ejes y cámara al iniciar.

Si la ventana `Game` está pequeña, usar el menú `Display 1` y seleccionar `Free Aspect`.

## 6. Si no aparece

- Confirmar que el botón **Play** está azul: el programa debe estar ejecutándose.
- En **Hierarchy** debe aparecer `OpenSees 3D Frame`.
- Abrir **Window > General > Console** y comprobar que no existan errores rojos.
- Si la vista está lejos, seleccionar `OpenSees 3D Frame` y presionar `F`.
- Si solo se quiere revisar la vista editada, volver a presionar **Play** para detener y regresar a **Scene**.

## 7. Resultado esperado

Debe verse un marco rectangular de `6 m x 5 m`, altura `3 m`, cuatro columnas, vigas segmentadas según las estaciones de carga de OpenSees y una losa física semitransparente sobre las vigas. La geometría se lee de `../results/model.json`, generado ejecutando `python .\P1L1\model.py` desde la raíz del repositorio. Cada columna termina en un empotramiento formado por placa base gris, pedestal cilíndrico y cuatro pernos dorados. Los ejes se muestran como X rojo, Y verde y Z azul. La deformación vertical se amplifica `1000` veces para poder apreciarla. La losa muestra dos trapecios amarillos tributando a las vigas de `6 m` y dos triángulos verdes tributando a las vigas de `5 m`. Las cargas se muestran en rojo con flechas hacia abajo y una envolvente del diagrama, con intensidad máxima `12.5 kN/m`.
