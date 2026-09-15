# Diagramas, ejes locales y deformada

Seleccionar una viga o columna abre el selector `N / Vy / Vz / My / Mz` del
inspector. Se muestra un diagrama con unidades y valores extremos, además de
su representación sobre la barra original. `Ocultar` elimina ese diagrama.
La longitud gráfica máxima de 1,5 m se normaliza para cada selección; no debe
usarse el tamaño gráfico para comparar capacidades entre elementos.

N es positivo en compresión. Vy, Vz, My y Mz se expresan como acciones sobre
la cara de corte de normal +x local: en i se usa menos la acción nodal de i;
en j se usa la acción nodal de j. Para N se invierten ambos signos. Las acciones
originales de extremo siguen visibles en su tabla, con su convención original.
Los diagramas recorren el elemento de i a j. El offset gráfico de N/Vy/Mz es
y local; el de Vz/My es z local. Azul indica positivo y rojo negativo.

El modelo actual aplica todas las cargas de barras a los nodos. Por ello N y V
son constantes (salvo redondeo) y los momentos son lineales entre nodos. La
interpolación de extremos es válida para esa idealización, no para una futura
barra con carga distribuida o puntual interior sin subdivisión. En ese caso se
debe extender el contrato y el evaluador antes de reutilizar los diagramas.

Los ejes se consultan directamente con las respuestas `xlocal`, `ylocal` y
`zlocal` de los elementos OpenSees. Rojo=x, verde=y, azul=z. Se transforma cada
vector por separado de XYZ a XZY: recalcular productos vectoriales después
del cambio de coordenadas invertiría la orientación por cambio de handedness.

La deformada de barras usa interpolación cúbica de Hermite para flexión, con
desplazamientos y giros nodales en coordenadas globales OpenSees; el axial se
interpola linealmente. La pendiente transversal es `theta × ex`. Es coherente
con barras Euler–Bernoulli elásticas sin carga interior. El giro torsional no
altera el eje central mostrado. Los bordes de los 169 shells se dibujan con las
traslaciones de sus nodos, en cian; no se representa una superficie curva
interior ni un campo de deformaciones/tensiones de shell.

Todos los resultados corresponden al caso activo. EX/EY se reconstruyen con
las bases de masa EXG/EXQ/EYG/EYQ; R combina G,Q,EX,EY con los ponderadores
activos, incluyendo giros y desplazamientos de nodos de muro.

Las demandas P-M de muro se exportan para los casos G, Q, EX, EY y R. Para cada
tramo vertical se suman las fuerzas globales de los nodos del borde inferior de
sus shells y el momento resultante se proyecta sobre el eje transversal a la
longitud del muro. Los tramos de pisos conservan los nodos compartidos en sus
interfaces, por lo que esta separación es de identificación y postproceso, no
una desconexión estructural. El punto se muestra en verde sobre la envolvente
nominal del segmento seleccionado. Esta es una resultante compatible con la
malla ShellMITC4; no reemplaza un diseño completo de muro ni incluye factores
de reducción, interacción biaxial, corte o efectos de segundo orden.

## Exportación

`visualization/exports/exportar_resultados_unity.py` genera
`Assets/Resources/semana4_resultados.json` a partir del modelo y los nueve NPZ
existentes. Exporta 629 nodos analíticos, 612 barras, 169 shells, ejes y seis
grados de libertad por nodo y caso. Comprueba topología, ortonormalidad y
valores finitos. También exporta metadatos de sección, material y restricciones
de barras, además de demandas P-M de muros para los cinco casos activos. No
ejecuta un nuevo análisis. El flujo `ejecutar.py` también
invoca esta exportación después de generar los casos.

Si cambia el edificio, deben regenerarse análisis y recursos juntos. Estos
cambios de postproceso no corrigen apoyos, vínculos, cargas ni armaduras.
Los diagramas N/V/M del inspector corresponden a barras. La demanda P-M de muro
se obtiene como resultante del borde inferior de sus shells; no es un diagrama
de esfuerzos distribuido sobre toda la superficie del muro.
