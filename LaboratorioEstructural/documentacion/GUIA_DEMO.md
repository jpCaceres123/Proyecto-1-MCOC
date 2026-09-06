# Guía breve de demostración

Duración sugerida: 10–15 minutos. Abrir la aplicación y conservar el informe numérico disponible.

| Paso | Acción | Qué explicar o comprobar |
|---|---|---|
| 1 | Mostrar ambos edificios y activar muros | LT1 en azul, LT2 en naranja; muros en violeta. Diferenciar geometría física y barras equivalentes. |
| 2 | Elegir una planta y activar diafragmas | Cada edificio tiene un diafragma independiente por nivel. La junta no se atraviesa con restricciones ni barras. |
| 3 | Activar nodos, IDs y ejes locales | Un nodo analítico tiene seis GDL; la entidad gráfica permite identificar su nodeTag/elementTag. |
| 4 | Seleccionar una viga y abrir Cargas | Mostrar área tributaria, qG, qQ, fuerza total y polígonos disponibles. Explicar la fuente tabular y sus asignaciones pendientes. |
| 5 | Activar G, deformada y Mz | La deformada está amplificada. El diagrama usa ejes locales y las cargas calculadas en OpenSees. |
| 6 | Seleccionar un nodo de apoyo | Mostrar las reacciones combinadas en los seis GDL globales. Contrastarlas con el equilibrio del informe. |
| 7 | Pulsar Prueba y variar EX/EY | Mostrar superposición. La combinación de referencia tiene una corrida explícita independiente registrada. |
| 8 | Abrir Sección | Comparar P-M de columna/muro, demanda y curva Whitney. Cambiar a M-phi y distinguir no linealidad de sección de linealidad global. |
| 9 | Abrir Cambios, reducir E y recalcular | La rigidez cambia, por lo que Python reconstruye y resuelve. Para E×0,8 con el resto fijo, la gravedad produce desplazamientos ×1,25. Restaurar E=1 al terminar. |
| 10 | Cambiar una dimensión y recalcular | A e inercias se recomputan; también cambia el peso propio. Revisar efectos físicos de nuevas secciones sobre la junta nominal. |
| 11 | AR con cámara en un lugar fijo | Alinear con referencias reales. Explicar que es registro manual, sin seguimiento espacial automático. |

## Preguntas que el grupo debe poder responder

- ¿Por qué no se importan todos los nodos de polígonos como nodos libres de OpenSees?
- ¿Por qué las hojas auxiliares de cargas no se suman otra vez a CasosCargaViga?
- ¿Qué datos reales faltan para confirmar la capacidad de las secciones de demostración?
- ¿Por qué un cambio de combinación es inmediato y un cambio de E requiere reanálisis?
- ¿Qué cambió al convertir los muros del libro a barras verticales por centroide?
- ¿Qué comprueba el equilibrio y qué errores de modelación puede no detectar?
- ¿Qué representa cada curva y qué mecanismos resistentes quedan fuera de la Fiber Section?
- ¿Cómo se transforma `(X,Y,Z)` de OpenSees a `(X,Z,Y)` en Unity y por qué no se copian directamente las rotaciones como Euler angles?

Las capturas incluidas muestran la aplicación realmente ejecutada. No constituyen evidencia de una prueba AR en el edificio ni de la validación del armado real.
