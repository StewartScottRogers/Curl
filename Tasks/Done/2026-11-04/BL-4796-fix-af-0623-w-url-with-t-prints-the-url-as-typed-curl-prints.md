---
id: BL-4796
title: Fix AF-0623: -w %{url} with -T prints the URL as typed; curl prints it with the uploaded file name appended
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console]
requirement: none
created: 2026-11-03
completed: 2026-11-03
---
# BL-4796 — Fix AF-0623: -w %{url} with -T prints the URL as typed; curl prints it with the uploaded file name appended

## Goal

The defect the audit office reported as AF-0623 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0623 (Medium, conformance auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0623-w-url-with-t-prints-the-url-as-typed-curl-prints-i.md`.

Location: `Curl.Console/CurlCommandRunner.cs:2593`

Location: `Curl.Console/CurlCommandRunner.cs:2593`

Reference: curl 8.22.0 (x86_64-w64-mingw32) libcurl/8.22.0 Schannel zlib/1.3.2 brotli/1.2.0 zstd/1.5.7 libidn2/2.3.8 libpsl/0.21.5 libssh2/1.11.1 WinLDAP. Differential run, seed 791054, case 44 (--write-out %{url} --upload-file sub/f.txt --disable -3 URL): both send the same PUT /f.txt and exit 0, but curl's stdout is 'http://127.0.0.1:50589/f.txt' and Curl's is 'http://127.0.0.1:50589/'. Smallest reduction: `-w %{url} -T sub/f.txt http://127.0.0.1:PORT/`. It differs even when the file is missing: both exit 26, curl prints 'http://127.0.0.1:50700/f.txt' and Curl prints 'http://127.0.0.1:50700/'. Cause: ReportAsync passes givenUrl, the URL before UploadTransferUrl appends the -T file name, to WriteOutAsync as the %{url} value (CurlCommandRunner.cs:2593, documented at 3305-3307 as 'as the glob expanded it'). curl's tool prints the URL after it has added the file name. Phase 2: ADR-0051 (-T URLs are resolved before the transfer) and ADR-0553 do not record %{url} printing the unresolved URL.

Reproduction, from the finding:

Run from the repository root:

```powershell
dotnet build Curl.Console -c Release; $a=@('-w','%{url}','-T','sub/f.txt','http://127.0.0.1:50700/'); & .\Record-CurlExchange.ps1 -Port 50700 -Curl 'C:\Program Files\Git\ucrt64\bin\curl.exe' -OutDirectory $env:TEMP\wurl\curl -CurlArgs $a; & .\Record-CurlExchange.ps1 -Port 50700 -Curl Curl.Console\bin\Release\net10.0\curl.exe -OutDirectory $env:TEMP\wurl\cand -CurlArgs $a; Get-Content $env:TEMP\wurl\curl\stdout.bin, $env:TEMP\wurl\cand\stdout.bin
```

- Expected: Both print http://127.0.0.1:50700/f.txt
- Actual: curl prints http://127.0.0.1:50700/f.txt; Curl prints http://127.0.0.1:50700/

The finding closes only when a later re-audit by the conformance auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- `%{url}` now prints the URL curl's tool holds after `add_file_name_to_url`: when `-T` appends the file name (path names no file, not standard input, URL parses), the resolved upload URL; otherwise the URL as given (`CurlCommandRunner.UrlWithUploadFileName`). An unparseable URL keeps the as-given text, as before (default: no measurement says otherwise).
- Pinned by `Curl.Console.UnitTests/Af0623UploadWriteOutUrlTests.cs` (appended, missing file exit 26, URL naming a file, `-T -`, unparseable URL).
- Reproduction re-run against curl 8.22.0 Schannel: both print `http://127.0.0.1:50700/f.txt`. Measure-CodeQuality for Curl.Console: 0 failing members. Full fast tests green on Windows; the change is platform-neutral (no paths beyond forward-slash relative names).

## Log

- 2026-11-03: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-11-03: Doing -> Done. %{url} on a -T upload prints the URL with the file name appended, as curl does
