# Guion de Defensa Individual P1L1

## 6 GDL

Cada nodo tiene tres traslaciones (`Ux`, `Uy`, `Uz`) y tres rotaciones
(`Rx`, `Ry`, `Rz`). El modelo utiliza `-ndm 3 -ndf 6`, por lo que cada nodo
tiene seis ecuaciones de equilibrio antes de aplicar las restricciones.

## `geomTransf`

`geomTransf` transforma las propiedades de la sección y las fuerzas de los
elementos entre el sistema global y el sistema local de cada elemento. Las
columnas y las vigas requieren vectores de referencia diferentes porque sus
ejes longitudinales tienen distintas orientaciones.

## Sistema local y sistema global

Los ejes globales describen el edificio y las cargas aplicadas. Los ejes
locales siguen la orientación de cada elemento. Por esto, `localForce` entrega
la fuerza axial, los cortantes, la torsión y los momentos en los extremos según
el sistema local del elemento.

## `Iy` e `Iz`

`Iy` e `Iz` son los segundos momentos de área respecto de los ejes locales `y`
y `z`. Controlan la rigidez a flexión mediante `E*I`. Intercambiarlos cambia
la respuesta a flexión del elemento.

## Qué resuelve OpenSees

Para cada caso de carga estática, OpenSees ensambla la matriz global de
rigidez, aplica los apoyos y las restricciones de los diafragmas rígidos,
resuelve la ecuación:

```text
K · u = P
```

Finalmente, calcula los desplazamientos nodales, las reacciones y las fuerzas
internas de los elementos.

## Por qué converger no significa estar correcto

Un análisis puede terminar correctamente desde el punto de vista numérico,
pero utilizar unidades, ejes, conectividad, cargas, apoyos o propiedades de
sección incorrectos. La corrección debe comprobarse de forma independiente
mediante:

- Equilibrio entre cargas y reacciones.
- Conservación de la carga tributaria de la losa.
- Orden de magnitud razonable de los desplazamientos y esfuerzos.
- Superposición para este modelo lineal.

## Preguntas clave del modelo

- La losa no se malló; su carga se descargó sobre las vigas mediante anchos
  tributarios.
- El hormigón utiliza `f'c = 25 MPa`, con unidades internas en kPa.
- Las fuerzas de elementos se exportan mediante `localForce`.
- El apoyo en `-7.97 m` representa la base del modelo; las fundaciones reales
  no se modelan.
