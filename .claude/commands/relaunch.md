---
name: relaunch
description: "TRIGGER THIS SKILL whenever The Great Email App needs to be killed, rebuilt, and relaunched after a code change. Call it when: the user says 'relaunch', 'restart the app', 'rebuild and run', 'try it again', 'kill and rerun', or any time after editing C#/XAML when the running instance needs to be replaced. The compiled exe file lock prevents `dotnet build` from succeeding while the app is running, so this skill always kills first."
---

# Relaunch The Great Email App

Stop any running instance, rebuild, and start a fresh one. Idempotent — safe to run repeatedly.

## Procedure

Run this single PowerShell command (it's all one pipeline so the user only sees one tool prompt):

```powershell
Get-Process -Name GreatEmailApp -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 500
Set-Location "E:\The Great Email App\src"
& "C:\Program Files\dotnet\dotnet.exe" build 2>&1 | Select-Object -Last 8
Start-Process -FilePath "E:\The Great Email App\src\GreatEmailApp\bin\Debug\net8.0-windows\GreatEmailApp.exe"
```

## Why each step matters

1. **`Stop-Process`** — the running exe holds a file lock on `GreatEmailApp.dll` and `GreatEmailApp.Core.dll`. `dotnet build` will fail with MSB3027 if the app is still up.
2. **`Start-Sleep 500ms`** — gives the OS a moment to release the file handles after process exit. Without this, the build occasionally still fails with the lock error on fast hardware.
3. **`Set-Location` + full `dotnet.exe` path** — the bash environment on this machine has trouble resolving the .NET SDK; PowerShell with the explicit path works reliably (see Known Issues if this resurfaces).
4. **`Select-Object -Last 8`** — surface only the build summary in the chat. Full output goes to the task log if the user wants to scroll.
5. **`Start-Process`** — non-blocking launch so we can keep working in the same chat.

## When NOT to use this skill

- If the build fails — let the user see the errors first; don't auto-launch a stale binary.
- If `dotnet` isn't installed — fall back to telling the user to install the SDK.
- If the user is debugging in Visual Studio — they'll launch from there.

## Rules

1. Always kill before build. No exceptions.
2. If `Stop-Process` fails (no process running), continue silently — `-ErrorAction SilentlyContinue` handles this.
3. After launch, do not poll/sleep waiting for the window — trust that it opened. The user will tell us if it didn't.
