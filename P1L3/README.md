# Semana 3

Ejecutar desde la raíz: `python P1L3/ejecutar.py`.
Dependencias: `python -m pip install -r P1L3/requirements.txt`.

El informe generado está en `INFORME.md`; figuras, CSV, respuestas completas
y verificaciones en `results/`. Los parámetros editables están en `parametros.json`.

El visor está integrado en el proyecto Unity existente de P1L2. Abrir
`P1L2/UnityVisualization/Assets/Main.unity`. Cada ejecución actualiza sus
recursos `Assets/Resources/semana3_*.csv`.

Se reutilizan sin cambios las fuentes numéricas de geometría y cargas de P1L2;
su visor Unity sí fue ampliado para Semana 3. G incorpora adicionalmente peso
propio de barras. Los apoyos heredados y las hipótesis de armadura se
documentan en el informe; los controles numéricos no validan su correspondencia
con el edificio real. Un resultado REVISAR produce código de salida 1.
