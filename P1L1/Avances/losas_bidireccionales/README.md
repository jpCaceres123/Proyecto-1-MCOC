# Herramienta de Losas Bidireccionales

Distribuye la carga de paños rectangulares hacia sus cuatro bordes mediante
líneas tributarias a 45 grados.

## Entrada

Cada paño requiere:

```json
{
  "panel_id": "paño_01_G",
  "length_x_m": 10.00,
  "length_y_m": 8.90,
  "q_kN_m2": 5.10
}
```

El archivo puede contener uno o varios paños dentro de `panels`.

## Ejecución

Desde la carpeta raíz:

```powershell
python .\P1L1\Avances\losas_bidireccionales\slab_bidirectional.py .\P1L1\Avances\losas_bidireccionales\slab_bidirectional_example.json
python .\P1L1\Avances\losas_bidireccionales\plot_slab_distribution.py .\P1L1\Avances\losas_bidireccionales\slab_bidirectional_example_distribution.json
```

Se genera un JSON con cargas lineales en `kN/m`, carga total del paño y
residual de conservación. También se genera una imagen separada por cada
paño, con cuatro gráficos: los dos bordes paralelos a X y los dos bordes
paralelos a Y.

Para un paño más largo en X, los bordes largos reciben una distribución
trapezoidal y los bordes cortos una distribución triangular.
