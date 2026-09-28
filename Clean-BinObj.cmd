@echo off
setlocal EnableExtensions

rem Clean build output directories beneath this repository only.
set "RepositoryRoot=%~dp0"

echo Cleaning bin and obj directories under "%RepositoryRoot%"...

for /d /r "%RepositoryRoot%" %%D in (bin obj) do (
    if exist "%%~fD\" (
        echo Removing "%%~fD"
        rd /s /q "%%~fD"
    )
)

echo.
echo Cleanup complete.
endlocal
