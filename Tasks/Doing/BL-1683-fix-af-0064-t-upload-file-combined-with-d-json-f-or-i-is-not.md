---
id: BL-1683
title: Fix AF-0064: -T / --upload-file combined with -d, --json, -F or -I is not refused: Curl sends a PUT and exits 0 where curl warns and exits 2
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console]
requirement: none
created: 2026-10-08
completed:
---
# BL-1683 — Fix AF-0064: -T / --upload-file combined with -d, --json, -F or -I is not refused: Curl sends a PUT and exits 0 where curl warns and exits 2

## Goal

The defect the audit office reported as AF-0064 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0064 (High, conformance auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0064-t-upload-file-combined-with-d-json-f-or-i-is-not-r.md`.

Location: `Curl.Console/CurlCommandRunner.cs:1310`

Location: `Curl.Console/CurlCommandRunner.cs:1310`

Reference: curl 8.21.0 (x86_64-w64-mingw32) libcurl/8.21.0 Schannel zlib/1.3.2 brotli/1.2.0 zstd/1.5.7 libidn2/2.3.8 libpsl/0.21.5 libssh2/1.11.1 WinLDAP. Differential run seed 264985340, count 300: case 235 (-g --data-binary a=1&b=2 --upload-file sub/f.txt --login-options 1 URL) differed in exitcode, stderr and request. Reduced to `-d a=1 -T <file> URL` (also in reverse order). Real curl: stderr 'Warning: You can only select one HTTP request method! You asked for both PUT \nWarning: (-T, --upload-file) and POST (-d, --data).', no request sent, exit 2. Curl: no warning, sends 'PUT /upload.txt HTTP/1.1' with the file body, exit 0. Same divergence for -F a=1 -T (curl: '...PUT (-T, --upload-file) and multipart formpost (-F, --form).', exit 2), -I -T (curl: '...PUT (-T, --upload-file) and HEAD (-I, --head).', exit 2; Curl prints the 200 headers and exits 0) and -s -T f --json {} (curl exit 2 silently; Curl exits 0 and prints the body). Cause: SelectedHttpMethod (Curl.Cli.UnitLibrary/SelectedHttpMethod.cs) and CommandLineWarning.RequestMethodNames have no PUT entry, and RequestMethodConflictLines (CurlCommandRunner.cs:1310) checks only -d/--json against HEAD/GET, so -T never takes part in the one-method check; InferredRequestMethod (line 3331) silently picks a method instead. No test in Curl.Cli.UnitTests or Curl.Console.UnitTests covers -T with another method. Phase 2: no ADR under Documentation/Planning/Decisions records this as deliberate (searched for upload-file together with request-method conflict). Blocker under conformance-auditor (a different exit code), mapped to High.

Reproduction, from the finding:

Run from the repository root:

```powershell
$o="$env:TEMP\af-put"; $a=@('-d','a=1','-T','CLAUDE.md','http://127.0.0.1:50999/'); & ./Record-CurlExchange.ps1 -Port 50999 -Curl 'C:\Program Files\Git\mingw64\bin\curl.exe' -OutDirectory "$o\curl" -CurlArgs $a | Out-Null; & ./Record-CurlExchange.ps1 -Port 50999 -Curl (Resolve-Path Curl.Console/bin/Release/net10.0/curl.exe) -OutDirectory "$o\candidate" -CurlArgs $a | Out-Null; 'curl ' + (Get-Content "$o\curl\exitcode.txt") + ' / Curl ' + (Get-Content "$o\candidate\exitcode.txt")
```

- Expected: curl 2 / Curl 2, both printing 'Warning: You can only select one HTTP request method! You asked for both PUT ... (-T, --upload-file) and POST (-d, --data).' and sending no request
- Actual: curl 2 / Curl 0; Curl printed no warning and request.bin holds 'PUT /CLAUDE.md HTTP/1.1' with the file body

The finding closes only when a later re-audit by the conformance auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-08: Created.
- 2026-10-07: Backlog -> Doing.
