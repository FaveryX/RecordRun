RecordRun - record and replay mouse and keyboard input (Windows)
Copyright (C) 2026 FaveryX - GPL-3.0-or-later (see LICENSE.txt)

BUILD THE INSTALLER
1. Keep all files together in one folder.
2. Double-click build-installer.bat
   (if .NET 8 SDK or Inno Setup is missing it installs them; then close the window and run it again).
3. Your installer: installer\RecordRun-Setup.exe

USE
Record -> Stop recording -> Play / Pause / Stop. Set repeat count, infinite loop and delay first.
Save / Load keeps recordings in %AppData%\RecordRun\Recordings (Load lets you rename or delete).
Emergency stop: Ctrl+Alt+F12.
Run as administrator to control elevated windows.
