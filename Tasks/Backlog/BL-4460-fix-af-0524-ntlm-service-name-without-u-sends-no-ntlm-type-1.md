---
id: BL-4460
title: Fix AF-0524: --ntlm --service-name without -u sends no NTLM Type-1 Authorization where curl starts NTLM with the default Windows credentials
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-4461]
touches: [Curl.Original.Protocol.Http.UnitLibrary, Curl.Protocol.Abstractions.UnitLibrary/HttpRequestOptions.cs, Curl.Engine.UnitLibrary/HttpRequestOptionsMapping.cs]
requirement: none
created: 2026-11-02
completed:
---
# BL-4460 — Fix AF-0524: --ntlm --service-name without -u sends no NTLM Type-1 Authorization where curl starts NTLM with the default Windows credentials

## Goal

The defect the audit office reported as AF-0524 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0524 (High, conformance auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0524-ntlm-service-name-without-u-sends-no-ntlm-type-1-a.md`.

Location: `Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs:370`

Location: `Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs:370`

Reference: curl 8.22.0 (x86_64-w64-mingw32) libcurl/8.22.0 Schannel zlib/1.3.2 brotli/1.2.0 zstd/1.5.7 libidn2/2.3.8 libpsl/0.21.5 libssh2/1.11.1 WinLDAP. Differential seed 933153194, case 210 (--data @data.txt --create-dirs --ntlm --service-name - URL): exit, stdout, stderr and request differ. Reduced to '--ntlm --service-name x -m 5 URL'. curl sends 'Authorization: NTLM TlRMTVNTUAABAAAAB7II...' (Type-1, -v says 'Server auth using NTLM with user '''). With '-d x' curl sends the empty probe 'Content-Length: 0' first, then the body, and the one-response recorder times out with exit 28. Curl sends no Authorization header and posts the body at once (exit 0). Without --service-name neither binary sends NTLM, so --service-name is the trigger. OriginCredentialOf (line 370) makes an empty credential only for --sasl-authzid with Digest and otherwise gives null when there is no -u, so no NTLM handshake starts. Against an NTLM server a script that relies on the logon's single sign-on gets 401 from Curl. Phase 2: ADR-0142 covers default credentials only for '-u :'; no ADR covers --service-name without -u.

Reproduction, from the finding:

Run from the repository root:

```powershell
dotnet build Curl.Console -c Release -v q; & ./Record-CurlExchange.ps1 -Port 50613 -OutDirectory $env:TEMP/sn-curl -CurlArgs '--ntlm','--service-name','x','-m','5','http://127.0.0.1:50613/'; & ./Record-CurlExchange.ps1 -Port 50613 -Curl (Resolve-Path Curl.Console/bin/Release/net10.0/curl.exe).Path -OutDirectory $env:TEMP/sn-cand -CurlArgs '--ntlm','--service-name','x','-m','5','http://127.0.0.1:50613/'; Select-String -Path $env:TEMP/sn-curl/request.bin,$env:TEMP/sn-cand/request.bin -Pattern '^Authorization: NTLM'
```

- Expected: Both request.bin files contain 'Authorization: NTLM TlRMTVNTUAAB...' (curl's behaviour).
- Actual: Only curl's request.bin matches; Curl's request has no Authorization header.

The finding closes only when a later re-audit by the conformance auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- 2026-11-02 (lane 1): The HTTP handler cannot see `--service-name`: it reaches only `MailRequestOptions` and `NegotiateOptions`, never `HttpRequestOptions` or `ITransferContext`. The fix needs (1) a `ServiceName` property on `Curl.Protocol.Abstractions.UnitLibrary/HttpRequestOptions.cs`, (2) mapping `authentication.ServiceName` into it in `Curl.Engine.UnitLibrary/HttpRequestOptionsMapping.cs` (next to `SaslAuthorizationIdentity`), and (3) `OriginCredentialOf` in `HttpProtocolHandler.cs` giving `new NetworkCredential("", "")` (the ADR-0142 default-credential path) when there is no `-u`, `ServiceName` is set and NTLM is among the allowed schemes, plus an ADR (Decided by Claude under Stewart's delegation) and unit tests beside the AF-0522 ones in `HttpProtocolHandlerTests.Authentication.cs`. Touches widened to name those two files. BL-4461 holds the whole of Curl.Engine.UnitLibrary, so this task depends on BL-4461 and goes back to Backlog before any code edit; no code was changed.

## Log

- 2026-11-02: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-11-02: Doing -> Backlog. Needs Curl.Engine.UnitLibrary/HttpRequestOptionsMapping.cs to map --service-name into HttpRequestOptions; BL-4461 holds Curl.Engine.UnitLibrary
