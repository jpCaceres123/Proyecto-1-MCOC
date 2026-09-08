# Semana 3 — carga viva, sismo, superposición y capacidad HA

**Estado de controles numéricos: OK.** El modelo conserva la geometría y las
restricciones de Semana 2. Los resultados de capacidad corresponden a una
armadura académica supuesta, pendiente de reemplazar por la de los planos.

## Alcance y parámetros

Unidades: kN, m, s; masas en toneladas (kN·s²/m), momentos en kN·m y giros en radianes.
Se reutilizan barras `elasticBeamColumn`, muros `ShellMITC4`, ejes locales y
diafragmas independientes LT1/LT2. No se introducen P–Delta ni materiales
no lineales en el edificio: esa linealidad permite superponer sus respuestas.
La Fiber Section no lineal se analiza por separado.

Cuatro `elasticBeamColumn` de acero SHS 300×300×20 conectan las puntas alineadas
de los voladizos: dos junto a F (`X=10,00` y `17,49 m`) entre Z=7,92 y
11,88 m; G y H entre Z=15,84 y 19,80 m.
Se adoptan E=200 GPa y ν=0,30. Son elementos elásticos con uniones rígidas en
los nodos de punta; no se comprueba aquí pandeo local, global ni conexiones.

La aceleración adoptada es 20% de g y la fracción de Q en
la masa es 50%, según la indicación recibida para este laboratorio.
Se adopta aceleración uniforme por piso. Es un patrón académico editable;
este cálculo no constituye una aplicación completa de NCh433 ni incluye R,
espectro, suelo, importancia o combinaciones normativas.

## A. Carga viva

`q_Q_kN_m2 = null` conserva las intensidades por zona de Semana 2. Un número
en ese campo aplica una intensidad uniforme a las mismas áreas cargadas.
Con zonas distintas se verifica Σ(q_Q,j A_j); con intensidad uniforme, q_Q A.
Se contrastan las cargas transferidas con las áreas de las zonas originales
mediante el verificador geométrico de Semana 2, independientemente de la suma
de receptores. La tolerancia es 0,002 kN por piso por redondeo del contrato.

| Cota [m] | Área origen [m²] | Q origen [kN] | Q transferida [kN] | Error [kN] |
| --- | --- | --- | --- | --- |
| 3.960000 | 711.280506 | 3026.828310 | 3026.828267 | -0.000044 |
| 7.920000 | 1367.873306 | 5427.242434 | 5427.242393 | -0.000042 |
| 11.880000 | 1391.723306 | 5536.081834 | 5536.081785 | -0.000049 |
| 15.840000 | 1494.555506 | 5267.629203 | 5267.629166 | -0.000037 |
| 19.800000 | 1486.376931 | 5346.815901 | 5346.815899 | -0.000002 |

`transferencia_Q.csv` identifica cada losa, receptor (viga o muro), área e intensidad.
Las resultantes de losa se reparten entre los nodos extremos del receptor,
tal como en Semana 2. Se conserva la fuerza total, pero no se reproduce el
diagrama de flexión local de una viga bajo carga distribuida: los esfuerzos
gravitacionales deben interpretarse dentro de esta idealización.

## B. Casos EX y EY

Para cada bloque y piso: W_i=G_i+0,5Q_i, m_i=W_i/g y F_i=0,20W_i.
G contiene la carga permanente de losa/terminaciones, peso propio de muros y,
como adición documentada respecto de Semana 2, peso propio de vigas y columnas.
Para HA se usa γ=24.516625 kN/m³; las cuatro columnas
SHS 300×300×20 usan ρ=7850 kg/m³. Se usan sus volúmenes brutos,
sin descontar intersecciones entre elementos. Las barras aportan la mitad a
cada extremo; los paneles de muro, un cuarto a cada nodo. El peso ubicado en
la base Z=0 (1753.771 kN incluyendo la fracción de Q)
no recibe aceleración de piso. No se usan las masas arbitrarias heredadas.

El CM es el centro de la masa discretizada de cada diafragma. Esta aproximación
usa las resultantes nodales de Semana 2; no calcula el centroide exacto de todos
los polígonos de carga. En el nodo maestro se aplica F más el par de transporte:
EX: Mz=−Fx(yCM−ym); EY: Mz=Fy(xCM−xm). Equivale estáticamente a cargar el CM.
No se añade excentricidad accidental. Los giros calculados se exportan por piso.

| Bloque | Z [m] | Masa [t] | CM X [m] | CM Y [m] | F [kN] |
| --- | --- | --- | --- | --- | --- |
| LT1 | 3.960 | 480.460 | 9.581 | 8.280 | 942.341 |
| LT2 | 3.960 | 848.734 | -17.641 | 8.281 | 1664.647 |
| LT1 | 7.920 | 1270.510 | 21.480 | 7.881 | 2491.890 |
| LT2 | 7.920 | 836.309 | -17.894 | 8.299 | 1640.279 |
| LT1 | 11.880 | 1256.806 | 20.881 | 7.469 | 2465.012 |
| LT2 | 11.880 | 836.309 | -17.894 | 8.300 | 1640.279 |
| LT1 | 15.840 | 1359.122 | 23.810 | 7.539 | 2665.686 |
| LT2 | 15.840 | 842.976 | -17.713 | 8.331 | 1653.355 |
| LT1 | 19.800 | 1505.237 | 24.811 | 7.029 | 2952.267 |
| LT2 | 19.800 | 655.760 | -18.232 | 8.262 | 1286.162 |

Carga lateral total en EX y en EY: **19401.916 kN**.
Corte de apoyos en EX: **19401.915 kN**;
en EY: **19401.917 kN**.
El corte se define como la suma de reacciones externas de todos los apoyos,
incluidos los situados sobre Z=0. No es un corte exclusivo de la sección Z=0.

![Desplazamientos y giros](results/respuesta_sismica.png)

**Condición heredada que requiere contraste con planos:** hay empotramientos en
las cotas [0.0, 3.96, 15.84] m. Los diafragmas incluyen nodos apoyados:
esto explica desplazamientos muy pequeños de algunos pisos y puede inhibir
la torsión. Los vínculos `equalDOF` de muros conectan nodos incluso con separación
geométrica; no equivalen a un brazo rígido con todas sus relaciones de giro.
Se conservan para no alterar silenciosamente el modelo recibido. Pasar los
controles de equilibrio y superposición no valida estas condiciones físicas.

Se emplea `Penalty` con α=1.0e+14. En nodos que también participan en
restricciones multipunto, `nodeReaction` es el residuo Ku−P y no representa
por sí solo la reacción externa. Para cada DOF apoyado se obtiene R=−αu;
se verifica su equilibrio con las fuerzas aplicadas y sensibilidad con α×10.
`*.npz` distingue `nodal_residual` de `reaction` en los apoyos.

## C. Superposición

Combinación de demostración: **R=1.2G + 1.4Q
+ 0.8EX + (-0.3)EY**.
Cada caso parte de `wipe()` y de la misma rigidez. Se eliminan los patrones
automáticos G+Q antes de cargar. R se resuelve nuevamente con la suma explícita
de cargas; no se obtiene del resultado superpuesto para efectuar la comparación.

| Respuesta | Muestra | Superpuesta | Explícita | Error relativo máximo |
| --- | --- | --- | --- | --- |
| desplazamientos | nodo 900116, DOF 3 | -0.0340711279 | -0.0340711279 | 2.36557281e-09 |
| reacciones de apoyo | nodo 400501, DOF 1 | -12606.3075 | -12606.3056 | 1.5013127e-07 |
| fuerzas internas | elemento 15, componente global 3 | 7389.43308 | 7389.43308 | 1.52823703e-08 |

La comparación abarca todos los DOF, todos los apoyos y todas las componentes
de fuerzas nodales resistentes de barras y shells, con tags ordenados.
La tabla muestra una componente de máxima magnitud de cada familia; el error
relativo usa la norma máxima de toda la familia. Fuerzas `eleForce` en ejes
globales: componentes traslacionales en kN y rotacionales en kN·m.
En desplazamientos: DOF 1–3 en m; 4–6 en rad. Los archivos completos permiten
reconstruir cualquier combinación posterior, dentro de la hipótesis lineal.

## D. Columna de hormigón armado

Sección 0.70 × 0.70 m, compatible con A=0,49 m² del modelo.
**Supuestos, no datos verificados de planos:** f'c=30.0 MPa,
fy=420.0 MPa, Es=200000.0 MPa;
12 barras Ø25 mm;
distancia cara–centro de barra 60 mm
(no es recubrimiento libre). As=5890.5 mm²; cuantía=1.202%.

`Concrete01`: compresión negativa, pico −f'c a −0.002, resistencia
residual nula a −0.0035, sin tracción. Se asume hormigón no confinado
en toda la sección, porque no se conoce el detalle de estribos.
`Steel01`: elastoplástico perfecto, b=0. La discretización parte de una malla
40 × 40; descuenta el área ocupada por
las barras de las celdas vecinas e incorpora fibras de acero separadas.
Total de fibras activas: 1600. Error Ac+As−Ag: 5.55e-17 m².

La sección se instancia en OpenSees como `Fiber` y `zeroLengthSection`.
Se aplica P constante y luego curvatura creciente con `DisplacementControl`.
La curva se detiene al cruzar εc=−0.0035 en la cara extrema o
|εs|=0.05; el último paso puede exceder levemente el límite y
se excluye al elegir la capacidad. Se controla equilibrio axial en cada paso.

![Discretización, M–φ y P–M](results/capacidad_HA.png)

| P [kN] (+ compresión) | M máximo [kN·m] | φ al máximo [1/m] |
| --- | --- | --- |
| 0.000000 | 750.602549 | 0.023250 |
| 3375.895981 | 1412.778909 | 0.009250 |
| 6751.791961 | 1500.813897 | 0.005500 |
| 16879.479903 | 0.000000 | 0.000000 |

Los tres puntos de flexocompresión son los máximos M de las curvas OpenSees
antes de alcanzar el límite de deformación. El punto M=0 es el pico de
compresión uniforme de las mismas fibras, a ε=−0.002; por ello no es
la fórmula normativa 0,85f'cAc+fyAs. La línea discontinua solo conecta cuatro
muestras, no define una envolvente completa ni permite diseñar por interpolación.

Interpretación: la compresión moderada aumenta el momento máximo respecto de
P=0, pero reduce la curvatura que puede alcanzarse. Las ramas descendentes
muestran degradación del hormigón. Al acercarse a compresión pura la capacidad
de momento tiende a cero. Son capacidades nominales del modelo de sección:
no incluyen factores de reducción, confinamiento, pandeo de barras, cortante,
esbeltez de columna ni comprobación demanda/capacidad del edificio.

Como comprobación adicional se integra la envolvente de materiales por
compatibilidad de deformaciones (`PM_compatibilidad_envolvente_material.csv`).
Sus valores al límite de deformación no deben confundirse con los picos de
M–φ: Concrete01 considera descarga/recarga en la historia de precarga y flexión.
Al duplicar las divisiones de la malla, el cambio máximo de ese cálculo es
0.101%. Al refinar malla y paso de curvatura, el cambio
máximo de los picos OpenSees es 0.087%.
Error axial máximo: 9.341e-08 kN. Estado HA: **OK**.

## Controles automáticos

| Control | Error | Tolerancia | Estado |
| --- | --- | --- | --- |
| G: equilibrio apoyos / carga | 1.702e-09 | 1.000e-04 | OK |
| G: compatibilidad diafragmas [m] | 6.103e-10 | 1.000e-05 | OK |
| Q: equilibrio apoyos / carga | 2.186e-09 | 1.000e-04 | OK |
| Q: compatibilidad diafragmas [m] | 1.949e-10 | 1.000e-05 | OK |
| EX: equilibrio apoyos / carga | 4.854e-08 | 1.000e-04 | OK |
| EX: compatibilidad diafragmas [m] | 2.254e-10 | 1.000e-05 | OK |
| EX: carga lateral total [kN] | 0.000e+00 | 1.000e-07 | OK |
| EX: corte basal relativo | 4.854e-08 | 1.000e-04 | OK |
| EX: pisos con desplazamiento contrario | 0.000e+00 | 0.000e+00 | OK |
| EX: momento aplicado respecto al CM [kNm] | 0.000e+00 | 1.000e-07 | OK |
| EY: equilibrio apoyos / carga | 5.301e-08 | 1.000e-04 | OK |
| EY: compatibilidad diafragmas [m] | 1.989e-09 | 1.000e-05 | OK |
| EY: carga lateral total [kN] | 0.000e+00 | 1.000e-07 | OK |
| EY: corte basal relativo | 5.285e-08 | 1.000e-04 | OK |
| EY: pisos con desplazamiento contrario | 0.000e+00 | 0.000e+00 | OK |
| EY: momento aplicado respecto al CM [kNm] | 0.000e+00 | 1.000e-07 | OK |
| R: equilibrio apoyos / carga | 2.406e-09 | 1.000e-04 | OK |
| R: compatibilidad diafragmas [m] | 1.493e-09 | 1.000e-05 | OK |
| Q: conservacion piso 3.96 [kN] | 4.364e-05 | 2.000e-03 | OK |
| Q: conservacion piso 7.92 [kN] | 4.168e-05 | 2.000e-03 | OK |
| Q: conservacion piso 11.88 [kN] | 4.855e-05 | 2.000e-03 | OK |
| Q: conservacion piso 15.84 [kN] | 3.678e-05 | 2.000e-03 | OK |
| Q: conservacion piso 19.8 [kN] | 1.961e-06 | 2.000e-03 | OK |
| Superposicion: desplazamientos, error relativo maximo | 2.366e-09 | 1.000e-05 | OK |
| Superposicion: reacciones de apoyo, error relativo maximo | 1.501e-07 | 1.000e-05 | OK |
| Superposicion: fuerzas internas, error relativo maximo | 1.528e-08 | 1.000e-05 | OK |
| EX: sensibilidad penalty x10 | 2.284e-04 | 1.000e-02 | OK |
| EY: sensibilidad penalty x10 | 1.577e-03 | 1.000e-02 | OK |

El comando termina con código distinto de cero si cualquier control resulta
REVISAR. Los supuestos físicos pendientes se mantienen visibles aunque los
controles numéricos estén OK. `manifest.json` registra hashes de entradas y
versiones del entorno para poder identificar la corrida.

## Integración con Unity

El visor de Semana 3 se integró en el proyecto existente
`P1L2/UnityVisualization`; no se creó una segunda escena. El panel izquierdo
mantiene la inspección de geometría y áreas tributarias de Semana 2. El panel
derecho permite seleccionar G, Q, EX, EY o R, ajustar la escala de la deformada,
mostrar fuerzas laterales y centros de masa y consultar dentro de Unity la
discretización Fiber, las curvas M–φ y los primeros puntos P–M.

`P1L3/ejecutar.py` exporta cada corrida a
`P1L2/UnityVisualization/Assets/Resources/semana3_*.csv`. Las líneas coloreadas
son la estructura deformada y se superponen a la geometría original. Los
archivos de respuesta completos siguen disponibles en `P1L3/results/`.

Abrir `P1L2/UnityVisualization/Assets/Main.unity`. Para reconstruir el ejecutable,
usar `Build > Edificio Viewer > Construir EXE`. El resultado queda en
`P1L2/UnityVisualization/Build/EdificioViewer.exe`.

## Reproducción y archivos

Desde la raíz del proyecto, ejecutar:

```powershell
python -m pip install -r P1L3/requirements.txt
python P1L3/ejecutar.py
```

Editar `P1L3/parametros.json` y volver a ejecutar para cambiar q_Q, aceleración,
fracción de Q, combinación o sección. También se admite `--parametros archivo.json`.
Los resultados se regeneran en `P1L3/results/`, este informe en
`P1L3/INFORME.md` y los recursos del visor dentro del Unity existente de P1L2.
Se necesita P1L2 al lado de P1L3. El análisis numérico puede ejecutarse sin
abrir Unity; para visualizar cambios hay que volver a abrir o reconstruir el visor.

Para la demostración: mostrar primero conservación de Q; luego masas, CM,
fuerzas y giros; comparar R con la suma de casos; por último explicar la malla
de fibras, los materiales y los cuatro puntos P–M. Antes de presentar como
modelo validado del edificio, contrastar apoyos y armadura con los planos.

## Referencia de implementación

La configuración `Fiber`/`zeroLengthSection` y el control de curvatura siguen
el procedimiento documentado en [OpenSeesPy: Moment Curvature Analysis](https://openseespydoc.readthedocs.io/en/latest/src/MomentCurvature.html).
La geometría, las cargas y las restricciones provienen de los archivos locales
de P1L2 identificados en el manifiesto.
