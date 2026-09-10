# Notebooks del proyecto

- `Semana3_LAB.ipynb`: recorrido completo de la Semana 3. Ejecuta el modelo y revisa carga viva, sismo EX/EY, superposición y Fiber Section.

Los cálculos siguen separados en módulos Python para que puedan reutilizarse:

| Etapa | Módulo |
|---|---|
| Casos G/Q/EX/EY/R y reparto | `../casos.py` |
| Masa y sismo pseudoestático | `../sismo.py` |
| Sección Fiber, M–φ y P–M | `../capacidad.py` |
| Orquestación y reportes | `../ejecutar.py` |

Para abrirlo desde `P1L3`:

```powershell
jupyter notebook notebooks/Semana3_LAB.ipynb
```
