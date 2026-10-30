#!/usr/bin/env bash
# Linux and macOS twin of Gap/RunGapAnalysis.cmd (BL-3928): runs RunGapAnalysis.ps1 under PowerShell 7
# with every argument passed through. It measures this platform here and the other of linux
# and macos through gap-platforms.yml; windows is measured only from a Windows checkout.
command -v pwsh >/dev/null 2>&1 || { echo "RunGapAnalysis.sh: needs PowerShell 7 (pwsh) on PATH" >&2; exit 1; }
exec pwsh -NoProfile -File "$(dirname "${BASH_SOURCE[0]}")/RunGapAnalysis.ps1" "$@"
