@echo off
sc stop MyWindowsServiceApp
sc delete MyWindowsServiceApp
echo Service uninstalled.
pause
