SET SERVICE_NAME=MyWindowsServiceApp
SET EXE_PATH=%~dp0invetoryBackGroundServices.exe

echo Installing %SERVICE_NAME% service...

sc create %SERVICE_NAME% binPath= "%EXE_PATH%" start= auto
sc start %SERVICE_NAME%

echo.
echo ✅ Service installed and started successfully.
pause
