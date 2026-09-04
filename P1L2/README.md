# P1L2: Modelo 3D del edificio

Modelo global del edificio de Ingenieria con vigas, columnas, muros, cargas
gravitacionales, areas tributarias y visualizacion en Unity.

## Objetivo

El modelo representa la estructura completa en unidades de kN, m y s.
Las losas no se modelan con elementos finitos: se representan geometricamente
y sus cargas se transfieren a las vigas mediante areas tributarias.
Los muros se modelan analiticamente en OpenSeesPy con elementos `ShellMITC4`.

## Archivos principales

### Fuentes editables

- `data/geometria_manual.json`: niveles, ejes, vigas, columnas, muros y voladizos.
- `data/cargas_losas.json`: zonas, vacios, cargas muertas y sobrecargas.
- `data/Cargas_losas.txt`: fuente original de las coordenadas de cargas.

### Codigo

- `scripts/generar_modelo_manual.py`: genera el modelo, las losas, el CSV y los Excel.
- `scripts/modelo_opensees_3d.py`: construye y analiza el modelo en OpenSeesPy.
- `UnityVisualization/Assets/Scripts/BuildingVisualizer.cs`: visor 3D de Unity.

### Archivos generados

- `outputs/modelo_3d_manual.json`: contrato del modelo generado.
- `outputs/nodos_modelo_manual.xlsx`: nodos y elementos.
- `outputs/losas_modelo.xlsx`: losas, vacios y cargas tributarias.
- `UnityVisualization/Assets/Resources/model_3d.csv`: archivo que lee Unity.
- `visualizers/visualizador_modelo.html`: visor HTML.
- `visualizers/visualizador_modelo_3d.html`: visor HTML 3D.

No se deben editar directamente los archivos generados. Se regeneran a partir
de los archivos de `data/`.

## Instalacion

Desde la raiz del repositorio:

```powershell
python -m pip install -r .\P1L2\requirements.txt
```

## Regenerar el modelo

```powershell
python .\P1L2\scripts\generar_modelo_manual.py
python .\P1L2\scripts\exportar_losas_excel.py
python .\P1L2\scripts\crear_visualizador_modelo.py
python .\P1L2\scripts\crear_visualizador_3d_modelo.py
```

## Ejecutar OpenSeesPy

```powershell
python .\P1L2\scripts\modelo_opensees_3d.py
```

El script construye:

- Vigas y columnas como `elasticBeamColumn`.
- Muros como `ShellMITC4`.
- Una `ElasticMembranePlateSection` por espesor de muro.
- Conexiones de borde mediante `equalDOF`.
- Diafragmas rigidos por nivel.
- Cargas de losas sobre vigas.
- Peso propio de los muros como carga vertical nodal.

La salida actual de referencia es:

```text
746 nodos en OpenSees
677 elementos de barras en el contrato
24 muros
169 elementos ShellMITC4
176 paneles de losa
Residual de equilibrio gravitacional: 0.000 kN
```

## Unity

Abrir `P1L2/UnityVisualization/` con Unity `6000.5.10f1` y ejecutar:

```text
Assets/Main.unity
```

El visor lee `Assets/Resources/model_3d.csv`. Permite activar o desactivar:

- Nodos.
- Vigas.
- Columnas.
- Muros.
- Apoyos.
- Losas y diafragmas.
- IDs.
- Ejes locales.
- Areas tributarias.

Los colores son:

- Vigas: azul.
- Columnas: rojo.
- Muros: gris.
- Losas: azul claro.
- Nodos: amarillo.

Los vacios se exportan como registros `V` y Unity divide visualmente las losas
alrededor de ellos. Los muros se dibujan como solidos grises; no se dibujan los
registros analiticos `WALL` como vigas azules.

## Convenciones

- OpenSees X corresponde a Unity X.
- OpenSees Y corresponde a Unity Z.
- OpenSees Z corresponde a Unity Y.
- Eje local rojo: `x'`, direccion longitudinal del elemento.
- Eje local verde: `y'`.
- Eje local azul: `z'`.

## Entrega

Para la entrega se deben incluir, como minimo:

- `scripts/modelo_opensees_3d.py`.
- `data/geometria_manual.json`.
- `data/cargas_losas.json`.
- `outputs/modelo_3d_manual.json`.
- `UnityVisualization/Assets/Resources/model_3d.csv`.
- El proyecto `UnityVisualization/`.

El repositorio debe acompanarse en Canvas con el enlace y el hash exacto del
commit evaluado. La demostracion se realiza en vivo y se debe poder explicar la
geometria, las cargas, los apoyos, los ejes locales y la transferencia de cargas.

## Pendientes de validacion

El modelo corre y conserva el equilibrio gravitacional, pero antes de
considerarlo definitivo se deben cotejar con los planos:

- Geometria exacta de vigas, muros, losas y voladizos.
- Bordes de las zonas no rectangulares.
- Dimensiones de los vacios.
- Conectividad de los muros ShellMITC4 con la estructura.
- Secciones, apoyos y comportamiento bajo cargas laterales.
