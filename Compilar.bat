
@echo off
setlocal
cd /d "%~dp0"

title Compilar MPBossStats

echo ==========================================
echo       tShock - Compilador de Plugins
echo ==========================================
echo.

where dotnet >nul 2>&1
if errorlevel 1 (
    echo ERROR: No se encuentra dotnet.
    echo Instala el SDK de .NET 9.
    pause
    exit /b 1
)

if not exist "MPBossStats.csproj" (
    echo ERROR: No se encuentra el archivo csproj.
    pause
    exit /b 1
)

echo SDK instalado:
dotnet --version
echo.
echo Compilando en Release...
echo.

dotnet build "MPBossStats.csproj" -c Release

if errorlevel 1 (
    echo.
    echo ERROR: La compilacion ha fallado.
    pause
    exit /b 1
)

echo.
echo ==========================================
echo COMPILACION CORRECTA
echo ==========================================
echo DLL:
echo %CD%\bin\Release\net9.0\MPBossStats.dll
echo.
pause
endlocal
