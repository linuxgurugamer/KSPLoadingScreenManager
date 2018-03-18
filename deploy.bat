

@echo off

rem H is the destination game folder
rem GAMEDIR is the name of the mod folder (usually the mod name)
rem GAMEDATA is the name of the local GameData
rem VERSIONFILE is the name of the version file, usually the same as GAMEDATA,
rem    but not always

set H=R:\KSP_1.4.1_dev
set GAMEDIR=LoadingScreenManager
set GAMEDATA="GameData\%GAMEDIR%"
set VERSIONFILE=%GAMEDIR%.version

copy /Y %VERSIONFILE% %GAMEDATA%
copy /Y "%1%2" "GameData\%GAMEDIR%\Plugins"

mkdir "%H%\GameData\%GAMEDIR%"
xcopy /y /s GameData\%GAMEDIR% "%H%\GameData\%GAMEDIR%"
