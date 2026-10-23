---
id: BL-3357
title: Fix AF-0380: --remote-name-all given after -T applies to the upload URL: Curl warns 'No remote filename' and writes curl_response where curl does neither
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary]
requirement: none
created: 2026-10-23
completed:
---
# BL-3357 — Fix AF-0380: --remote-name-all given after -T applies to the upload URL: Curl warns 'No remote filename' and writes curl_response where curl does neither

## Goal

The defect the audit office reported as AF-0380 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0380 (Medium, conformance auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0380-remote-name-all-given-after-t-applies-to-the-uploa.md`.

Location: `Curl.Cli.UnitLibrary/CommandLineOptions.cs:2354`

Location: `Curl.Cli.UnitLibrary/CommandLineOptions.cs:2354`

Reference: curl 8.22.0 (x86_64-w64-mingw32) libcurl/8.22.0 Schannel zlib/1.3.2 brotli/1.2.0 zstd/1.5.7 libidn2/2.3.8 libpsl/0.21.5 libssh2/1.11.1 WinLDAP. Differential case 263 (seed 850517410): `--upload-file sub/f.txt --remote-name-all http://127.0.0.1:PORT/`, differs in stderr. Reduced to `-T f.txt --remote-name-all http://127.0.0.1:PORT/` (f.txt present): both exit 0 and send the same PUT /f.txt. curl prints nothing and writes no file. Curl prints `Warning: No remote filename, uses "curl_response"` and creates an empty curl_response. With a named URL (`-T f.txt --remote-name-all http://127.0.0.1:PORT/a.txt`), curl writes no file and Curl writes a.txt. Option order decides this in curl: `--remote-name-all -T f.txt URL/` makes both binaries warn and write curl_response, and `-H X:1 --remote-name-all URL/b.txt` makes both write b.txt. So curl's -T creates the URL's output entry with the --remote-name-all setting in force at that point. In Curl, -T only appends to UploadFiles (around line 2292). The URL's UrlOutput is created later by AddUrlOutput (`new(RemoteNameAll)`, line 2354), after --remote-name-all has been set.

Reproduction, from the finding:

Run from the repository root:

```powershell
dotnet build Curl.Console -c Release; $r=(Get-Location).Path; foreach ($n in 'curl','cand') { $d=New-Item -ItemType Directory -Force "$env:TEMP\af-rna\$n"; Set-Content "$d\f.txt" 'file body'; Push-Location $d; [Environment]::CurrentDirectory=$d.FullName; $a=@{Port=51892; OutDirectory=$d.FullName; CurlArgs=@('-T','f.txt','--remote-name-all','http://127.0.0.1:51892/')}; if ($n -eq 'cand') { $a.Curl="$r\Curl.Console\bin\Release\net10.0\curl.exe" }; & "$r\Record-CurlExchange.ps1" @a | Out-Null; Pop-Location; "$n warning=$([bool](Select-String -Path $d\stderr.txt -Pattern 'No remote filename' -Quiet)) curl_response=$(Test-Path $d\curl_response)" }
```

- Expected: curl warning=False curl_response=False / cand warning=False curl_response=False
- Actual: curl warning=False curl_response=False / cand warning=True curl_response=True

The finding closes only when a later re-audit by the conformance auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-23: Created.
