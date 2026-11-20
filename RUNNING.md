

<!-- local-development:start -->
# Desarrollo local

Puedes abrir este repositorio en VS Code, Antigravity o Sublime Text. El compilador y los servicios necesarios dependen del componente indicado abajo.

## Visual Studio 2022 / .NET Framework

Instala la carga de trabajo de escritorio .NET y el paquete de destino indicado en la tabla. Los proyectos ASP.NET también requieren la carga web e IIS Express. Abre la solución `.sln` correspondiente y selecciona el proyecto de aplicación para iniciar.

```powershell
# Ejecutar desde Developer PowerShell de Visual Studio 2022
$solutionDir = (Resolve-Path 'Candy-API').Path + '\'
MSBuild.exe 'Candy-API/APICandy/APICandy.csproj' /restore /p:RestorePackagesConfig=true /p:Configuration=Debug /p:DeployOnBuild=false "/p:SolutionDir=$solutionDir"
```

## Componentes y validación

Inventario y compilaciones locales del 2026-11-15. Una compilación correcta no acredita todavía interacción, compatibilidad de base de datos, instalador ni despliegue.

| Componente | Tipo / destino | Validación |
| --- | --- | --- |
| [Candy-API/APICandy/APICandy.csproj](<Candy-API/APICandy/APICandy.csproj>) | dotnet / v4.8.1 | Compila; prueba funcional pendiente |
| [Candy-API/Candy/Candy.csproj](<Candy-API/Candy/Candy.csproj>) | dotnet / v4.8.1 | Compila; prueba funcional pendiente |
| [Candy-API/CandyApi.sln](<Candy-API/CandyApi.sln>) | solution | Sin ejecutar |

## Archivos ignorados

`.gitignore` excluye credenciales locales, estado del editor y salidas de las herramientas detectadas. No deja de versionar archivos que ya estaban en Git. Los manifiestos, archivos de bloqueo, recursos fuente y scripts de esquema forman parte del código.
<!-- local-development:end -->

<!-- improvements-20261119:start -->
## Mejoras y verificación — 2026-11-19 (Motor Match-3 thread-safe `CandyBoardEngine` y manejo seguro de API)

- Reemplazado el estado estático inseguro de `PartidaController` por `CandyBoardEngine` con partidas concurrentes por ID, generación de tablero 9×9 sin coincidencias iniciales, validación de adyacencia ortogonal y detección/cascada Match-3.
- Añadido endpoint `GET api/Partida/{id}` y respuestas `400 BadRequest` / `404 NotFound` ante cuerpos nulos, índices fuera de rango (`0..80`), celdas repetidas o partidas inexistentes.
- Validación: compilación MSBuild de `APICandy` y `Candy`, más 12 aserciones automatizadas sobre creación de tableros, validaciones de intercambio y cascadas Match-3.
<!-- improvements-20261119:end -->
