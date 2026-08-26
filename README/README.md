# Contexto del Proyecto

## Identificacion

- Proyecto: Proyecto 1 de MCOC.
- Responsable inicial: Juan Pablo Caceres.
- Colaboradores previstos: `benjagarciag` y `nagarcia4`.
- Repositorio previsto: `Proyecto-1-MCOC`.

## Proposito

Este repositorio centraliza el trabajo del grupo y los planos del edificio
entregados para el proyecto. La carpeta `planos_edificio_ing` contiene los
archivos originales en formato DWG, que serviran como base para el desarrollo,
revision y documentacion del proyecto.

## Estado actual

- Los planos originales estan en `planos_edificio_ing/`.
- Hay 38 archivos DWG versionados con Git LFS.
- El repositorio local usa `main` como rama principal.
- Existe un commit inicial con los planos y la documentacion basica.
- El repositorio remoto de GitHub aun debe crearse y publicarse.
- La cuenta de GitHub debe autenticarse con `gh auth login` antes de publicar.

## Objetivos de trabajo

1. Revisar y clasificar los planos disponibles.
2. Definir las tareas tecnicas y repartirlas entre los integrantes.
3. Registrar avances, decisiones y resultados en el repositorio.
4. Mantener los archivos originales sin modificaciones accidentales.
5. Revisar los cambios mediante ramas y Pull Requests.

## Organizacion de archivos

- `planos_edificio_ing/`: archivos DWG originales del edificio.
- `README/README.md`: este documento, que mantiene el contexto general.
- `README.md`: guia corta del repositorio y flujo de trabajo.
- `.gitattributes`: configuracion de Git LFS para archivos DWG.
- `.gitignore`: archivos temporales que no deben versionarse.

## Reglas de colaboracion

- No trabajar directamente sobre `main` salvo para integraciones revisadas.
- Crear una rama por tarea, por ejemplo `revision-planos`.
- Usar commits breves y descriptivos.
- No subir archivos temporales de AutoCAD (`.bak`, `.dwl`, `.dwl2`, entre otros).
- Antes de modificar un DWG original, confirmar con el grupo si debe conservarse
  una copia de referencia.
- Abrir un Pull Request para revisar cambios importantes antes de integrarlos.

## Pendientes

- Autenticar GitHub en la maquina de trabajo.
- Crear el repositorio remoto `Proyecto-1-MCOC`.
- Agregar como colaboradores a `benjagarciag` y `nagarcia4`.
- Completar el objetivo tecnico, metodologia y entregables especificos del
  proyecto cuando el grupo los defina.
- Documentar aqui las decisiones importantes y el avance de cada etapa.

## Como mantener este contexto

Cada vez que cambie el objetivo, la metodologia, la estructura de archivos o
la distribucion de tareas, actualizar este documento. Asi cualquier integrante
puede entender el estado del proyecto antes de comenzar a trabajar.
