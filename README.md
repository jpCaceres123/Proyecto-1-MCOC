# Proyecto 1 MCOC

Repositorio del proyecto colaborativo de modelación estructural, OpenSeesPy y visualización Unity.

## Laboratorios

| Laboratorio | Estado | Documentación |
|---|---|---|
| Semana 1: benchmark 3D | Completado | [`P1L1/`](P1L1/) |
| Semana 2-5: edificio, sismo, capacidad y viewer interactivo | En desarrollo | [`Edificio/`](Edificio/) · [Guía Semana 5](Edificio/documentation/SEMANA5_VARIANTES.md) · [`Enunciados`](Enunciados%20e%20Instrucciones/) |
| Semana 6: AR Android con marcadores | APK compilado; prueba física pendiente | [APK Android](Edificio/visualization/android-ar/dist/EdificioAR.apk) · [Marcadores imprimibles](Edificio/visualization/android-ar/markers/imprimir.html) · [Guía AR](Edificio/visualization/android-ar/README.md) |

El modelo global tiene sus fuentes, scripts, resultados y visualizador Unity en
`Edificio/`, separado de P1L1. Los archivos generados deben reconstruirse desde
las fuentes de `Edificio/data/` antes de distribuir una actualización.

## Ejecución del modelo global

Los comandos completos están en [`Edificio/README.md`](Edificio/README.md).

## Ejecución de Semana 1

```powershell
python -m pip install -r .\P1L1\requirements.txt
python .\P1L1\model.py
python .\P1L1\plot_model.py
```

El modelo, los resultados, el visor Unity, los avances y los archivos SAP2000 de Semana 1 están agrupados en [`P1L1/`](P1L1/).

## Documentación

- [Contexto general](Contexto_Proyecto.md)
- [Enunciados](Enunciados%20e%20Instrucciones/)
- [Avances P1L1](P1L1/Avances/)
- [Material SAP2000 P1L1](P1L1/sap2000/)

La guía de Semana 5 detalla la superposición en vivo, las modificaciones de intensidad/sección, las reglas de reanálisis y los verificadores ejecutables.

## Campus jugable en primera persona

- [Proyecto Unity y guía de controles](Edificio/visualization/unity/CampusPlayable/README.md), para Unity **6000.5.11f1**.
- [ZIP Windows](Edificio/visualization/unity/CampusPlayable/Delivery/CampusIngenieria_Windows.zip): extraer completo y abrir `CampusIngenieria/CampusIngenieria.exe`.
- [ZIP del proyecto editable](Edificio/visualization/unity/CampusPlayable/Delivery/CampusIngenieria_Unity.zip).

Incluye el recorrido arquitectónico, terraza y cafetería, láser de consulta de resultados estructurales (tecla 1), AK-47 (tecla 2) y el panel con cargas, diagramas y movimientos. Los resultados corresponden a análisis guardados; la arquitectura del recorrido conserva el contrato estructural original. La versión fue compilada; el recorrido actualizado no tiene una nueva prueba de juego.