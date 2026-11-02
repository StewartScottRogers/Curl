---
id: BL-4457
title: Fix AF-0522: --digest --sasl-authzid with a body and no -u sends the body at once where curl sends the empty Digest probe POST first
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4458]
touches: [Curl.Original.Protocol.Http.UnitLibrary, Curl.Original.Protocol.Http.UnitTests, Curl.Protocol.Abstractions.UnitLibrary/HttpRequestOptions.cs, Curl.Protocol.Abstractions.UnitTests/HttpRequestOptionsTests.cs, Curl.Engine.UnitLibrary/HttpRequestOptionsMapping.cs, Curl.Engine.UnitTests/HttpRequestOptionsMappingTests.cs]
requirement: none
created: 2026-11-01
completed: 2026-11-01
---
# BL-4457 — Fix AF-0522: --digest --sasl-authzid with a body and no -u sends the body at once where curl sends the empty Digest probe POST first

## Goal

The defect the audit office reported as AF-0522 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0522 (Medium, conformance auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0522-digest-sasl-authzid-with-a-body-and-no-u-sends-the.md`.

Location: `Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs:397`

Location: `Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs:397`

Reference: curl 8.22.0 (x86_64-w64-mingw32) libcurl/8.22.0 Schannel zlib/1.3.2 brotli/1.2.0 zstd/1.5.7 libidn2/2.3.8 libpsl/0.21.5 libssh2/1.11.1 WinLDAP. Found by Invoke-DifferentialConformance.ps1, seed 1278816542, case 23 (`--data-raw @- --digest --url-query @data.txt --sasl-authzid 1`). There the reference timed out (the one-shot recorder answers only its probe) and Curl exited 0. Reduced by dropping one option at a time to `--digest --sasl-authzid 1 -d x`; without --sasl-authzid both binaries match. curl -v shows `* Server auth using Digest with user ''`, then the first request `POST / ... Content-Length: 0` with no body, then `Issue another request to this URL` with the 1-byte body. Curl's only request is `POST / ... Content-Length: 1` with body `x`. With `-u a:b --digest -d x` both send the Content-Length: 0 probe, so the gap is the condition: SendsDigestProbe requires `request.Credential is not null` (line 400), and --sasl-authzid alone gives no credential, but curl 8.22.0 treats it as an empty user and probes. A server sees a different request sequence (two requests instead of one). Phase 2: ADR-0441 (a Digest POST probes with an empty body) states the rule only for credentials and does not address --sasl-authzid without -u; not explained.

Reproduction, from the finding:

Run from the repository root:

```powershell
$d=(New-Item -ItemType Directory -Force (Join-Path $env:TEMP 'af-digest-authzid')).FullName; Push-Location $d; [Environment]::CurrentDirectory=$d; dotnet build (Join-Path (Get-Location -PSProvider FileSystem).Path '..') *> $null; Pop-Location; dotnet build Curl.Console -c Release -v q; $exe=(Resolve-Path Curl.Console/bin/Release/net10.0/curl.exe).Path; $rec=(Resolve-Path Record-CurlExchange.ps1).Path; & $rec -Port 58803 -OutDirectory (Join-Path $d 'curl') -CurlArgs '--digest','--sasl-authzid','1','-d','x','-m','5','http://127.0.0.1:58803/' *> $null; & $rec -Port 58803 -Curl $exe -OutDirectory (Join-Path $d 'cand') -CurlArgs '--digest','--sasl-authzid','1','-d','x','-m','5','http://127.0.0.1:58803/' *> $null; Select-String -Path (Join-Path $d 'curl/request.bin'),(Join-Path $d 'cand/request.bin') -Pattern '^Content-Length'
```

- Expected: Both request.bin files contain `Content-Length: 0` (curl's Digest probe; curl then exits 28 against the one-shot recorder)
- Actual: curl/request.bin: `Content-Length: 0` (curl exit 28); cand/request.bin: `Content-Length: 1` with body `x` (Curl exit 0)

The finding closes only when a later re-audit by the conformance auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- 2026-11-01 (lane 3, first claim, no code edited): measured real curl 8.22.0 with
  Record-CurlExchange.ps1. `--digest --sasl-authzid 1 -d x` sends `POST` with
  `Content-Length: 0` and logs `* Server auth using Digest with user ''`; `--digest
  --sasl-authzid 1` (GET) logs the same line and sends no Authorization header;
  `--sasl-authzid 1` and `--basic --sasl-authzid 1` send no Authorization header and no
  auth line. So curl treats --sasl-authzid as an empty user for Digest only; giving the
  Engine an empty `NetworkCredential` for every scheme would wrongly send `Basic Og==`.
- The HTTP handler cannot see --sasl-authzid: it reaches only `MailRequestOptions`, which
  `MailRequestOptionsMapping` builds for mail schemes alone. Plan: add
  `SaslAuthorizationIdentity` (string?) to `HttpRequestOptions`
  (Curl.Protocol.Abstractions.UnitLibrary/HttpRequestOptions.cs), set it from
  `EasyAuthenticationOptions.SaslAuthorizationIdentity` in
  Curl.Engine.UnitLibrary/HttpRequestOptionsMapping.cs, and in
  `HttpProtocolHandler.SendsDigestProbe` (line 397) accept `request.Credential is not null
  || options.SaslAuthorizationIdentity is not null` for the origin, answering the Digest
  challenge with an empty user (`new NetworkCredential("", "")`) when only the authzid is
  set; then an ADR extending ADR-0441 and focused unit tests in each project.
- Touches widened to those Abstractions and Engine files for that reason. BL-4458 holds
  the whole Curl.Protocol.Abstractions.UnitLibrary, so this task depends on it and goes
  back to Backlog until it is Done (lane rule 3). No commits carry code for this task yet.

- 2026-11-01 (lane 1, second claim): done as planned above. `HttpRequestOptions.SaslAuthorizationIdentity`, set by `HttpRequestOptionsMapping.Of`; `HttpProtocolHandler.OriginCredentialOf` gives an empty `NetworkCredential` when there is no credential, the authzid is set and Digest is the one scheme allowed, so `SendsDigestProbe` probes and the challenge is answered for user `''`. Limited to Digest-only because Basic must not send `Basic Og==` (measured) and `--anyauth` was not measured (ADR-1182). The finding's reproduction, run against the Debug build, now gives `Content-Length: 0` for both curl and Curl. Fast tests of the three changed test projects green; Measure-CodeQuality.ps1 for the three libraries: 0 failing members. Change is platform-neutral (no paths, OS text or certificates).

## Log

- 2026-11-01: Created.
- 2026-11-01: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-11-01: Doing -> Backlog. Needs Curl.Protocol.Abstractions.UnitLibrary/HttpRequestOptions.cs, which BL-4458 holds; waits on BL-4458
- 2026-11-01: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-11-01: Doing -> Done. Digest-only --sasl-authzid without -u now answers as an empty user and sends the empty probe POST first (ADR-1182)
