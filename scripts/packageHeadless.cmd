@ECHO OFF

set PACKAGENAME=NineChronicles.HEADLESS.v100050.4T

@REM 1. Create folder with package name
@REM 2. Move out folder to sub-directory called app inside package folder
@REM 3. Copy startHeadless script to package folder
@REM 4. Zip up and compress contents to .zip file

if not exist "BuildPackage" mkdir BuildPackage

ECHO Creating a new package: %PACKAGENAME%...

if exist BuildPackage\%PACKAGENAME% (
    RD /S /Q "BuildPackage\%PACKAGENAME%\"
    mkdir BuildPackage\%PACKAGENAME%
) else (
    mkdir BuildPackage\%PACKAGENAME%
)

ECHO Building project...
cmd /c scripts\buildHeadless.cmd

ECHO Copying build to new package...
move /Y out BuildPackage/%PACKAGENAME%\app

ECHO Copying scripts to new package...
copy scripts\startHeadless.cmd BuildPackage\%PACKAGENAME%\

ECHO Zipping up package...
if exist BuildPackage\%PACKAGENAME%.zip (
    del /f /q "BuildPackage\%PACKAGENAME%.zip"
    powershell Compress-Archive BuildPackage\%PACKAGENAME% BuildPackage\%PACKAGENAME%.zip
) else (
    powershell Compress-Archive BuildPackage\%PACKAGENAME% BuildPackage\%PACKAGENAME%.zip
)

ECHO Cleaning up...
RD /S /Q "BuildPackage\%PACKAGENAME%\"

ECHO YOUR PACKAGE IS READY!
ECHO You can find your package in the directory:
ECHO       "BuildPackage\%PACKAGENAME%.zip"