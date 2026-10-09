---
id: BL-1875
title: Fix AF-0125: --crlf is ignored on HTTP uploads: Curl sends LF bytes with Content-Length where curl converts to CRLF and sends chunked
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-10-09
completed:
---
# BL-1875 — Fix AF-0125: --crlf is ignored on HTTP uploads: Curl sends LF bytes with Content-Length where curl converts to CRLF and sends chunked

## Goal

The defect the audit office reported as AF-0125 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0125 (High, conformance auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0125-crlf-is-ignored-on-http-uploads-curl-sends-lf-byte.md`.

Location: `Curl.Cli.UnitLibrary/CommandLineOptionTable.cs:192`

Location: `Curl.Cli.UnitLibrary/CommandLineOptionTable.cs:192`

Reference: curl 8.21.0 (x86_64-w64-mingw32) libcurl/8.21.0 Schannel. Differential seed 1306208109, case 300: --post303 --crlf --form f=@f.txt --post302 http://127.0.0.1:PORT/ (differs in request). Reduced to --crlf -F f=@f.txt URL. With f.txt = 'file body\n', curl sends 'Transfer-Encoding: chunked' and the part body 'file body\r\n' (request 414 bytes). Curl sends 'Content-Length: 202' and 'file body\n' (395 bytes). The same happens with --crlf -T f.txt (curl: chunked 'b\r\nfile body\r\n'; Curl: Content-Length: 10, 'file body\n') and with --crlf --data-binary @f.txt. Without --crlf both binaries send identical requests. ConvertLineEndings is set at CommandLineOptionTable.cs:192 and flows into TransferContext, but only Curl.Protocol.File.UnitLibrary (CrlfUploadConverter) reads it. No HTTP code converts line endings. Phase 2: ADR-0003's amendment says --crlf applies to uploads generally but measured only file://. It does not record HTTP as a deliberate divergence.

Reproduction, from the finding:

Run from the repository root:

```powershell
$o="$env:TEMP\cf-crlf"; New-Item -ItemType Directory -Force $o | Out-Null; [IO.File]::WriteAllText("$o\f.txt","file body`n"); $a=@('--crlf','-T',"$o\f.txt",'http://127.0.0.1:51005/'); & ./Record-CurlExchange.ps1 -Port 51005 -Curl 'C:\Program Files\Git\mingw64\bin\curl.exe' -OutDirectory "$o\curl" -CurlArgs $a | Out-Null; & ./Record-CurlExchange.ps1 -Port 51005 -Curl (Resolve-Path Curl.Console/bin/Release/net10.0/curl.exe) -OutDirectory "$o\candidate" -CurlArgs $a | Out-Null; 'curl ' + (Get-Item "$o\curl\request.bin").Length + ' / Curl ' + (Get-Item "$o\candidate\request.bin").Length
```

- Expected: curl 133 / Curl 133 (Transfer-Encoding: chunked, body 'file body\r\n')
- Actual: curl 133 / Curl 114 (Content-Length: 10, body 'file body\n')

The finding closes only when a later re-audit by the conformance auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- 2026-10-09 (lane 4): `CommandLineOptionTable.cs:192` already sets `ConvertLineEndings` and
  `TransferContextFactory` passes it into the transfer context; the CLI side is correct. Only the
  File and FTP handlers read it. The fix is in the HTTP handler: when `ConvertLineEndings` is on,
  convert the upload body (`-T`, `-F` file parts, `--data-binary @file`) LF -> CRLF and send it
  chunked, as curl does (it cannot know the converted length up front). So `touches` gains
  `Curl.Protocol.Http.UnitLibrary` and `Curl.Protocol.Http.UnitTests`. BL-1863, in Doing, touches
  `Curl.Protocol.Http.UnitLibrary`, so the task goes back to Backlog until BL-1863 is Done.
  Reuse the line-ending logic of `Curl.Protocol.File.UnitLibrary/CrlfUploadConverter.cs` (copy it
  into the HTTP library; protocols never reference each other).

## Log

- 2026-10-09: Created.
- 2026-10-09: Backlog -> Doing.
- 2026-10-09: Doing -> Backlog. Needs Curl.Protocol.Http.UnitLibrary, which BL-1863 (in Doing) touches
- 2026-10-09: Backlog -> Doing.
