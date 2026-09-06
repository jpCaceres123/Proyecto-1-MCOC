# Bitácora de apoyo de IA

Fecha: 5 de septiembre de 2026. Herramienta utilizada: Codex en la sesión del usuario. No se declara uso de OpenCode ni pruebas presenciales que no se hayan realizado.

## Entradas del usuario

- Encargo de siete semanas: modelo global lineal 3D, cargas tributarias, superposición, fibras, Unity y AR básica.
- Dos edificios separados por 10 cm; confirmación de que los DXF usan centímetros.
- Planos LT1/LT2 y posteriormente `modelo_estructural_completo.xlsx`.
- Instrucción de guardar el trabajo dentro de `Proyecto edificio` y construir Unity con cálculo OpenSees en Python.

## Trabajo asistido y revisión crítica

1. Lectura de planos y extracción inicial: se distinguió posición de lámina de posición física; no se convirtió automáticamente cada línea CAD en una barra.
2. Revisión del Excel: se detectaron nodos gráficos, cuatro pares de nodos de muro coincidentes, 82 barras WALL inclinadas, ausencia de diafragmas explícitos y diferencias de cobertura entre las hojas de carga.
3. Construcción de una geometría analítica derivada: centroides de muros, brazos, subdivisión de barras, apertura de junta y conservación de cargas de la barra eliminada.
4. Implementación de casos independientes y verificación de equilibrio en las seis componentes. Se contrastó superposición con una corrida explícita.
5. Revisión de orientación de secciones: se corrigió el eje local de vigas para que el canto h trabaje en dirección vertical. Una ménsula con solución analítica comprueba el eje de flexión usado.
6. Implementación de fibras con armado didáctico. Se separó la curva de sección del análisis global y se agregó comparación de compresión uniforme independiente y bloque Whitney.
7. Revisión de la rama comprimida de P-M: se evitó tomar la respuesta uniforme postpico del hormigón como límite de la curva de capacidad.
8. Compilación real de Unity, ejecución del visor y revisión de capturas. Se corrigió el encuadre de cámara para mantener el modelo dentro del área central de la interfaz.
9. Pruebas de extremo a extremo de reanálisis y recarga, documentadas en los registros de resultados cuando se ejecutan.

## Verificaciones reproducibles

Los registros se encuentran en `work/calculo.log`, `work/pruebas.log`, `work/unity_build.log`, `resultados/validacion.json` y `resultados/unity_smoke.txt`. El código de las pruebas está incluido.

Las verificaciones numéricas prueban las propiedades descritas, pero no sustituyen la comparación de conectividad, secciones, cargas y apoyos con los planos y la convención del profesor. La asignación heredada de paneles a vigas cercanas, los muros equivalentes y el armado representativo requieren revisión humana del curso.

## Pendientes que no se han presentado como ejecutados

- Confirmación del profesor de hipótesis de muros, apoyos, rigideces y patrón lateral.
- Sustitución de las armaduras de demostración por barras reales de las secciones escogidas.
- Validación en terreno de AR con cámara y referencias físicas.
- Si el curso exige específicamente OpenCode, repetir una tarea de verificación allí y adjuntar su registro real; no inventar una bitácora de esa herramienta.
