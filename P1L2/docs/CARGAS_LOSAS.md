# Cargas de Losas P1L2

Documento de referencia para transformar las cargas de `P1L2/Cargas_losas.txt`
en datos estructurados. Todas las coordenadas estan en metros y se expresan como
`(X, Y)`, salvo las pasadas del piso 4, que originalmente incluyen `Z`.

## Convencion

- Origen: `E-3 = (0.00, 0.00)`.
- `X` positivo hacia la derecha del eje E.
- `Y` positivo desde el eje 3 hacia el eje 1.
- Cielos: `z = 3.96`, `7.92`, `11.88`, `15.84` y `19.80 m`.
- Peso propio: `PP_LOSA = espesor_m * 2500 kg/m3`.
- Las cargas `PM_ADIC` y `SC` estan transcritas en `kg/m2`; para el modelo se
  convierten a `kN/m2` multiplicando por `9.80665/1000`.
- Los vertices de cada poligono deben recorrer el perimetro sin cruzarse.

## Resumen de cargas

| Nivel | Zona | PM ADIC (kg/m2) | SC (kg/m2) | Observacion |
|---|---:|---:|---:|---|
| 3.96 | 1 | 260 | 250 | Vacio Zona 1 |
| 3.96 | 2 | 260 | 500 | Zona intermedia |
| 3.96 | 3 | 260 | 300 | Vacio central entre zonas 2 y 3 |
| 7.92 | 1 | 260 | 250 | Vacio Zona 1 |
| 7.92 | 2 | 260 | 500 | Zona ampliada |
| 7.92 | 3 | 260 | 300 | Vacio central entre zonas 2 y 3 |
| 7.92 | 4 | 260 | 400 | Zona inferior |
| 7.92 | 5 | 200 | 200 | Voladizo, `Y=16.50..17.30` |
| 11.88 | 1 | 260 | 250 | Vacio Zona 1 |
| 11.88 | 2 | 260 | 500 | Tres poligonos |
| 11.88 | 3 | 260 | 300 | Vacio central |
| 11.88 | 4 | 260 | 400 | Zona inferior |
| 11.88 | 5 | 200 | 200 | Voladizo |
| 15.84 | 1 | 260 | 250 | Zona inicial |
| 15.84 | 2 | 260 | 500 | Vacio de escalera en poligono 1 |
| 15.84 | 3 | 260 | 300 | Vacio central |
| 15.84 | 4 | 200 | 200 | Zona ampliada |
| 19.80 | 1 | 350 | 100 | Zona central |
| 19.80 | 2 | 260 | 500 | Dos poligonos |
| 19.80 | 3 | 7600 | 800 | Franja interior |
| 19.80 | LT2 | 200 | 200 | Pasadas excluidas |

## Geometria y vacios LT1

Los poligonos y vacios de LT1 se encuentran transcritos con sus vertices
completos en `Cargas_losas.txt`, agrupados por nivel. La regla de modelacion es:

```text
area cargada = poligono exterior - vacios
```

Los vacios no reciben `PP_LOSA`, `PM_ADIC` ni `SC`.

## LT2, cielos 1 a 3

Las cinco zonas LT2 se repiten en:

```text
z = 3.96, 7.92, 11.88 y 15.84 m
```

| Zona | PM ADIC (kg/m2) | SC (kg/m2) |
|---|---:|---:|
| LT2 A | 260 | 500 |
| LT2 B | 260 | 500 |
| LT2 C | 260 | 500 |
| LT2 D | 260 | 300 |
| LT2 E | 260 | 200 |

El vacio de LT2 B tiene vertices:

```text
(-0.80, 10.15)
(-3.22, 10.15)
(-3.22, 12.82)
(-0.80, 12.82)
```

## LT2, cielo del piso 4

Nivel:

```text
z = 19.80 m
```

El poligono cargado es:

```text
(-0.45, -0.25)
(-0.45, 17.30)
(-32.45, 17.30)
(-32.45, -1.09)
(-31.6501, -1.09)
(-31.6501, -0.25)
```

Carga:

```text
PP_LOSA = e * 2500 kg/m3
PM_ADIC = 200 kg/m2
SC      = 200 kg/m2
```

Las seis pasadas son vacios dentro de este poligono. Sus vertices completos
se mantienen en `Cargas_losas.txt` y son:

```text
P1: (-5.500,4.975)  (-3.723,4.975)  (-3.723,6.950)  (-5.500,6.950)
P2: (-5.500,9.050)  (-3.723,9.050)  (-3.723,11.110) (-5.500,11.110)
P3: (-20.500,4.975) (-18.723,4.975) (-18.723,6.950) (-20.500,6.950)
P4: (-22.877,4.975) (-21.100,4.975) (-21.100,6.950) (-22.877,6.950)
P5: (-20.500,9.050)  (-18.723,9.050)  (-18.723,11.110) (-20.500,11.110)
P6: (-22.877,9.050)  (-21.100,9.050)  (-21.100,11.110) (-22.877,11.110)
```

En las pasadas no se aplica ninguna carga.

## Fuente geometrica

`P1L2/Cargas_losas.txt` es la fuente editable de coordenadas. Este documento
resume la semantica y los valores para evitar que la implementacion confunda
zonas cargadas con vacios o pasadas.
