# Semana 3

Ejecutar desde la raíz: `python Edificio/analysis/load_cases/ejecutar.py`.
Dependencias: `python -m pip install -r Edificio/requirements.txt`.

El informe generado está en `INFORME.md`; figuras, CSV, respuestas completas
y verificaciones en `results/`. Los parámetros editables están en `parametros.json`.

El visor está integrado en `Edificio/`. Abrir
`Edificio/visualization/unity/UnityVisualization/Assets/Main.unity`. Cada ejecución actualiza sus
recursos `Assets/Resources/semana3_*.csv`.

En Unity, `Ponderadores de masa sísmica` permite editar αG y αQ y pulsar
`Aplicar masa`: m=(αG G+αQ Q)/g. Actualiza EX/EY, el centro de masa y la
combinación R durante la sesión; los λ de R se editan por separado.
`Restablecer masa` recupera los valores de la corrida. Los valores iniciales
se configuran con `ponderador_G_masa` (1.0) y `fraccion_Q_masa` (0.5).

Al seleccionar una columna, el inspector presenta la trazabilidad axial:
compresión del tramo, suma de los pilares conectados inmediatamente encima y
aporte vertical neto del nudo. El archivo completo se exporta a
`results/auditoria_axiales_columnas.csv` y a Resources para Unity. Una
redistribución negativa se informa como tal; no se reemplazan las fuerzas de
equilibrio de OpenSees por una suma artificial de pisos.
La intensidad de sobrecarga q_Q no cambia al modificar su participación αQ.

Se reutilizan sin cambios las fuentes numéricas de geometría y cargas integradas en `Edificio/data/`;
su visor Unity sí fue ampliado para Semana 3. G incorpora adicionalmente peso
propio de barras. Para los pilares se usa el detalle recibido: sección 70×70 cm,
16 barras longitudinales Ø22 y estribos Ø12 cada 10 cm; `f'c=35 MPa` y
`fy=420 MPa`. Un resultado REVISAR produce código de salida 1.
