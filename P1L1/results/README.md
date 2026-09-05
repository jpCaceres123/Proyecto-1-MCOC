# Resultados

Esta carpeta recibe los archivos generados por `python .\P1L1\model.py` y `python .\P1L1\plot_model.py`:

- `nodal_displacements.csv`: seis GDL por nodo.
- `reactions.csv`: seis componentes de reacción por apoyo.
- `element_forces.csv`: fuerzas locales de cada elemento.
- `verification.json` y `verification.md`: verificaciones obligatorias.
- `model.json`: geometria, conectividad, apoyos y desplazamientos para Unity.
- `geometry.png`: geometría y ejes globales.
- `discretization.png`: nodos adicionales generados cada 0.5 m en las vigas superiores.
- `deformed_geometry.png`: geometría original y deformada con escala visual x1000.

Estos archivos son salidas generadas y no deben editarse manualmente. Para
actualizarlos, ejecutar los comandos indicados en `P1L1/README.md`.
