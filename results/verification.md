# Verificación automática

Caso ejecutado con OpenSeesPy en unidades kN-m. La losa se descarga en dos direcciones mediante tributación a 45 grados: cargas trapezoidales en las vigas largas y triangulares en las vigas cortas.

## Cargas de losa

- Losa: `6.0 m x 5.0 m`.
- Carga superficial: `q = 5.0 kN/m2`.
- Ancho tributario máximo: `2.5 m`.
- Carga máxima: `wmax = q * 2.5 = 12.5 kN/m`.
- Vigas de 5 m: triangular `0 -> 12.5 -> 0 kN/m`.
- Vigas de 6 m: trapezoidal `0 -> 12.5 -> 12.5 -> 0 kN/m`.

| Magnitud | Referencia independiente | OpenSeesPy | Error |
|---|---:|---:|---:|
| Carga vertical total (kN) | 150.000000 | 150.000000 | 0.000000 |
| Suma de reacciones Z (kN) | 150.000000 | 150.000000 | 1.933e-12 |
| Desplazamiento nodo 7, uz (m) | revisar estimación elástica | -5.000000e-05 | n/a |
| Axial elemento 1, extremo i (kN) | simetría: 150/4 = 37.500000 | 37.500000 | n/a |
| Momento extremo elemento 5, My-i (kN m) | revisar con SAP2000 | 18.060900 | n/a |

La primera y segunda filas son referencias independientes de estática global. Las cargas variables se aplican por tramos de 0.5 m, con intensidad media en cada tramo. La distribución usada queda exportada en `results/slab_load_distribution.csv`.

**Criterio:** el equilibrio global pasa si `abs(error) < 1e-8 kN`.
