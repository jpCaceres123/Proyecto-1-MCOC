# Subir este proyecto a GitHub

Esta carpeta contiene una copia organizada. Los originales, planos CAD, borradores y ejecutables anteriores permanecen fuera del repositorio.

El repositorio local se prepara con la rama `main`, sin repositorio remoto ni publicación. Desde su carpeta, revisar los archivos y crear el primer commit:

```powershell
git status
git add .
git commit -m "Agregar laboratorio estructural y recreación arquitectónica"
```

Crear un repositorio vacío en GitHub y utilizar la dirección que proporcione:

```powershell
git remote add origin https://github.com/TU_USUARIO/TU_REPOSITORIO.git
git push -u origin main
```

Sustituir los dos marcadores por los datos reales. También se puede añadir esta carpeta a GitHub Desktop y publicarla desde allí.

Los ejecutables y ZIP están excluidos del código fuente. Se pueden adjuntar a una Release si se desea distribuir una versión compilada. No se ha agregado una licencia: la elección corresponde al autor, y las fotografías y planos conservan sus derechos de origen.

El cálculo genera archivos JSON con rutas del equipo local; esos resultados están excluidos de Git. Después de clonar, instalar las dependencias y recalcular siguiendo el README.
