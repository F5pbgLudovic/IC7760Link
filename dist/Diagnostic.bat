@echo off
chcp 65001 >nul
cd /d "%~dp0"
if not exist "IC7760Link.exe" (
  echo ERREUR : IC7760Link.exe est introuvable a cote de ce fichier.
  echo Vous avez probablement lance Diagnostic.bat depuis l'interieur du zip.
  echo Faites un clic droit sur le zip, "Extraire tout...", puis relancez depuis le dossier extrait.
  pause
  exit /b
)
echo IC-7760 Link - diagnostic de la liaison CI-V
echo Fermez d'abord IC7760Link et les autres logiciels qui utilisent le port COM de l'IC-7760.
set /p PORT=Port COM du poste (ex. COM5) : 
set /p BAUD=Vitesse [115200] : 
if "%BAUD%"=="" set BAUD=115200
set /p ADR=Adresse CI-V [B2] : 
if "%ADR%"=="" set ADR=B2
echo Enregistrement pendant 12 secondes...
"%~dp0IC7760Link.exe" --diag=%PORT%,%BAUD%,%ADR%
echo.
if exist "%~dp0diagnostic-ci-v.txt" (
  echo Termine. Le fichier est ici : %~dp0diagnostic-ci-v.txt
  explorer /select,"%~dp0diagnostic-ci-v.txt"
) else (
  echo Le fichier n'a pas ete cree.
)
pause
