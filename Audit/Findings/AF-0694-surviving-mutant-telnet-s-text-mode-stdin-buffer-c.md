---
id: AF-0694
title: Surviving mutant: telnet's text-mode stdin buffer can be two bytes short and overflow on a full read
auditor: quality
severity: High
status: closed
reason: Re-audit 2026-11-12_2105.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Engine.UnitLibrary/WindowsTextModeReadStream.cs:decoded-plus1:surviving-mutant
reproduction: mutation Curl.Engine.UnitLibrary/WindowsTextModeReadStream.cs:20:+1
task: BL-4888
tasks: BL-4888
found: 2026-11-05
found-at: 4eba3c63638befcc6a63aaa31ddf5419922fa939
scorecard: 2026-11-05_2025.md
duplicate-of:
closed: 2026-11-12
closed-how: mechanical
closed-by: 2026-11-12_2105.md
---
# AF-0694 - Surviving mutant: telnet's text-mode stdin buffer can be two bytes short and overflow on a full read

## Summary

High finding from the quality auditor at `Curl.Engine.UnitLibrary/WindowsTextModeReadStream.cs:20`: Surviving mutant: telnet's text-mode stdin buffer can be two bytes short and overflow on a full read.

## Evidence

Location: `Curl.Engine.UnitLibrary/WindowsTextModeReadStream.cs:20`

'private readonly byte[] decoded = new byte[RawReadSize + 1];' mutated to 'RawReadSize - 1' (4095) survived. Translate (line 100) writes up to one decoded byte per raw byte, plus a held CR, into decoded, and each raw read is up to RawReadSize (4096). So a telnet standard input that delivers 4095+ bytes in one read would throw IndexOutOfRangeException, changing the exit code and the bytes sent. No test reads more than a few bytes.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/WindowsTextModeReadStream.cs:20:+1 -Member decoded -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

## Re-audits

- 2026-11-12 | 2026-11-12_2105.md | reproduces: no | Ran the -Site command: WindowsTextModeReadStream.cs:20 'new byte[RawReadSize + 1]' -> 'RawReadSize - 1' was killed. Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-11-05: filed proposed.
- 2026-11-12: accepted -> closed. Re-audit 2026-11-12_2105.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
