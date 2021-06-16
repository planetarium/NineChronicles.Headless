@ECHO OFF

set COMMIT="f800e708776fd299ddbe9c89d63c35688abefc38"

ECHO "Building Nine Chronicles - HEADLESS..."
dotnet publish NineChronicles.Headless.Executable/NineChronicles.Headless.Executable.csproj ^
-c Release ^
-r win10-x64 ^
-o out ^
--self-contained ^
--version-suffix %COMMIT%