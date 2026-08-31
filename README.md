# Proyecto Modelo Edificio 1

Modelo estructural 3D generado desde geometria manual, con vigas, columnas, muros, losas, cargas tributarias y visualizadores.

## Estructura

- `data/`: geometria editable del modelo.
- `scripts/`: generadores, exportadores y modelo OpenSeesPy.
- `outputs/`: archivos generados JSON y Excel.
- `visualizers/`: visualizadores HTML 2D y 3D.
- `docs/`: instrucciones del modelo.
- `Planos/` y `Planos PDF/`: planos base del edificio.
- `UnityVisualization/`: proyecto Unity para visualizacion.

## Flujo de trabajo

Editar solo `data/geometria_manual.json` y regenerar:

```powershell
python scripts/generar_modelo_manual.py
python scripts/exportar_losas_excel.py
python scripts/crear_visualizador_modelo.py
python scripts/crear_visualizador_3d_modelo.py
python scripts/modelo_opensees_3d.py
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
python -m pip install -r requirements.txt
```
