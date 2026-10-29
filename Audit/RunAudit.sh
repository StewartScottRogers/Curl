#!/usr/bin/env bash
# Linux and macOS twin of Audit/RunAudit.cmd: runs an audit of the dark factory's work.
# Arguments pass through, e.g. Audit/RunAudit.sh -NewTab -DryRun
# Needs PowerShell 7 (pwsh) on PATH. With -NewTab the audit opens as a herdr tab running pwsh
# when this runs inside herdr; without herdr it starts as a detached pwsh process writing
# <repo>.audit/RunAudit-<stamp>.log.
command -v pwsh >/dev/null 2>&1 || { echo "RunAudit.sh: needs PowerShell 7 (pwsh) on PATH" >&2; exit 1; }
exec pwsh -NoProfile -File "$(dirname "${BASH_SOURCE[0]}")/RunAudit.ps1" "$@"
