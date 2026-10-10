---
id: AF-0064
title: -T / --upload-file combined with -d, --json, -F or -I is not refused: Curl sends a PUT and exits 0 where curl warns and exits 2
auditor: conformance
severity: High
status: accepted
reason: 
key: conformance:Curl.Console/CurlCommandRunner.cs:upload-file-method-conflict:exit-code
reproduction: none
task: BL-1683
tasks: BL-1683
found: 2026-10-07
found-at: 0fcb5afc262ef32bb48ad058cf1f4a2b2c68d511
scorecard: 2026-10-07_1336.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0064 - -T / --upload-file combined with -d, --json, -F or -I is not refused: Curl sends a PUT and exits 0 where curl warns and exits 2

## Summary

High finding from the conformance auditor at `Curl.Console/CurlCommandRunner.cs:1310`: -T / --upload-file combined with -d, --json, -F or -I is not refused: Curl sends a PUT and exits 0 where curl warns and exits 2. Reported by an auditor flagged unreliable in 2026-10-07_1336.md.

## Evidence

Location: `Curl.Console/CurlCommandRunner.cs:1310`

Reference: curl 8.21.0 (x86_64-w64-mingw32) libcurl/8.21.0 Schannel zlib/1.3.2 brotli/1.2.0 zstd/1.5.7 libidn2/2.3.8 libpsl/0.21.5 libssh2/1.11.1 WinLDAP. Differential run seed 264985340, count 300: case 235 (-g --data-binary a=1&b=2 --upload-file sub/f.txt --login-options 1 URL) differed in exitcode, stderr and request. Reduced to `-d a=1 -T <file> URL` (also in reverse order). Real curl: stderr 'Warning: You can only select one HTTP request method! You asked for both PUT \nWarning: (-T, --upload-file) and POST (-d, --data).', no request sent, exit 2. Curl: no warning, sends 'PUT /upload.txt HTTP/1.1' with the file body, exit 0. Same divergence for -F a=1 -T (curl: '...PUT (-T, --upload-file) and multipart formpost (-F, --form).', exit 2), -I -T (curl: '...PUT (-T, --upload-file) and HEAD (-I, --head).', exit 2; Curl prints the 200 headers and exits 0) and -s -T f --json {} (curl exit 2 silently; Curl exits 0 and prints the body). Cause: SelectedHttpMethod (Curl.Cli.UnitLibrary/SelectedHttpMethod.cs) and CommandLineWarning.RequestMethodNames have no PUT entry, and RequestMethodConflictLines (CurlCommandRunner.cs:1310) checks only -d/--json against HEAD/GET, so -T never takes part in the one-method check; InferredRequestMethod (line 3331) silently picks a method instead. No test in Curl.Cli.UnitTests or Curl.Console.UnitTests covers -T with another method. Phase 2: no ADR under Documentation/Planning/Decisions records this as deliberate (searched for upload-file together with request-method conflict). Blocker under conformance-auditor (a different exit code), mapped to High.

## Reproduction

Run from the repository root:

```powershell
$o="$env:TEMP\af-put"; $a=@('-d','a=1','-T','CLAUDE.md','http://127.0.0.1:50999/'); & ./Record-CurlExchange.ps1 -Port 50999 -Curl 'C:\Program Files\Git\mingw64\bin\curl.exe' -OutDirectory "$o\curl" -CurlArgs $a | Out-Null; & ./Record-CurlExchange.ps1 -Port 50999 -Curl (Resolve-Path Curl.Console/bin/Release/net10.0/curl.exe) -OutDirectory "$o\candidate" -CurlArgs $a | Out-Null; 'curl ' + (Get-Content "$o\curl\exitcode.txt") + ' / Curl ' + (Get-Content "$o\candidate\exitcode.txt")
```

- Expected: curl 2 / Curl 2, both printing 'Warning: You can only select one HTTP request method! You asked for both PUT ... (-T, --upload-file) and POST (-d, --data).' and sending no request
- Actual: curl 2 / Curl 0; Curl printed no warning and request.bin holds 'PUT /CLAUDE.md HTTP/1.1' with the file body

## Re-audits

- 2026-10-08 | 2026-10-08_0748.md | not re-audited | overlaps planted defect PD-301 in Curl.Console/CurlCommandRunner.cs, so the auditor's verdict (reproduces no) is set aside: Ran the reproduction (-d a=1 -T CLAUDE.md): output 'curl 2 / Curl 2'. Both wrote the same two lines: 'Warning: You can only select one HTTP request method! You asked for both PUT ' / 'Warning: (-T, --upload-file) and POST (-d, --data).'
- 2026-10-08 | 2026-10-08_2315.md | not re-audited | overlaps planted defect PD-301 in Curl.Console/CurlCommandRunner.cs, so the auditor's verdict (reproduces no) is set aside: Ran the reproduction against curl 8.21.0 Schannel: 'curl 2 / Curl 2'. Both print 'Warning: You can only select one HTTP request method! You asked for both PUT (-T, --upload-file) and POST (-d, --data).'
- 2026-10-09 | 2026-10-09_0225.md | reproduces: no | Ran the reproduction against curl 8.21.0 Schannel: 'curl 2 / Curl 2'. Both print 'Warning: You can only select one HTTP request method! You asked for both PUT' / 'Warning: (-T, --upload-file) and POST (-d, --data).'
- 2026-10-09 | 2026-10-09_0647.md | not re-audited | overlaps planted defect PD-301 in Curl.Console/CurlCommandRunner.cs, so the auditor's verdict (reproduces no) is set aside: Ran the reproduction (with $env:TEMP set to the scratch folder): 'curl 2 / Curl 2'. Both binaries print the same two lines, 'Warning: You can only select one HTTP request method! You asked for both PUT' / 'Warning: (-T, --upload-file) and POST (-d, --data).'. Reference curl 8.21.0 Schannel.
- 2026-10-09 | 2026-10-09_1435.md | not re-audited | overlaps planted defect PD-301 in Curl.Console/CurlCommandRunner.cs, so the auditor's verdict (reproduces no) is set aside: Ran the reproduction: 'curl 2 / Curl 2'. Both wrote the same two warning lines ('Warning: You can only select one HTTP request method! You asked for both PUT ' / 'Warning: (-T, --upload-file) and POST (-d, --data).'). Reference curl 8.21.0 Schannel (Git for Windows).

## Log

- 2026-10-07: filed proposed.
- 2026-10-07: proposed -> accepted.
