# ADR-1015: Hook commands run powershell, or else pwsh, through sh

- Status: Accepted
- Date: 2026-10-29
- Decided by Claude under Stewart's delegation (BL-3926)

## Context

The three hook commands in `.claude/settings.json` began `powershell -NoProfile ...`. Linux
and macOS have no `powershell`, only `pwsh`, so off Windows none of them started. For the
audit guard hook that is worse than a missing feature: Claude Code treats a hook that cannot
start (exit 127) as a non-blocking error and runs the tool anyway, so a dark factory lane on
Linux or macOS could read and change audit paths with the guard silently gone.

Claude Code runs every hook command with `sh`: Git Bash on Windows (measured 2026-10-29 with
a scratch `UserPromptSubmit` hook, which saw `$0` = `/usr/bin/bash`), the system shell on
Linux and macOS. So one POSIX `sh` line works on all three.

## Decision

Each hook command is one `sh` line that runs its script with `powershell` when it is on PATH
and with `pwsh` otherwise:

```sh
if command -v powershell >/dev/null 2>&1; then exec powershell -NoProfile -ExecutionPolicy Bypass -File .claude/hooks/<script>.ps1;
elif command -v pwsh >/dev/null 2>&1; then exec pwsh -NoProfile -ExecutionPolicy Bypass -File .claude/hooks/<script>.ps1;
fi
```

The audit guard's line has one more branch: with neither on PATH inside a dark factory lane
(`CURL_DARK_FACTORY_LANE` set) it writes a refusal and exits 2, so the guard fails closed.

- **`powershell` first** keeps Windows exactly as it was: the hooks have always run under
  Windows PowerShell 5.1 there, which every Windows machine has; `pwsh` is not guaranteed.
- **`exec`** hands standard input and the exit code straight to the script, so the guard's
  exit 2 still refuses the tool call.
- **Inline in `settings.json`**, not in a wrapper script: `settings.json` is a guard file a lane
  may not change, and a separate wrapper would be a file a lane could edit to switch the guard
  off.

Rejected: plain `pwsh` everywhere (a Windows machine without PowerShell 7 would lose the guard
the same way Linux had), and a `pwsh ... || powershell ...` chain (it reruns the hook after the
guard's own exit 2).

## Consequences

`Audit/Guard/Test-GuardHooks.ps1` runs each hook command through `sh` as Claude Code does, and
checks that the guard refuses a lane's read of `Audit/README.md`, allows `README.md`, allows
an interactive session, fails closed without either shell, and that the other hooks start.
CI's `guard-hooks` job runs it, and the audit guard's `-SelfTest`, on Windows, Linux and macOS.
