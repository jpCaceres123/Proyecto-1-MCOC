# Separación de muros por piso

Los 24 trazados de muro de la geometría fuente se conservan en `source_walls`.
Para el análisis y Unity se generan **82 paños independientes**, cada uno limitado por dos niveles consecutivos.

El ID se forma como `muro_origen × 100 + piso`. Ejemplos:

| Panel | Significado | Cotas |
|---|---|---|
| 101 | Muro origen 1, piso 1 | 0.00–3.96 m |
| 105 | Muro origen 1, piso 5 | 15.84–19.80 m |
| 1002 | Muro origen 10, piso 2 | 3.96–7.92 m |
| 2302 | Muro origen 23, piso 2 | 3.96–7.92 m |

## Flujo

1. `generar_modelo_manual.py` ejecuta `split_walls_by_story()` usando `levels_m`.
2. Conserva `source_wall_id`, piso, longitud y espesor en cada paño.
3. Las cargas de losa dirigidas a muros se reasignan al paño que termina en el nivel cargado.
4. `modelo_opensees_3d.py` genera la malla ShellMITC4 de cada paño. Cada registro de malla contiene un solo intervalo vertical.
5. `capacidad_muros.py` hereda la armadura mediante `source_wall_id`, recorta el perfil a las cotas del paño y exporta una curva P–M asociada al ID de ese piso.
6. Unity recibe 82 registros `W` y permite seleccionar cada paño por separado. El inspector informa sus cotas, altura, muro de origen y piso; la curva P–M corresponde a ese paño.

La separación no agrega juntas ni libera desplazamientos entre pisos. Los paños consecutivos comparten coordenadas/nodos en su borde, de modo que el muro sigue siendo continuo estructuralmente. Solo los muros que físicamente comienzan sobre una losa conservan el vínculo inferior especial; este vínculo no se repite en los pisos siguientes. La modificación separa entidades, cargas, trazabilidad y selección por nivel.

## Verificaciones

- Todos los paños abarcan exactamente un intervalo entre niveles.
- El área de elevación acumulada de cada muro origen se conserva.
- Toda carga de losa sobre muro llega al paño inmediatamente inferior.
- El análisis completo G, Q, EX, EY y R termina en estado `OK`.
- La conservación de cargas y el equilibrio global permanecen dentro de tolerancia.
- Las curvas P–M cubren los 24 muros origen y se exportan por paño.

En la ejecución final se obtuvieron **169 ShellMITC4**, todos con altura vertical máxima de **3,96 m**, asociados de forma única a los 82 paños. Las 22 pruebas automatizadas terminaron correctamente. La compilación de comprobación de Unity también terminó con código 0; su log está en `Edificio/documentation/semana05_evidencias/muros_por_piso_unity.log`.

La tabla completa está en `Edificio/results/auditoria_muros_por_piso.csv`. La geometría fuente no se duplicó manualmente: la separación es automática y se repite cada vez que se regenera el modelo.

El modelo anterior ya subdividía los shells en franjas entre niveles, por lo que no correspondía asignar un elemento Shell único a toda la altura. El problema restante era que todas esas franjas compartían el mismo ID de muro en los datos, la capacidad y la interfaz. Esta corrección hace explícita la identidad por piso sin alterar artificialmente la rigidez o introducir discontinuidades. Por esa razón, la respuesta global debe permanecer prácticamente igual; el cambio correcto aparece en la identificación y consulta por piso, no como una variación artificial de desplazamientos.
