# SQ4: ampliación a todas las losas

Finalizado y verificado el 22-09-2026, para escritorio Windows.

La restricción de cuatro losas por nivel queda eliminada. El contrato ahora
incluye los **652 paneles** del modelo, con 318 vigas receptoras en cinco niveles.

## Selección y recorrido

- Selector de nivel (1–5), búsqueda por ID y planta general seleccionable.
- Clic en una superficie del nivel activo para colocar la carga; arrastrar
  conserva la órbita de cámara. Botón Enfocar para recuperar la vista.
- W/A/S/D cruza a losas contiguas. El segmento de movimiento se comprueba
  contra límites de losas y vacíos; no se permite saltar huecos ni juntas.
- El centrado busca una zona válida cuando el centro geométrico cae en un vacío.
- Siguen disponibles magnitud, velocidad visual, reparto, conservación,
  diagramas y deformada amplificada. El par transferido se informa en kN·m.

## Cambio físico autorizado

Se mantiene P(1−η)/Pη en paneles con bordes opuestos. Para bordes subdivididos
se escoge la barra receptora por proyección geométrica, no por orden de lista.
Los otros 485 paneles (9 voladizos y 476 paneles explícitos) utilizan los
receptores que ya tienen asignados en el modelo. Se transfiere la fuerza
vertical y el par `(r_carga − r_receptor) × F`, conservando fuerza y momento.

No se cambian apoyos, secciones, rigideces, restricciones ni las cargas G/Q.
No se agrega una placa FE. La selección de receptor más cercano es una regla
de transferencia idealizada; al pasar entre receptores no conectados puede
haber discontinuidad de respuesta. Se muestra la deformada de las barras,
no una flecha de placa de la losa en voladizo.

## Motor y comprobaciones

Las bases nodales de Fz/Mx/My se resuelven con OpenSees en 310 nodos: 930
soluciones, comprimidas por nodo y cargadas según demanda en Unity. La
superposición usa cargas consistentes de fuerza y par puntual, y corrige las
acciones de extremo de las barras cargadas. La deformada integra los términos
particulares y el momento incluye el salto debido al par.

- Auditoría: **10.680 controles aprobados**.
- Suite completa: **28 pruebas aprobadas**.
- Unity Windows64: compilación exitosa con 6000.5.9f1; `SQ4_RUNTIME_OK`.
  Centros válidos en 652 paneles, 15 categorías/niveles contrastados,
  bloqueo de huecos y juntas aprobado. Error máximo de extremo de deformada:
  2,91×10⁻¹¹ m; error de momento de transferencia en esos casos: 4,59×10⁻⁶ kN·m.
- Comparación fuerza/par con vigas subdivididas, biempotradas y en voladizo.
- Cobertura exacta de los IDs de las 652 losas, rechazo de vacíos, carga cero,
  continuidad de receptores comunes y lectura finita de todas las bases.
- Regeneración: `python Edificio/analysis/load_cases/carga_movil.py`.
- Pruebas: `python -m unittest discover -s Edificio/verification/tests -p "test_*.py"`.

Resultados detallados:
[auditoría](../Edificio/results/verificacion_carga_movil.json),
[pruebas de fuerza y par](../Edificio/verification/tests/test_carga_movil_global.py),
[documentación de la regla](../Edificio/documentation/CARGA_MOVIL.md).

Se mantiene la limitación heredada de vínculos equalDOF no coincidentes:
conservar el momento de la transferencia **no certifica equilibrio global de
momento calculado sólo con apoyos SP**. La respuesta sigue siendo ΔSQ4,
lineal y cuasiestática; no incluye dinámica ni certificación de diseño.

## Capturas del ejecutable

Selección fuera de la franja inicial:

![Losa seleccionada](assets/carga_movil_todas/carga_movil_centro.png)

Voladizo con transferencia excéntrica y planta seleccionable:

![Voladizo](assets/carga_movil_todas/carga_movil_voladizo.png)

Vacíos/juntas en rosa y balance de carga visible:

![Vacíos](assets/carga_movil_todas/carga_movil_vacios.png)

## Ejecutable local

Abrir `Edificio/visualization/unity/UnityVisualization/Build/SQ4-Todas-Losas-2026-09-22/Edificio.exe`.
Se conserva el ejecutable anterior. Los binarios de Unity no se publican;
sí se incluyen fuentes, bases comprimidas y recursos para reconstruirlos.
En el editor: abrir `Assets/Main.unity`, ejecutar Play y elegir **Explorar carga móvil**.
