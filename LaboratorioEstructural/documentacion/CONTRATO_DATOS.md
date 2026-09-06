# Intercambio OpenSees–Unity

`resultados/proyecto.json`, esquema 1, se copia a StreamingAssets. Unity utiliza clases serializables y JsonUtility. La publicación usa un archivo temporal y reemplazo final; no se comunica mediante servidor.

- `model.nodes`: id, x/y/z en metros, edificio, tipo y condición de apoyo.
- `model.elements`: id analítico, source del Excel, wall físico, nodos i/j, tipo, sección, longitud, ejes locales e identificación del edificio.
- `model.walls`: geometría física para mostrar muros; no implica elementos adicionales en el solver.
- `model.diaphragms`: nodo maestro, nodos restringidos, posición, nivel, peso y fuerza lateral idealizada.
- `model.slabs` y `model.tributaries`: geometría gráfica y polígonos de reparto. No son elementos finitos de placa.
- `cases`: G, Q, EX y EY. Cada caso contiene desplazamientos de seis GDL, reacciones de apoyos, fuerzas locales de extremo y 21 estaciones de diagrama por elemento.
- `capacities`: secciones representativas, curva P-M, curva M-phi, comparación Whitney y verificación independiente.
- `loadRows`: aportes de CasosCargaViga para inspección de áreas y cargas del elemento de origen.
- `validation`: resultados de equilibrio, compatibilidad y superposición, además del hash SHA-256 del Excel leído.
- `notes`: hipótesis que la interfaz muestra en la pestaña Notas.
- `projectRoot` y `pythonExecutable`: rutas locales generadas por el solver para permitir reanálisis desde la aplicación Windows. Al mover de equipo se regeneran con una corrida local.

Desplazamientos: `[Ux, Uy, Uz, Rx, Ry, Rz]`, con traslaciones en m y giros en radianes. Fuerzas locales de extremo: `[Fx_i, Fy_i, Fz_i, Mx_i, My_i, Mz_i, Fx_j, Fy_j, Fz_j, Mx_j, My_j, Mz_j]`.

Diagramas: N positivo en tracción de la sección; Vy, Vz, T, My y Mz siguen la convención interna de corte definida en solver.py. Se verifica que el extremo derecho del diagrama reproduzca las seis fuerzas locales nodales del extremo j.

Para demanda-capacidad se usa P=Fx_i como compresión positiva del elemento vertical de referencia y |Mz_i| para flexión uniaxial. No se sustituye un esfuerzo de otra sección ni un máximo global por la demanda de esa referencia.

Las rotaciones no se copian directamente a Euler angles de Unity. La deformada cúbica usa los seis desplazamientos por nodo, transforma a ejes locales, interpola y convierte la posición resultante al sistema de Unity. Esta interpolación no incluye la solución particular de una carga entre nodos.

Las flechas gravitacionales en Unity indican dirección y presencia de carga; su longitud es cualitativa. Las fuerzas numéricas se consultan en Cargas. Las flechas sísmicas se escalan por fuerza de piso respecto del máximo de cada edificio.
