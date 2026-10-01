@echo off
REM Runs one audit of the dark factory's work (BL-1020). Arguments pass through, e.g. Audit\RunAudit.cmd -NewTab
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0RunAudit.ps1" %*
