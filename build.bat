@ECHO OFF

ECHO Cleaning up old builds...
rmdir /s /q bin\x64\Release
rmdir /s /q bin\Any\Release
rmdir /s /q obj
rmdir /s /q builds

ECHO Updating externals...
git submodule init
git submodule update

ECHO Building...
dotnet publish -c Release -p:PublishProfile="Windows - Release x64"
dotnet publish -c Release -p:PublishProfile="Windows - Release x86"
dotnet publish -c Release -p:PublishProfile="Windows - Release Arm"
dotnet publish -c Release -p:PublishProfile="Linux - Release x64"
dotnet publish -c Release -p:PublishProfile="Linux - Release Arm"
dotnet publish -c Release -p:PublishProfile="OSX - Release x64"

ECHO Archiving...
7z a -t7z -mmt16 -mx9 .\builds\flp2midi-win-x64.7z .\bin\x64\Release\net5.0\publish\win-x64\*.dll .\bin\x64\Release\net5.0\publish\win-x64\flp2midi.exe
7z a -t7z -mmt16 -mx9 .\builds\flp2midi-win-x86.7z .\bin\Any\Release\net5.0\publish\win-x86\*.dll .\bin\Any\Release\net5.0\publish\win-x86\flp2midi.exe
7z a -t7z -mmt16 -mx9 .\builds\flp2midi-win-arm.7z .\bin\Any\Release\net5.0\publish\win-arm\*.dll .\bin\Any\Release\net5.0\publish\win-arm\flp2midi.exe
7z a -t7z -mmt16 -mx9 .\builds\flp2midi-linux-x64.7z .\bin\x64\Release\net5.0\publish\linux-x64\flp2midi
7z a -t7z -mmt16 -mx9 .\builds\flp2midi-linux-arm.7z .\bin\Any\Release\net5.0\publish\linux-arm\flp2midi
7z a -t7z -mmt16 -mx9 .\builds\flp2midi-osx-x64.7z .\bin\x64\Release\net5.0\publish\osx-x64\flp2midi .\bin\x64\Release\net5.0\publish\osx-x64\*.dylib

PAUSE