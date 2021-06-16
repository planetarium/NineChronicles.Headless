@ECHO OFF

set COMMIT="a0c68ddc17b06a1537813f24dfd7ba5e899928a9"

ECHO "Building Nine Chronicles - HEADLESS..."
dotnet publish NineChronicles.Headless.Executable/NineChronicles.Headless.Executable.csproj ^
-c Release ^
-r win10-x64 ^
-o out ^
--self-contained ^
--version-suffix %COMMIT%