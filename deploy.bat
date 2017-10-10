
set H=R:\KSP_1.3.1_dev
echo %H%

copy /Y LoadingScreenManager\bin\Debug\LoadingScreenManager.dll GameData


cd GameData

copy LoadingScreenManager.dll %H%\GameData
mkdir "%H%\GameData\LoadingScreenManager"
xcopy /y /s LoadingScreenManager "%H%\GameData\LoadingScreenManager"
