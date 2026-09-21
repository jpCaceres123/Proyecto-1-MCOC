# SQ4 — carga móvil asociada al usuario

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
