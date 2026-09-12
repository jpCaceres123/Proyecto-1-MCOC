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
- `scripts/convertir_cargas_losas.py`: convierte el archivo textual de cargas a JSON.
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

`convertir_cargas_losas.py` es una herramienta independiente de importacion; no
forma parte de la generacion del modelo y no debe ejecutarse sobre un JSON que
ya haya sido corregido manualmente.

## Instalacion

Desde la raiz del repositorio:

```powershell
python -m pip install -r .\Edificio\requirements.txt
```

## Regenerar el modelo

```powershell
python .\Edificio\model\builders\generar_modelo_manual.py
python .\Edificio\model\builders\convertir_cargas_losas.py
```

## Ejecutar OpenSeesPy

```powershell
python .\Edificio\model\opensees\modelo_opensees_3d.py
```

El script construye:

- Vigas y columnas como `elasticBeamColumn`.
- Muros como `ShellMITC4`.
- Una `ElasticMembranePlateSection` por espesor de muro.
- Conexiones de borde mediante `equalDOF`.
- Diafragmas rigidos por nivel.
- Cargas de losas sobre vigas y bordes de muro definidos para LT2.
- Peso propio de los muros como carga vertical nodal.

La salida actual de referencia es:

```text
1548 nodos en OpenSees
674 elementos de barras en el contrato
24 muros
169 elementos ShellMITC4
659 paneles de losa
Residual de equilibrio gravitacional: 0.000 kN
Compatibilidad de diafragmas: OK (error maximo 1.099e-4 m)
```

Las columnas de los ejes G, H e I parten respectivamente en `Z = 0.00 m`,
`Z = 3.96 m` y `Z = 3.96 m`. Los primeros nodos de cada columna se modelan
como empotrados.

## Unity

Abrir `Edificio/visualization/unity/UnityVisualization/` con Unity `6000.5.10f1` y ejecutar:

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

Para construir el ejecutable, usar el menu `Build > Edificio Viewer > Construir
EXE`. El script reutiliza la cache incremental de Unity; la primera compilacion
puede tardar mas que las siguientes.

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

## Verificacion actual

La verificacion independiente se ejecuta con:

```powershell
python .\Edificio\verification\load_transfer\verificar_modelo.py
```

El resultado actual es `OK`. Las comprobaciones incluyen:

- Carga total de losa por nivel.
- Particion y suma de areas tributarias.
- Conservacion de cargas permanentes y sobrecargas.
- Losas sin zona de carga.
- Equilibrio gravitacional global.
- Compatibilidad de diafragmas rigidos independientes para LT1 y LT2.

El error maximo de particion cargada es `0.006512 m2`, dentro de la tolerancia
adoptada de `0.01 m2`.
