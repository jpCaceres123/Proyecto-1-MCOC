# Edificio

Modelo 3D OpenSeesPy organizado por función. Las unidades son kN, m, s y radianes.

## Comandos

Desde la raíz del proyecto:

```powershell
python -m pip install -r Edificio/requirements.txt
python Edificio/model/builders/convertir_cargas_losas.py
python Edificio/model/builders/generar_modelo_manual.py
python Edificio/analysis/load_cases/ejecutar.py
python Edificio/verification/load_transfer/verificar_reparto_combinacion.py
python Edificio/verification/capacity/verificar_pm_excel.py
python -m unittest discover -s Edificio/verification/tests -p "test_*.py"
```

El ejecutable principal regenera `Edificio/results/` y los recursos de Unity en
`Edificio/visualization/unity/UnityVisualization/Assets/Resources`.

Abrir `Edificio/visualization/unity/UnityVisualization` con Unity 6000.5.10f1
y cargar `Assets/Main.unity`.

Los parámetros están en `Edificio/data/parameters/parametros.json`; se puede
usar otro archivo con `--parametros ruta.json`.
