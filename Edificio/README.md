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

## Laboratorio Semana 5

Informe y limitaciones: [reports/semana05.md](../reports/semana05.md).

```powershell
python Edificio/verification/interactive/verificar_semana05.py
python Edificio/verification/interactive/verificar_modificaciones.py
```

El segundo comando ejecuta dos variantes y restaura los resultados base al terminar.
Para iPhone 15 / iOS 26 se agregó el menú Unity
`Build > Edificio Viewer > Exportar iOS (iPhone 15)`.
La exportación requiere iOS Build Support y la aplicación final requiere Xcode en macOS.
El intento local quedó bloqueado por ausencia del módulo; no hay IPA generada.

La interfaz móvil se puede probar en Play con `View > Vista para celular`.
Uso, cambios y verificación: [interfaz_celular.md](../reports/interfaz_celular.md).
