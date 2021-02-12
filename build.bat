@ECHO OFF

ECHO Updating externals...
git submodule init
git submodule update

ECHO Building...
dotnet publish -p:PublishProfile="Windows - Release x64"
dotnet publish -p:PublishProfile="Windows - Release x32"
dotnet publish -p:PublishProfile="Windows - Release Arm"
dotnet publish -p:PublishProfile="Linux - Release x64"
dotnet publish -p:PublishProfile="Linux - Release Arm"
dotnet publish -p:PublishProfile="OSX - Release x64"
PAUSE