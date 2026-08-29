# Visor Unity del modelo LT1

Este es un proyecto Unity 6 minimo para visualizar el contrato estructural
`benchmark_lt1/Avances_P1L1/results/unity_model.json`.

## Abrir y ejecutar

1. Abrir Unity Hub.
2. Seleccionar **Add project from disk**.
3. Elegir la carpeta `unity`.
4. Abrir la escena `Assets/Scenes/Main.unity`.
5. Presionar **Play**.

El lector busca primero un `TextAsset` asignado en el Inspector. Si no existe,
busca automaticamente el JSON en:

```text
../results/unity_model.json
```

Por eso la carpeta `unity` debe permanecer dentro de `Avances_P1L1`. Si se
mueve el proyecto Unity, copie el JSON a
`Assets/DataSamples/unity_model.json` y asigne ese archivo al campo
`Model Json` del objeto `StructuralModel`.

## Resultado visual

- Esferas: nodos.
- Esferas rojas: apoyos.
- Columnas: azul oscuro.
- Vigas X: azul.
- Vigas Y: naranjo.
- Cada objeto conserva el `nodeTag` o `elementTag` en su nombre.

La conversion de coordenadas ya viene realizada en el JSON siguiendo el
tutorial: `(X,Y,Z)_OpenSees -> (X,Z,Y)_Unity`.
