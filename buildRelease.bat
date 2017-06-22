
@echo off

copy /Y LoadingScreenManager\bin\Release\LoadingScreenManager.dll GameData

copy /Y ..\MiniAVC.dll GameData\LoadingScreenManager

set RELEASEDIR=d:\Users\jbb\release
set ZIP="c:\Program Files\7-zip\7z.exe"

copy GameData\LoadingScreenManager\LoadingScreenManager.version a.version
set VERSIONFILE=a.version
rem The following requires the JQ program, available here: https://stedolan.github.io/jq/download/
c:\local\jq-win64  ".VERSION.MAJOR" %VERSIONFILE% >tmpfile
set /P major=<tmpfile

c:\local\jq-win64  ".VERSION.MINOR"  %VERSIONFILE% >tmpfile
set /P minor=<tmpfile

c:\local\jq-win64  ".VERSION.PATCH"  %VERSIONFILE% >tmpfile
set /P patch=<tmpfile

c:\local\jq-win64  ".VERSION.BUILD"  %VERSIONFILE% >tmpfile
set /P build=<tmpfile
del tmpfile
set VERSION=%major%.%minor%.%patch%
if "%build%" NEQ "0"  set VERSION=%VERSION%.%build%

del a.version
echo Version:  %VERSION%

copy /y /s License.txt GameData\LoadingScreenManager
copy /Y LoadingScreenManager\README.md GameData\LoadingScreenManager


set FILE="%RELEASEDIR%\LoadingScreenManager-%VERSION%.zip"
IF EXIST %FILE% del /F %FILE%
%ZIP% a -tzip %FILE% GameData
