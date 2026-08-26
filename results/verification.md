# Verificación automática

Caso ejecutado con OpenSeesPy en unidades kN-m.

| Magnitud | Referencia independiente | OpenSeesPy | Error |
|---|---:|---:|---:|
| Carga vertical total (kN) | 300.000000 | 300.000000 | 0.000000 |
| Suma de reacciones Z (kN) | 300.000000 | 300.000000 | 5.684e-14 |
| Desplazamiento nodo 7, uz (m) | revisar estimación elástica | -1.000000e-04 | n/a |
| Axial elemento 1, extremo i (kN) | simetría: 300/4 = 75.000000 | 75.000000 | n/a |
| Momento extremo elemento 5, My-i (kN m) | viga fija: abs(wL²/12) = 37.500000 | 25.047540 | n/a |

La primera y segunda filas son referencias independientes de estática global. Para la defensa, la estimación de viga fija de la última fila sirve como orden de magnitud; el marco redistribuye momentos entre columnas y vigas, por lo que no se espera igualdad exacta.

**Criterio:** el equilibrio global pasa si `abs(error) < 1e-8 kN`.
