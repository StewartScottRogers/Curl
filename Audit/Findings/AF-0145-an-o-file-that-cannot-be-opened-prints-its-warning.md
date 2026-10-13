---
id: AF-0145
title: An -o file that cannot be opened prints its Warning line inside the progress-meter row instead of after the meter's closing newline
auditor: conformance
severity: Low
status: accepted
reason: 
key: conformance:Curl.Console/CurlCommandRunner.cs:output-open-warning-after-meter:stderr
reproduction: none
task: BL-1964
tasks: BL-1964
found: 2026-10-10
found-at: 1b27494521dec4bdaa3fe60c8dc7a3fc73874253
scorecard: 2026-10-10_0123.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0145 - An -o file that cannot be opened prints its Warning line inside the progress-meter row instead of after the meter's closing newline

## Summary

Low finding from the conformance auditor at `Curl.Console/CurlCommandRunner.cs:4029`: An -o file that cannot be opened prints its Warning line inside the progress-meter row instead of after the meter's closing newline. Reported by an auditor flagged unreliable in 2026-10-10_0123.md.

## Evidence

Location: `Curl.Console/CurlCommandRunner.cs:4029`

Reference: curl 8.21.0 (x86_64-w64-mingw32) libcurl/8.21.0 Schannel zlib/1.3.2 brotli/1.2.0 zstd/1.5.7 libidn2/2.3.8 libpsl/0.21.5 libssh2/1.11.1 WinLDAP. Invoke-DifferentialConformance.ps1 -Count 300 -Seed 455559493, case 233 (--output-dir missing.txt --output f.txt URL) differed in stderr only; both exit 23 with the same request. Reduced to --output missing.txt/f.txt URL (reproduced 3 of 3 runs). curl's stderr ends '... 0\r\nWarning: Failed to open the file missing.txt/f.txt: No such file or directory\r\n': the meter row is closed with its newline before the warning. Curl's ends '... 0Warning: Failed to open the file missing.txt/f.txt: No such file or directory\r\n\r\n': the warning is written mid-row, and the meter's end newline (WriteProgressAsync, CurlCommandRunner.cs:4029) comes after it as a stray blank line. With -s both stderr outputs are empty (identical). Phase 2: no ADR under Documentation/Planning/Decisions records this ordering as deliberate.

## Reproduction

Run from the repository root:

```powershell
$o="$env:TEMP\ac-odir"; $a=@('--output','missing.txt/f.txt','http://127.0.0.1:50994/'); & ./Record-CurlExchange.ps1 -Port 50994 -Curl 'C:\Program Files\Git\mingw64\bin\curl.exe' -OutDirectory "$o\curl" -CurlArgs $a | Out-Null; & ./Record-CurlExchange.ps1 -Port 50994 -Curl (Resolve-Path Curl.Console/bin/Release/net10.0/curl.exe) -OutDirectory "$o\candidate" -CurlArgs $a | Out-Null; 'curl: ' + @(Select-String -Path "$o\curl\stderr.txt" -Pattern '^Warning: Failed').Count + ' / Curl: ' + @(Select-String -Path "$o\candidate\stderr.txt" -Pattern '^Warning: Failed').Count
```

- Expected: curl: 1 / Curl: 1 (the Warning line starts its own line after the meter's newline, no trailing blank line)
- Actual: curl: 1 / Curl: 0 (Curl's warning follows the meter row on the same line: '...0Warning: Failed to open the file missing.txt/f.txt: No such file or directory', then an extra CRLF)

## Re-audits

- 2026-10-11 | 2026-10-11_0051.md | not re-audited | overlaps planted defect PD-201 in Curl.Console/CurlCommandRunner.cs, so the auditor's verdict (reproduces no) is set aside: Ran the reproduction against curl 8.21.0 (x86_64-w64-mingw32, Schannel): 'curl: 1 / Curl: 1' - Curl's 'Warning: Failed' line now starts a line, as curl's does; both exit 23.
- 2026-10-11 | 2026-10-11_1638.md | not re-audited | overlaps planted defect PD-201 in Curl.Console/CurlCommandRunner.cs, so the auditor's verdict (reproduces no) is set aside: Ran the reproduction: 'curl: 1 / Curl: 1'. In both, the Warning: Failed to open the file line starts its own line after the meter rows' closing newline. Both exit 23.
- 2026-10-12 | 2026-10-12_1653.md | not re-audited | overlaps planted defect PD-201 in Curl.Console/CurlCommandRunner.cs, so the auditor's verdict (reproduces no) is set aside: Ran the reproduction as given against curl 8.21.0 (Schannel, Git for Windows): it printed 'curl: 1 / Curl: 1'. In both binaries the Warning line starts its own line after the meter row, both exit 23, and the two stderr.txt files are byte-for-byte identical (cmp).
- 2026-10-13 | 2026-10-13_0925.md | reproduces: no | Ran the reproduction with the reference path changed from 'C:\Program Files\Git\mingw64\bin\curl.exe', which no longer exists, to ucrt64\bin\curl.exe (curl 8.22.0 Schannel, the differential tool's reference). Output: 'curl: 1 / Curl: 1'. Both exit 23, and stderr.txt is byte-identical (cmp reports no difference). Each is 4 meter rows, the closing '0\r\n', then 'Warning: Failed to open the file missing.txt/f.txt: No such file or directory\r\n' on its own line.

## Log

- 2026-10-10: filed proposed.
- 2026-10-10: proposed -> accepted.
