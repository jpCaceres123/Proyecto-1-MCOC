# P1L2: Modelo del Edificio 1

Avance de Semana 2: modelo estructural 3D generado desde geometría manual, con vigas, columnas, muros, losas, cargas tributarias y visualizadores.

## Estructura

- `data/`: geometria editable del modelo.
- `scripts/`: generadores, exportadores y modelo OpenSeesPy.
- `outputs/`: archivos generados JSON y Excel.
- `visualizers/`: visualizadores HTML 2D y 3D.
- `docs/`: instrucciones del modelo.
- `UnityVisualization/`: proyecto Unity para visualizacion.
- Los planos de referencia permanecen en la carpeta local `Planos/` del repositorio y no se duplican dentro de este laboratorio.

## Flujo de trabajo

Desde la raíz del repositorio, editar solo `P1L2/data/geometria_manual.json` y regenerar:

```powershell
python .\P1L2\scripts\generar_modelo_manual.py
python .\P1L2\scripts\exportar_losas_excel.py
python .\P1L2\scripts\crear_visualizador_modelo.py
python .\P1L2\scripts\crear_visualizador_3d_modelo.py
python .\P1L2\scripts\modelo_opensees_3d.py
```

## Archivos generados principales

- `outputs/modelo_3d_manual.json`
- `outputs/nodos_modelo_manual.xlsx`
- `outputs/losas_modelo.xlsx`
- `visualizers/visualizador_modelo.html`
- `visualizers/visualizador_modelo_3d.html`
- `UnityVisualization/Assets/Resources/model_3d.csv`

## Dependencias

```powershell
python -m pip install -r .\P1L2\requirements.txt
```

El proyecto Unity usa la versión `6000.5.10f1` y la escena `UnityVisualization/Assets/Main.unity`.

El script `modelo_opensees_3d.py` construye el modelo y aplica cargas, pero el avance actual todavía requiere revisar conectividad, malla de losas, muros analíticos, secciones, apoyos y equilibrio antes de considerarse una solución estructural definitiva.
