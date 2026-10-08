---
id: BL-1684
title: Fix AF-0065: --interface with an unbindable name plus --negotiate reports the Negotiate SSPI failure instead of curl's interface-binding failure
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console, Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-10-08
completed:
---
# BL-1684 — Fix AF-0065: --interface with an unbindable name plus --negotiate reports the Negotiate SSPI failure instead of curl's interface-binding failure

## Goal

The defect the audit office reported as AF-0065 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0065 (Low, conformance auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0065-interface-with-an-unbindable-name-plus-negotiate-r.md`.

Location: `Curl.Console/CurlCommandRunner.cs`

Location: `Curl.Console/CurlCommandRunner.cs`

Reference: curl 8.21.0 (x86_64-w64-mingw32) libcurl/8.21.0 Schannel zlib/1.3.2 brotli/1.2.0 zstd/1.5.7 libidn2/2.3.8 libpsl/0.21.5 libssh2/1.11.1 WinLDAP. Differential run seed 264985340, count 300: case 220 (--ftp-method GET --interface @f.txt --negotiate URL) differed in stderr only. Reduced to `--interface @f.txt --negotiate URL`; --interface @f.txt alone and --negotiate alone are the same as curl. Both exit 45. curl stderr: 'curl: (45) Failed to connect to 127.0.0.1:PORT after N ms: Failed binding local connection end'. Curl stderr: 'curl: (45) InitializeSecurityContext failed: SEC_E_NO_CREDENTIALS (0x8009030e) - No credentials are available in the security package'. Curl sets up the Negotiate security context before binding the local interface, where curl binds first and never reaches authentication; scripts that parse the message see different text. Phase 2: the Negotiate ADRs (ADR-0176, ADR-0227) do not mention --interface or this order; no ADR records the divergence. Major/Minor under conformance-auditor: a stderr-only wording difference on an uncommon path, mapped to Low.

Reproduction, from the finding:

Run from the repository root:

```powershell
$o="$env:TEMP\af-if"; $a=@('--interface','@f.txt','--negotiate','-s','-S','http://127.0.0.1:50998/'); & ./Record-CurlExchange.ps1 -Port 50998 -Curl 'C:\Program Files\Git\mingw64\bin\curl.exe' -OutDirectory "$o\curl" -CurlArgs $a | Out-Null; & ./Record-CurlExchange.ps1 -Port 50998 -Curl (Resolve-Path Curl.Console/bin/Release/net10.0/curl.exe) -OutDirectory "$o\candidate" -CurlArgs $a | Out-Null; Get-Content "$o\curl\stderr.txt", "$o\candidate\stderr.txt"
```

- Expected: Both lines: 'curl: (45) Failed to connect to 127.0.0.1:50998 after N ms: Failed binding local connection end'
- Actual: curl: '...Failed binding local connection end'; Curl: 'curl: (45) InitializeSecurityContext failed: SEC_E_NO_CREDENTIALS (0x8009030e) - No credentials are available in the security package'

The finding closes only when a later re-audit by the conformance auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- 2026-10-07 (lane 9) diagnosis: the cause is not in `Curl.Console` and not specific to
  `--interface`. `HttpProtocolHandler.WithFirstAuthorizationFailure`
  (`Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs`, ~line 327) replaces the message of
  *any* failed transfer with the first line the authenticator reported while making the first
  `Authorization` value (ADR-0344, BL-955). Curl makes the Negotiate context before connecting;
  curl makes it only once connected, so a connect failure never meets it. Measured with Git's
  curl 8.21.0: `curl --negotiate -sS http://127.0.0.1:50998/` (nothing listening) gives
  `curl: (7) Failed to connect to 127.0.0.1:50998 after N ms: Could not connect to server`;
  Curl gives `curl: (7) InitializeSecurityContext failed: SEC_E_NO_CREDENTIALS ...`. Same for
  `--interface @f.txt --negotiate` (exit 45).
- Planned fix: in `WithFirstAuthorizationFailure`, keep the result's own message when the
  transfer failed before a connection opened (the failure `ConnectAndExchangeAsync` returns
  for `connect.Connection is null`, and `FailBeforeConnecting`). Mark those results (e.g. a
  flag on the plan/result, or check the report's connect stage) and add unit tests in
  `Curl.Protocol.Http.UnitTests` for a refused connect and a local-bind failure with
  `--negotiate` and a failing context, keeping BL-955's 401 test as is.
- Needs `Curl.Protocol.Http.UnitLibrary` and `Curl.Protocol.Http.UnitTests`, added to
  `touches`; BL-1609 (in Doing) holds them, so this task went back to Backlog until it is done.

## Log

- 2026-10-08: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Backlog. Fix is in Curl.Protocol.Http.UnitLibrary (HttpProtocolHandler.WithFirstAuthorizationFailure), which BL-1609 in Doing touches; resume once BL-1609 is Done
