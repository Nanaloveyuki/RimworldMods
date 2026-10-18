@echo off
setlocal

if not "%~2"=="" set "RimWorldVersion=%~2"
if not defined RimWorldVersion if defined RIMWORLD_VERSION set "RimWorldVersion=%RIMWORLD_VERSION%"
if not defined RimWorldVersion set "RimWorldVersion=1.7"
if not "%RimWorldVersion%"=="1.7" if not "%RimWorldVersion%"=="1.6" (
  echo ERROR: RimWorldVersion must be 1.7 or 1.6.
  exit /b 1
)
if not "%~1"=="" set "RimWorldDir=%~1"
if not defined RimWorldDir if defined RIMWORLD_DIR set "RimWorldDir=%RIMWORLD_DIR%"
if not defined RimWorldDir if "%RimWorldVersion%"=="1.7" if exist "%~dp0..\..\..\Assembly-CSharp.dll" set "RimWorldDir=%~dp0..\..\..\"
if not defined RimWorldDir if "%RimWorldVersion%"=="1.6" if exist "%~dp0..\..\..\RimWorldWin64_Data\Managed\Assembly-CSharp.dll" set "RimWorldDir=%~dp0..\..\..\"
if not defined RimWorldDir if "%RimWorldVersion%"=="1.7" set "RimWorldDir=E:\Apps\Steam\steamapps\common\RimWorld"

if not defined RimWorldDir (
  echo ERROR: RimWorld install folder was not found.
  echo Usage: compile.bat "E:\Path\To\RimWorld" [1.7^|1.6]
  exit /b 1
)
set "ManagedDir=%RimWorldDir%"
set "UnityAssembly=UnityStubs.dll"
set "OutputDir=..\1.7\Assemblies"
if "%RimWorldVersion%"=="1.6" (
  set "ManagedDir=%RimWorldDir%\RimWorldWin64_Data\Managed"
  set "UnityAssembly=UnityEngine.CoreModule.dll"
  set "OutputDir=..\Assemblies"
)
if not exist "%ManagedDir%\Assembly-CSharp.dll" (
  echo ERROR: Invalid RimWorld %RimWorldVersion% install folder: %RimWorldDir%
  exit /b 1
)
if not exist "%ManagedDir%\%UnityAssembly%" (
  echo ERROR: Missing %ManagedDir%\%UnityAssembly%
  exit /b 1
)

dotnet build "%~dp0LeadYourPet.csproj" --configuration Release /p:RimWorldDir="%RimWorldDir%" /p:RimWorldVersion="%RimWorldVersion%"
if errorlevel 1 exit /b 1

dotnet build "%~dp0..\Guard\Source\LeadYourPetContinuedGuard.csproj" --configuration Release /p:RimWorldDir="%RimWorldDir%" /p:RimWorldVersion="%RimWorldVersion%"
if errorlevel 1 exit /b 1

echo Build complete: %OutputDir%\LeadYourPet.dll ^(RimWorld %RimWorldVersion%^)
endlocal
