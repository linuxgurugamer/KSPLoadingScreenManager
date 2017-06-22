
rem copy /y "$(TargetPath)" "r:\KSP_1.2.2_dev\GameData\"

rem copy /y "$(TargetPath)" "D:\Users\jbb\github\KSPLoadingScreenManager\LoadingScreenManager\GameData"


set H=R:\KSP_1.3.0_dev
echo %H%

copy /Y LoadingScreenManager\bin\Debug\LoadingScreenManager.dll GameData


cd GameData

copy LoadingScreenManager.dll %H%\GameData
mkdir "%H%\GameData\LoadingScreenManager"
xcopy /y /s LoadingScreenManager "%H%\GameData\LoadingScreenManager"
