@echo off
setlocal

set "RimWorldDir=%~1"

if not defined RimWorldDir if defined RIMWORLD_DIR set "RimWorldDir=%RIMWORLD_DIR%"
if not defined RimWorldDir if exist "%~dp0..\..\..\RimWorldWin64_Data\Managed\Assembly-CSharp.dll" set "RimWorldDir=%~dp0..\..\..\"

if not defined RimWorldDir (
  echo ERROR: RimWorld install folder was not found.
  echo Usage: compile.bat "E:\Path\To\RimWorld"
  exit /b 1
)

if not exist "%RimWorldDir%\RimWorldWin64_Data\Managed\Assembly-CSharp.dll" (
  echo ERROR: Invalid RimWorldDir: %RimWorldDir%
  exit /b 1
)

dotnet build "%~dp0LeadYourPet.csproj" --configuration Release /p:RimWorldDir="%RimWorldDir%"
if errorlevel 1 exit /b 1

dotnet build "%~dp0..\Guard\Source\LeadYourPetContinuedGuard.csproj" --configuration Release /p:RimWorldDir="%RimWorldDir%"
if errorlevel 1 exit /b 1

echo Build complete: ..\Assemblies\LeadYourPet.dll
endlocal
