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

## Semana 5 — viewer interactivo y variantes

La guía de uso, los rangos de sliders, las reglas de reanálisis, los verificadores y el flujo de restauración están en [SEMANA5_VARIANTES.md](documentation/SEMANA5_VARIANTES.md). Desde la raíz del checkout: `python Edificio/verification/interactive/verificar_semana05.py` verifica superposición frente a soluciones explícitas; `python Edificio/verification/interactive/verificar_modificaciones.py` ejecuta variantes de carga y sección, y restaura el estado base. La verificación de modificaciones regenera `Edificio/results/` y los recursos Unity; SQ4 debe regenerarse cuando el hash del modelo cambie.

## Semana 6 — aplicación AR Android

La aplicación para identificar vigas y columnas mediante imágenes impresas está en [visualization/android-ar](visualization/android-ar/README.md). El APK se genera en `visualization/android-ar/dist/EdificioAR.apk`; las imágenes para imprimir se seleccionan en `visualization/android-ar/markers/imprimir.html`. Usa los mismos IDs y resultados OpenSees del contrato del visor Unity, con casos G/Q/EX/EY/R, esfuerzos firmados y diagramas por estación. El equipo informó que probó el reconocimiento y la visualización de resultados en un Redmi Note 9 Pro, pero todavía no midió el error espacial de alineamiento ni documentó fecha o condiciones de prueba; ver el [informe de Semana 6](../reports/semana06.md).
