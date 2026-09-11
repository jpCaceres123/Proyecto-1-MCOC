# Curvas P–M de muros en la dirección principal

## Archivos

- `datos/enfierradura_muros.md`: copia íntegra del registro entregado.
- `datos/asignacion_armadura_muros.json`: relación revisable entre los nombres
  del plano y los ID 1–24 del modelo.
- `capacidad_muros.py`: cálculo independiente de las envolventes.
- `results/PM_muros_envolvente.csv`: curva densa para ambos signos de momento.
- `results/PM_muros_puntos_clave.csv`: puntos A–G.
- `results/PM_muros_resumen.csv`: geometría, armadura y capacidades principales.
- `results/capacidad_PM_muros.png`: comparación gráfica de los 24 muros.
- `Assets/Resources/semana3_pm_muros.json`: contrato leído por Unity.

## Sección resistente

La sección de cada muro es un rectángulo de longitud `L` en planta y espesor
`e`. La dirección principal corresponde a flexión en el plano: la deformación
longitudinal cambia a lo largo de `L`.

La armadura vertical participa como acero longitudinal. `D.M.` genera dos
barras por cada posición, una en cada cara. Los refuerzos de borde confirmados
se agregan en los dos extremos. La armadura horizontal se conserva para futuras
comprobaciones de corte, pero no se incorpora a P–M.

Las posiciones verticales se distribuyen entre recubrimientos de 50 mm. El
número de intervalos se redondea hacia arriba, de modo que la separación real
nunca supere la especificada.

## Compatibilidad y equilibrio

Para una profundidad de eje neutro `c` medida desde el borde comprimido:

```text
εs(d) = εcu (c-d) / c
fs = limitar(Es εs, -fy, +fy)
a = mínimo(β1 c, L)
Cc = 0,85 f'c e a
P = Cc + Σ(As fs)
M = Cc(L/2-a/2) + Σ[As fs (L/2-d)]
```

Se emplean `f'c=35 MPa`, `fy=420 MPa`, `Es=210000 MPa`, `εcu=0,003` y
`β1=0,80`. La compresión axial se presenta positiva.

La rama opuesta se obtiene cambiando el signo de M. Esto es válido porque la
malla y los refuerzos de borde modelados son simétricos respecto del centro del
muro. Si un plano posterior indica bordes distintos, deberán calcularse las dos
orientaciones por separado.

## Puntos característicos

- **A:** límite de compresión axial con el factor máximo 0,80.
- **B:** deformación nula en la fibra extrema traccionada de hormigón.
- **C:** acero extremo justo en fluencia, `εt=fy/Es`.
- **D:** estado con `εt=0,003`.
- **E:** estado con `εt=0,005`.
- **F:** flexión pura; se resuelve numéricamente `P=0`.
- **G:** tracción pura de toda la armadura vertical.

La curva densa barre valores de `c`, filtra el límite máximo de compresión y
agrega los estados de compresión y tracción puras.

## Correspondencia con el modelo

El registro entregado advierte que no contiene ID ni coordenadas de
`geometria_manual.json`. Por eso la asignación se mantiene fuera del código de
cálculo y cada relación incluye `confianza` y `criterio_asignacion`.

- 11 asignaciones son de confianza alta: coincidencia clara de eje, longitud o
  espesor.
- 10 son de confianza media: correspondencia geométrica probable o un paño del
  modelo reúne más de un registro.
- 3 son de confianza baja: el modelo simplificado no permite identificar de
  manera única el nombre del plano.

El cálculo no oculta estas diferencias. Unity muestra la confianza al
seleccionar cada muro y pide revisar las asignaciones media/baja antes de un
diseño final.

## Uso en Unity

Los muros se pueden seleccionar desde la geometría o desde el filtro `Muro` del
inspector. El panel muestra:

- nombre asociado del plano y nivel de confianza;
- longitud, espesor y rango de alturas;
- malla vertical y horizontal;
- refuerzos de borde;
- área total de acero vertical;
- envolvente `±M` y valores A–G.

Cuando cambia la armadura con la altura aparece una barra para seleccionar el
segmento correspondiente.

## Reproducción

Desde la raíz del proyecto:

```powershell
python P1L3/ejecutar.py
python -m unittest discover -s P1L3 -p "test_*.py"
```

La primera orden regenera el análisis completo, las curvas de muros y los
recursos de Unity. La segunda verifica que los 24 ID estén asignados, que los
perfiles cubran la altura modelada, que la separación real no exceda la del
plano y que los puntos límite sean físicamente consistentes.
