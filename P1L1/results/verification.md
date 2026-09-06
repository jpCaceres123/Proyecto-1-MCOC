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
| Desplazamiento nodo 7, uz (m) | acortamiento axial N H/(A E) = -5.000000e-05 | -5.000000e-05 | ver referencias independientes abajo |
| Axial elemento 1, extremo i (kN) | simetría: 150/4 = 37.500000 | 37.500000 | ver referencias independientes abajo |
| Momento extremo elemento 5, My-i (kN m) | límite independiente wmax L^2/8 = 56.250000 | 18.060900 | ver referencias independientes abajo |

La primera y segunda filas son referencias independientes de estática global. Las cargas variables se aplican por tramos de 0.5 m, con intensidad media en cada tramo. La distribución usada queda exportada en `results/slab_load_distribution.csv`.

**Criterio:** el equilibrio global pasa si `abs(error) < 1e-8 kN`.

## Referencias independientes

- Desplazamiento vertical: `N H/(A E) = -5.000000e-05 m`; OpenSeesPy: `-5.000000e-05 m`.
- Axial por simetría: `P/4 = 37.500000 kN`; elemento 1: `37.500000 kN`.
- Momento elemento 5: `18.060900 kN*m`; límite independiente `wmax L^2/8 = 56.250000 kN*m`.
- Equilibrio global de seis componentes: residual máximo `9.550e-12`.
