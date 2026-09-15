# Indice de resultados

Esta carpeta contiene los archivos generados por el analisis estructural y los
archivos que consume el visor Unity. Actualmente se mantienen en un mismo nivel
porque los scripts de Python, las pruebas y Unity esperan estas rutas exactas.
No mover ni renombrar archivos sin actualizar primero esos consumidores.

## Regenerar resultados

Desde la raiz del proyecto:

```powershell
python Edificio/analysis/load_cases/ejecutar.py
```

La ejecucion sobrescribe los resultados principales, actualiza el contrato de
Unity y vuelve a generar las figuras y verificaciones. Para revisar el estado
del calculo se deben inspeccionar `resumen_global.json`,
`verificaciones_globales.csv` y los archivos de capacidad.

## 1. Contratos y geometria

| Archivo | Contenido |
| --- | --- |
| `modelo_3d_manual.json` | Contrato principal para OpenSees y Unity: nodos, elementos, losas, muros, apoyos y cargas transferidas. |
| `manifest.json` | Hashes y manifest de los archivos exportados para Unity. |
| `apoyos_heredados.csv` | Apoyos o restricciones heredadas usadas al construir el modelo. |
| `nodos_modelo_manual.xlsx` | Tabla legible de nodos y coordenadas. |
| `losas_modelo.xlsx` | Tabla legible de losas y zonas de carga. |
| `auditoria_muros_por_piso.csv` | Auditoria de los 82 paños de muro separados por nivel. |

El archivo mas importante de este grupo es `modelo_3d_manual.json`. Unity no
debe alimentarse editando manualmente la escena: primero se regenera este
archivo y luego se abre o reinicia la escena.

## 2. Casos de carga

Cada caso tiene normalmente tres archivos:

- `*_pisos.csv`: respuesta resumida por piso.
- `*_fuerzas.json`: fuerzas y respuestas exportadas del caso.
- `*_fuerzas_locales.json`: fuerzas locales de elementos de barra.
- `*.npz`: resultados numericos completos usados por los verificadores.

| Caso | Significado |
| --- | --- |
| `G` | Carga permanente y peso propio. |
| `Q` | Carga viva. |
| `EX` | Sismo pseudoestatico en X. |
| `EY` | Sismo pseudoestatico en Y. |
| `R` | Combinacion de referencia. |
| `EXG`, `EXQ` | Componentes de EX asociadas a G y Q. |
| `EYG`, `EYQ` | Componentes de EY asociadas a G y Q. |

Los casos `EXG`, `EXQ`, `EYG` y `EYQ` permiten modificar los ponderadores de
masa y reconstruir la respuesta sismica por superposicion. No son casos
adicionales independientes del modelo, sino componentes auxiliares.

## 3. Cargas y sismo

| Archivo | Contenido |
| --- | --- |
| `transferencia_Q.csv` | Transferencia de carga viva desde losas hacia vigas y muros. |
| `conservacion_Q.csv` | Comparacion entre carga viva de origen y carga transferida por nivel. |
| `masas_y_sismo.csv` | Masa, centro de masa y fuerzas sismicas por piso. |
| `sismo_por_piso.csv` | Resumen de la distribucion de fuerzas sismicas. |
| `respuesta_sismica.png` | Grafico de desplazamientos y giros sismicos. |

Para verificar el reparto de cargas se debe comenzar por `transferencia_Q.csv`
y `conservacion_Q.csv`. Para revisar la accion sismica se debe comenzar por
`masas_y_sismo.csv` y `sismo_por_piso.csv`.

## 4. Verificaciones globales

| Archivo | Contenido |
| --- | --- |
| `resumen_global.json` | Estado resumido de los casos y controles principales. |
| `verificaciones_globales.csv` | Equilibrio, conservacion y controles numericos globales. |
| `comparacion_superposicion.csv` | Comparacion entre respuesta superpuesta y solucion explicita. |
| `auditoria_axiales_columnas.csv` | Trazabilidad axial de columnas y aportes de elementos conectados. |
| `verificacion_demanda_capacidad.json` | Resumen total de elementos revisados y estados de capacidad. |
| `verificacion_demanda_capacidad.csv` | Detalle elemento por elemento de demanda, capacidad y utilizacion. |

Estos archivos son los primeros que se deben consultar para saber si una
corrida termino correctamente. Un resultado `OK` no reemplaza la revision de
las hipotesis estructurales, pero indica que los controles implementados no
detectaron una inconsistencia numerica.

## 5. Capacidad de hormigon armado

| Archivo | Contenido |
| --- | --- |
| `capacidad_HA.png` | Curvas y resumen grafico de capacidad de columnas/secciones HA. |
| `fibras.csv` | Discretizacion de fibras de la seccion de referencia. |
| `momento_curvatura.csv` | Relacion momento-curvatura. |
| `PM_puntos.csv` | Puntos principales de la curva P-M de la seccion de referencia. |
| `resumen_capacidad.json` | Resumen de capacidad de columnas y secciones HA. |

Estos resultados corresponden principalmente a columnas y a la seccion de
hormigon armado de referencia. La grafica es evidencia visual; la tabla JSON o
CSV es la fuente para revisar valores numericos.

## 6. Capacidad de muros

| Archivo | Contenido |
| --- | --- |
| `PM_muros_envolvente.csv` | Curva P-M completa de los muros. |
| `PM_muros_puntos_clave.csv` | Puntos caracteristicos A-G de las curvas. |
| `PM_muros_resumen.csv` | Geometria, armadura y capacidades principales. |
| `PM_compatibilidad_envolvente_material.csv` | Controles de compatibilidad de material y envolvente. |
| `PM_muros_unity.json` | Curvas y datos preparados para el inspector de Unity. |
| `resumen_capacidad_muros.json` | Resumen de muros de origen y paños por piso. |
| `demanda_muros.csv` | Demandas extraidas para la verificacion de muros. |
| `verificacion_demanda_capacidad.*` | Resultado combinado de columnas y muros. |
| `capacidad_PM_muros.png` | Grafico de envolventes P-M de muros. |

La documentacion del procedimiento esta en
`Edificio/documentation/METODO_PM_MUROS.md` y la explicacion de la separacion
por piso en `Edificio/documentation/muros_por_piso.md`.

## 7. Que consultar segun la pregunta

| Pregunta | Archivo inicial |
| --- | --- |
| Que geometria ve Unity? | `modelo_3d_manual.json` |
| La corrida termino bien? | `resumen_global.json` y `verificaciones_globales.csv` |
| Como se repartio Q? | `transferencia_Q.csv` y `conservacion_Q.csv` |
| Que fuerzas sismicas se aplicaron? | `masas_y_sismo.csv` y `sismo_por_piso.csv` |
| Coincide la superposicion? | `comparacion_superposicion.csv` |
| Que capacidad tiene una columna? | `resumen_capacidad.json`, `PM_puntos.csv` y `capacidad_HA.png` |
| Que capacidad tiene un muro? | `PM_muros_resumen.csv`, `PM_muros_unity.json` y `capacidad_PM_muros.png` |
| Cumplen los elementos? | `verificacion_demanda_capacidad.json` y `.csv` |
| Que datos usa Unity? | `manifest.json` y `PM_muros_unity.json` |

## 8. Archivos auxiliares y formato

- Los archivos `.npz` contienen arreglos numericos completos para los
  verificadores; no estan pensados para lectura manual.
- Los archivos `.json` contienen contratos, resumenes o datos estructurados.
- Los archivos `.csv` contienen tablas para inspeccion, filtros y auditorias.
- Los archivos `.xlsx` son exportaciones legibles de geometria, no la fuente
  principal del modelo.
- Los archivos `.png` son figuras generadas, no entradas del analisis.

Los resultados son generados y pueden cambiar al ejecutar el modelo. Por eso no
se deben editar manualmente para corregir una verificacion; se debe modificar
la entrada o el codigo correspondiente y regenerar todo el conjunto.
