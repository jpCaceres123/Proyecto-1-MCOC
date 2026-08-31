# Visualizador Unity

1. Crear un proyecto Unity 2022.3 LTS o posterior.
2. Copiar `Assets/Scripts/BuildingVisualizer.cs` y `Assets/Resources/model_3d.csv`.
3. Crear una escena, agregar un `GameObject` vacio y adjuntar `BuildingVisualizer`.
4. Ejecutar la escena. Columnas aparecen en rojo, vigas en azul y nodos en amarillo.

El CSV es generado por `generar_modelo_desde_excel.py` usando
`nodos_modelo_opensees.xlsx`. No editarlo manualmente: modificar la hoja de
nodos y volver a ejecutar el generador.
