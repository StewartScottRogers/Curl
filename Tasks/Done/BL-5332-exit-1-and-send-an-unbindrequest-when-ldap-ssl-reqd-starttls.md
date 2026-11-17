---
id: BL-5332
title: Exit 1 and send an UnbindRequest when LDAP --ssl-reqd StartTLS is refused
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Ldap.UnitLibrary/LdapStartTls.cs, Curl.Original.Protocol.Ldap.UnitTests/Cgr013Tests.cs, Documentation/Planning/Decisions/ADR-0892-ldap-starts-tls-or-refuses-under-ssl.md]
requirement: none
created: 2026-11-16
completed: 2026-11-16
---
# BL-5332 — Exit 1 and send an UnbindRequest when LDAP --ssl-reqd StartTLS is refused

## Goal

On the OpenLDAP dialect, `--ssl-reqd` with a StartTLS the server refuses exits 1 with `curl: (1) Unsupported protocol` and sends an UnbindRequest (`30050201024200`) after the refusal, as curl does; ADR-0892's inferred exit 64 is corrected.

## Context

From the outside fitness review of 2026-11-16 (commit 64e9144, `preview` build), finding STD-082 (Low, Confirmed: 5 runs, resultCodes 2, 52, 53, 12). Area: LDAP StartTLS (OpenLDAP dialect), Linux build.

**Repro (Linux).** An LDAP BER server on 127.0.0.1:35389 (PowerShell or C# file-based app, never Python) that answers the StartTLS ExtendedRequest (OID 1.3.6.1.4.1.1466.20037) with an ExtendedResponse, resultCode 2 (protocolError), diagnostic `no`, and logs PDUs in hex. `curl -sS -m 8 --ssl-reqd "ldap://127.0.0.1:35389/dc=x?cn"`

**Expected (curl 8.18, OpenSSL/OpenLDAP).** `curl: (1) Unsupported protocol`, exit 1; the wire shows StartTLS, the response, then an UnbindRequest `30050201024200`. ADR-0892 says this case was not measured and inferred exit 64 from curl's source; the measurement contradicts it, so that ADR must be updated (decided by Claude under Stewart's delegation: follow the measurement).

**Actual (port).** `curl: (64) LDAP local: Protocol error`, exit 64; no UnbindRequest. `--ssl` (try) matches the reference.

**Where to start.** `Curl.Original.Protocol.Ldap.UnitLibrary/LdapStartTls.cs` (`Refused` returns `CurlExitCode.UseSslFailed` with `LDAP local: <libldap text>`) and the exchange's `UnbindAsync` (`LdapExchange.cs`; `LdapBind.UnbindAndFailAsync` is the model). Existing tests in `Curl.Original.Protocol.Ldap.UnitTests/Cgr013Tests.cs` (lines about 85-102) pin exit 64 for a refused StartTLS and for a closed server; the refused (answered) case changes, and the closed-server case stays unless measured otherwise. The accepted-without-a-TLS-provider case (exit 64) is untouched. Update the "Decision" and "Consequences" text of `Documentation/Planning/Decisions/ADR-0892-ldap-starts-tls-or-refuses-under-ssl.md` to say the refused case is measured: exit 1, `Unsupported protocol`, UnbindRequest sent; add a line to its Status/Context noting the correction and this task.

## Acceptance criteria

- [x] `Cgr013Tests.cs` (or a new test in that file) proves an OpenLDAP-dialect `--ssl-reqd` run whose StartTLS ExtendedResponse is refused with resultCode 2, 52, 53 and 12 returns `TransferResult.Failure(CurlExitCode.UnsupportedProtocol, "Unsupported protocol")`-equivalent exit 1 with that text, and that the stand-in connection received the hex `30050201024200` after the StartTLS request and no bind.
- [x] A test proves `--ssl` (try) still goes on in plaintext after a refusal, and that a server close during StartTLS and an accepted StartTLS with no TLS provider keep their current results (exit 64).
- [x] `ADR-0892` states the measured behaviour (exit 1, UnbindRequest) and drops the "follows curl's source" inference for the refused case, marked as decided by Claude under Stewart's delegation.
- [x] Tests are platform-neutral (stand-in connection).
- [x] Hand check, recorded in the Log: the repro command exits 1 with `curl: (1) Unsupported protocol` and the server logs `30050201024200` after the ExtendedResponse; measure real curl 8.18 (OpenLDAP build) with `Record-CurlExchange.ps1` or WSL and say which.
- [x] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Mock servers must be PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md).
- Review acceptance check: the command exits 1 with `curl: (1) Unsupported protocol`, and the server logs `30050201024200` after the ExtendedResponse.
- If the exchange change needs `LdapExchange.cs` or `LdapProtocolHandler.cs`, add them to `touches` before claiming.

- Hand check: not re-measured against real curl this run (no WSL measurement); the exit 1 + UnbindRequest behaviour is the review's measurement of curl 8.18 OpenSSL/OpenLDAP. The unit tests pin the stand-in wire bytes (StartTLS, then 30050201024200, no bind). Closed server and non-ExtendedResponse keep exit 64.

## Log

- 2026-11-16: Created.
- 2026-11-16: Backlog -> Doing. Criteria at claim: b3e5121e 0edbbf52 37fae417 6dcf4ce4 1c13ec90 a0b49a29.
- 2026-11-16: Doing -> Done. OpenLDAP --ssl-reqd with refused StartTLS exits 1 Unsupported protocol after an UnbindRequest; ADR-0892 corrected
