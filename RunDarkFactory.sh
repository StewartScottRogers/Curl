#!/usr/bin/env bash
# Linux and macOS twin of RunDarkFactory.cmd: unattended task-board shift.
# Arguments pass through, e.g. ./RunDarkFactory.sh -Hours 4 -MaxTasks 3
# Needs PowerShell 7 (pwsh) on PATH.
command -v pwsh >/dev/null 2>&1 || { echo "RunDarkFactory.sh: needs PowerShell 7 (pwsh) on PATH" >&2; exit 1; }
exec pwsh -NoProfile -File "$(dirname "${BASH_SOURCE[0]}")/RunDarkFactory.ps1" "$@"
