# SQ4 — carga móvil asociada al usuario

## Versión 3: cuatro bordes y cámara de primera persona (actual)

En los **167 paneles** que el modelo identifica con vigas en los cuatro
bordes, la carga P se transmite a una viga de cada lado. Con posición
normalizada ξ en X y η en Y, las fracciones son:

| Borde | Fracción de P |
|---|---:|
| Inferior | (1−η)/2 |
| Superior | η/2 |
| Izquierdo | (1−ξ)/2 |
| Derecho | ξ/2 |

La suma es P. Los primeros momentos en X/Y coinciden con la posición de la
carga. Si un borde tiene varias barras, se escoge el tramo cuya proyección
queda más cerca del punto; el par entre posición ideal y proyección conserva
el momento aplicado. Las reacciones y acciones de extremo se calculan con
bases OpenSees para el edificio completo. La auditoría comprueba fuerza y
momento en los 652 paneles y soluciones explícitas por categoría/nivel.

Los otros **485 paneles** del contrato geométrico (476 paneles explícitos y
9 voladizos) no tienen cuatro vigas perimetrales asignadas. Siguen usando
sus receptores estructurales declarados y la transferencia excéntrica de la
versión 2. Dibujar cuatro vigas allí supondría inventar apoyos ausentes del
modelo. Esta distinción se indica en el panel de SQ4. Las cargas G/Q y sus
reglas anteriores no se modifican.

El personaje proviene del FBX `among-us.zip` entregado por el usuario, con
cuerpo rojo, contorno oscuro y visor celeste. La importación se conserva en
`Assets/Resources/AmongUs.fbx`; el visor lo escala a 1,20 m de altura. El
botón **Entrar en primera persona** ubica la cámara a la altura de sus ojos.
W/A/S/D avanza respecto a la dirección de la mirada, con el mismo bloqueo de
vacíos; mantener el botón derecho y mover el ratón cambia la mirada.
**Cambiar a exterior** devuelve la órbita centrada en la losa activa.
Un clic todavía coloca la carga en la superficie del nivel. El modelo del
personaje se oculta sólo en primera persona para no tapar la cámara.

La exportación usa contrato `schema = 3`, con 312 nodos base, 479 vigas
receptoras y 10.686 controles SQ4 aprobados. Es necesario regenerar recursos
con `python Edificio/analysis/load_cases/carga_movil.py` y reconstruir el
ejecutable después de actualizar el proyecto. El visor antiguo no debe leer
el contrato nuevo. Ver el
[reporte actualizado](../../reports/2026-09-23_primera_persona_cuatro_vigas.md).

## Versión 2: registro histórico de todas las losas

El alcance de dos bordes y los números de bases que siguen describen la
versión anterior, sustituida por la regla de cuatro bordes donde corresponde.

### Todas las losas

Se habilitan **652 paneles, 318 vigas receptoras y cinco niveles**. Ya no se
limita el recorrido a una franja de cuatro losas. Seleccionar nivel y losa en
la planta general, escribir su ID, o hacer clic en la vista 3D del nivel activo.
W/A/S/D camina entre superficies contiguas; los huecos, juntas y espacios sin
losa bloquean el recorrido. La selección por clic puede llevar la carga a otra
losa sin simular un recorrido por el espacio intermedio.

La regla física conserva los dos bordes opuestos donde están definidos. Si un
borde contiene varias barras, se utiliza el tramo cuya proyección está más
cerca de la posición. Para los 9 voladizos y los 476 paneles de carga explícita
se utilizan exclusivamente sus receptores `support`/`nearest_support` del
modelo: P se aplica en la proyección más próxima y se incorpora el par
`M = (r_carga − r_apoyo) × F`. No se borra la excentricidad ni se agregan apoyos.
En los dos bordes, el par sólo corrige el desplazamiento entre la posición
ideal en el borde y su proyección real; no duplica el primer momento ya
conservado por P(1−η)/Pη. En empates se elige el menor ID para reproducibilidad.

Esta es una transferencia idealizada de fuerza y momento, no una losa FE ni
una validación de su rigidez de placa. El cambio de receptor entre barras no
conectadas puede producir un salto de respuesta: no se suaviza artificialmente.
La deformada mostrada sigue siendo de barras; no se afirma calcular la flecha
del extremo libre de una losa en voladizo sin modelar su rigidez.

La nueva respuesta utiliza bases nodales Fz/Mx/My en 310 nodos, comprimidas por
nodo en `Resources/SQ4Nodes/*.bytes` y cargadas cuando se necesitan. Son 930
soluciones lineales del edificio. Las cargas consistentes de una fuerza y un
par puntuales se obtienen por trabajo virtual con Hermite y sus derivadas.
Las acciones de extremo se corrigen restando esas cargas equivalentes a las
fuerzas de barra de la solución nodal. La integración de curvatura incluye
tanto fuerza puntual como par; el gráfico incorpora el salto de momento.

Regenerar con `python Edificio/analysis/load_cases/carga_movil.py` o con
`python Edificio/analysis/load_cases/ejecutar.py --carga-movil`. El archivo
`carga_movil.json` es ahora contrato **schema 2**, con geometría, reglas y
vacíos; necesita también los archivos de bases. La caché local en
`results/sq4_cache/` no se publica. Si se modifica el algoritmo del modelo o
sus módulos auxiliares, invalidar esa caché antes de regenerar.

La auditoría actual registra **10.680 controles aprobados**, incluidos
conservación por panel y contrastes de superposición en cada categoría/nivel.
Las pruebas independientes incluyen vigas biempotradas y en voladizo con
fuerza y par puntual, contrastadas con subdivisión explícita. Se mantienen
los límites del modelo heredado `equalDOF` y el carácter incremental,
lineal y cuasiestático: no se presenta como validación integral de diseño.

## Versión 1: registro histórico de la franja inicial

Los números de paneles, bases y restricciones de recorrido que siguen
describen la versión anterior, sustituida por la versión 2 indicada arriba.

Implementación incremental, lineal y cuasiestática. El avatar representa una
carga viva vertical, adicional a los casos gravitacionales y sísmicos. La
velocidad del recorrido es visual: no se modelan impacto, inercia ni vibración.

## Regla física y paneles

El recorrido habilitado es una franja de paneles completos del edificio, entre
X = −28,30 y −24,55 m. Hay cuatro paneles contiguos en cada uno de los cinco
niveles Z = 3,96; 7,92; 11,88; 15,84 y 19,80 m (20 paneles en total).
Se obtienen del contrato geométrico; se excluyen paneles con vacíos y se exige
que las dos vigas receptoras cubran exactamente sus bordes. La posición se
limita a esa superficie. W/A/S/D permite cruzar entre paneles vecinos del mismo
nivel; las flechas del selector permiten cambiar de panel o nivel.

Para una posición (x,y), sea η=(y−ymin)/(ymax−ymin). La viga inferior recibe
P₀=P(1−η) y la superior P₁=Pη. Ambas cargas se aplican dentro de las vigas en
su proyección X=x. Se respeta el sentido i→j de cada elemento. Esta es una
regla didáctica unidireccional de transferencia de carga localizada; no es un
análisis FE de losa ni modifica el reparto de 45° de las cargas G/Q existentes.

Se cumple P₀+P₁=P y P₀ r₀+P₁ r₁=P r, por lo que se conservan la fuerza y el
momento de la carga aplicada. En un borde compartido, toda la carga llega a la
misma viga desde cualquiera de los dos paneles; no se duplica.

## Respuesta estructural

Las bases se resuelven sobre el edificio completo, con los apoyos, shells,
rigidez y vínculos existentes. Cada una de las 25 vigas receptoras tiene cuatro
casos de 1 kN en x/L = 0, 1/3, 2/3 y 1. Para este marco elasticBeamColumn con
transformación Linear, las fuerzas nodales equivalentes de una carga puntual
dependen cúbicamente de su posición. La interpolación de Lagrange reproduce
los GDL y las acciones de extremo a cualquier posición, sin ejecutar Python
en cada cuadro de Unity. La magnitud y el reparto se superponen linealmente.

Los diagramas incluyen el salto de corte y el cambio de pendiente del momento
en el punto de carga. El máximo absoluto de momento se evalúa exactamente en
los extremos y en el punto de aplicación. La deformada de las vigas cargadas
se obtiene integrando la curvatura, incorporando el término de carga puntual;
las demás barras usan Hermite. El máximo de desplazamiento indicado se busca
en 101 puntos de cada viga receptora. La deformada rosa muestra las barras del
nivel activo. Los resultados son incrementos Δ debidos exclusivamente a la
carga móvil, no una combinación G+Q+SQ4 ni una comprobación de capacidad.

## Uso en Unity

1. Abrir `Assets/Main.unity` y ejecutar Play.
2. Pulsar **Explorar carga móvil** en el panel izquierdo.
3. Elegir magnitud (0–100 kN, con botones 0/1/10/50), panel y posición.
4. Arrastrar el punto en la planta del panel derecho o usar W/A/S/D.
5. Usar **Recorrer →**, **Pausar**, **Centrar** o **Enfocar**.
6. Pulsar una viga receptora para consultar su ΔMy y ΔVz, incluidos extremos
   y máximo interior. El turquesa corresponde al borde inferior, ámbar al
   superior; el avatar es ámbar y la deformada rosa.
7. **Volver a casos del edificio** restaura las capas y la visualización previa.

La tarjeta muestra la carga recibida por cada viga, su porcentaje, el error
de conservación de fuerza y momento del reparto y la suma de reacciones
verticales. Con P=0 desaparecen las flechas y la respuesta adicional es nula.

## Regeneración y comprobaciones

Desde la raíz del repositorio:

```powershell
python Edificio/analysis/load_cases/carga_movil.py
python -m unittest discover -s Edificio/verification/tests -p "test_*.py"
```

También se puede ejecutar todo con
`python Edificio/analysis/load_cases/ejecutar.py --carga-movil`.
Al cambiar geometría, secciones, materiales o restricciones deben regenerarse
ambos conjuntos de resultados. SQ4 conserva el hash del contrato geométrico en
`Assets/Resources/carga_movil.json` y escribe su auditoría en
`results/verificacion_carga_movil.json`. No se editan esos resultados a mano.

La auditoría contrasta posiciones no usadas como bases, magnitudes distintas,
bordes y carga cero con corridas explícitas de OpenSees. Verifica GDL, acciones
de todas las barras, reacciones y conservación del reparto. Las pruebas
independientes incluyen una viga biapoyada y otra biempotrada: comparan la
deformada integrada con elementos subdivididos y las flechas PL³/(48EI) y
PL³/(192EI) para carga central. Unity tiene comprobación de extremo de
deformada y respuesta nula en el modo de captura `--moving-preview`.

Limitación heredada: existen vínculos equalDOF entre nodos no coincidentes.
Pueden introducir pares globales; la auditoría registra el residuo de momento
obtenido usando sólo apoyos SP y no lo presenta como una verificación aprobada
de equilibrio global de momento. El reparto SQ4 conserva su momento aplicado
y la suma global de fuerzas se verifica. No se cambió esa idealización del
edificio como parte de esta funcionalidad.

La aplicación de cargas puntuales en coordenadas locales y de su posición
normalizada sigue la [documentación oficial de eleLoad de OpenSeesPy](https://openseespydoc.readthedocs.io/en/latest/src/eleload.html).
Los resultados y las capturas de escritorio se resumen en
[el reporte de entrega](../../reports/2026-09-21_vigas_y_carga_movil.md).
