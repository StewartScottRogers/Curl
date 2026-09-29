---
id: BL-526
title: Answer NTLM challenges for HTTP as the platform's curl does
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-525, BL-684]
touches: [Curl.Authentication.UnitLibrary, Curl.Authentication.UnitTests, Curl.Console, Curl.Console.UnitTests, Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests, Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Curl.Cli.UnitLibrary, Curl.Cli.UnitTests, Documentation/Planning/Decisions/ADR-0180-http-ntlm-answers-in-three-legs-from-a-fresh-context-per-leg.md, Documentation/Planning/Decisions/README.md]
requirement: none
created: 2026-09-28
completed: 2026-09-28
---
# BL-526 — Answer NTLM challenges for HTTP as the platform's curl does

## Goal

`--ntlm -u user:pass` (and `--anyauth` when NTLM ranks first) sends curl 8.21.0's NTLM Type 1 message, answers the server's Type 2 challenge with a Type 3 message on the same connection, and fails as curl does when the server refuses, using the mechanism BL-525's ADR decides, on every platform.

## Context

- Prerequisite of audit rows 14, 34 and 39 (see BL-525). ADR-0028 ranks NTLM; `Curl.Authentication.UnitLibrary/RankedHttpAuthenticator.cs` and `HttpAuthSchemeRanking.cs` pick it, and `BasicAndBearerAuthenticator.cs` currently answers nothing for it.
- Standing rule (root `CLAUDE.md`, "Decisions", 2026-09-28): NTLM works on every platform, hand-built where the BCL lacks it. `Curl.Authentication.UnitLibrary` references `Curl.Ntlm.UnitLibrary` (BL-682 to BL-684) for the messages and responses; add the reference here.
- NTLM authenticates the connection, so the handshake must stay on one connection (`Curl.Protocol.Http.UnitLibrary` keep-alive handling, ADR-0034 for the 401 retry).
- `Record-CurlExchange.ps1` can serve a canned `401` with `WWW-Authenticate: NTLM <type2>` on one connection; the Type 1 bytes curl sends are deterministic, the Type 3 bytes contain a client nonce and time, so pin structure and fields, not whole bytes, where they are random.

## Acceptance criteria

- [x] Measured first with `Record-CurlExchange.ps1` (`-HoldOpenMilliseconds` so the second request arrives on the same connection): `--ntlm -u u:p` against a `401 NTLM` then a `401` with a fixed Type 2 then a `200`; request bytes, stderr and exit code copied into Notes.
- [x] `Curl.Authentication.UnitTests` pin the Type 1 bytes exactly and the Type 3 fields (flags, domain, user, workstation, NTLMv2 response computed from a fixed client nonce and time injected through the seam).
- [x] A `Curl.Console.UnitTests` test runs the three-leg exchange through the HTTP handler with a fake connector and pins the requests.
- [x] NTLM works on Windows, Linux and macOS through the route BL-525's ADR gives each platform (the hand-built `Curl.Ntlm.UnitLibrary`, BL-683 and BL-684, wherever the ADR uses it); no platform refuses it, and where platforms' measured bytes or messages differ each is pinned in its own `OSCondition` test.
- [x] `curl -V` lists `NTLM` among the features on every platform (ADR-0021), with a test.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

### Measured 2026-09-28

`Record-CurlExchange.ps1 -Script` (one connection; steps `read`, `send <response>`, `close`),
curl 8.21.0 (mingw, Schannel, SSPI) on Windows 11 and, with `-Curl wsl.exe -ListenAddress
172.26.96.1`, curl 8.18.0 (OpenSSL, curl's own NTLM) on Ubuntu. `-HoldOpenMilliseconds`
only holds a connection after its one response, so `-Script` was the way to answer several
requests on one connection; no change to the script was needed. Type 2 = MS-NLMP 4.2.4.3's
CHALLENGE, `TlRMTVNTUAACAAAADAAMADgAAAAzgoriASNFZ4mrze8AAAAAAAAAACQAJABEAAAABgBwFwAAAA9TAGUAcgB2AGUAcgACAAwARABvAG0AYQBpAG4AAQAMAFMAZQByAHYAZQByAAAAAAAA`.

- `--ntlm -u u:p`, 401 Type 2, 200 (both): request 1 carries Type 1 (Windows
  `TlRMTVNTUAABAAAAB4IIogAAAAAAAAAAAAAAAAAAAAAKAPRlAAAADw==`, Linux
  `TlRMTVNTUAABAAAABoIIAAAAAAAAAAAAAAAAAAAAAAA=`), request 2 Type 3 on the same connection
  ("Reusing existing http: connection"), stdout `ok`, stderr empty with `-sS`, exit 0.
  Linux's Type 3, byte for byte:
  `TlRMTVNTUAADAAAAGAAYAEAAAABUAFQAWAAAAAAAAACsAAAAAgACAKwAAAAWABYArgAAAAAAAAAAAAAAM4KK4iWAoKMN+kq8Eimq1kt9xV8afTTK0zM6hWRUG/gzNnfh/LfhEpJFHEgBAQAAAAAAAIAkZ3HbT90BGn00ytMzOoUAAAAAAgAMAEQAbwBtAGEAaQBuAAEADABTAGUAcgB2AGUAcgAAAAAAAAAAAHUAVwBPAFIASwBTAFQAQQBUAEkATwBOAA==`
  (flags 0xE28A8233, user `u`, workstation `WORKSTATION`, client challenge
  `1A7D34CAD3333A85`, time FILETIME `0x01DD4FDB71672480`). Windows's Type 3 starts
  `TlRMTVNTUAADAAAAGAAYAHgAAADcANwAkAAAA...`, flags 0xA2888205, workstation the machine name, a MIC.
- `--ntlm`, 401 bare `NTLM`, 401 Type 2, 200 (Windows): Type 1, Type 1, Type 3, exit 0.
- `--ntlm`, 401 bare `NTLM` twice (both): Type 1, Type 1, then "NTLM handshake failure",
  stdout the 401 body `nope`, exit 0; two requests.
- `--ntlm`, 401 Type 2, 401 bare `NTLM` (both): Type 1, Type 3, "NTLM handshake rejected",
  stdout `nope`, exit 0.
- `--anyauth -u u:p`, 401 `Basic realm="r"` + `NTLM`, 401 Type 2, 200 (both): no
  Authorization, Type 1, Type 3, one connection, exit 0.
- `--ntlm`, 401 `NTLM TlRMTVNTUAACAAAA` (Type 2 cut short): Windows `curl: (94) An
  authentication function returned an error`, stdout empty; Linux "NTLM handshake failure
  (bad type-2 message)", stdout `nope`, exit 0.
- `--ntlm`, 401 `NTLM @@@notbase64` (both): "NTLM authentication problem, ignoring",
  stdout `nope`, exit 0.

### Plan and decisions (ADR-0180)

- `IHttpAuthenticator` gains a default `ContinueAuthorizationAsync(request, sentAuthorization,
  sentBeforeAnyChallenge, challenges)` answering null; the HTTP handler calls it for a 401
  to a request that already sent `Authorization` (it used to never retry then). Only NTLM
  overrides it, so Basic/Digest/Negotiate, RTSP and WebSocket are unchanged.
  `HttpAuthenticationFailedException` carries exit 94 to the handler.
- No context is held between legs: Type 3 comes from a fresh context stepped through Type 1
  then Type 2. Both routes write the same Type 1 each time (probed on SSPI), so the MIC
  still matches.
- `NtlmHttpAuthenticator(contexts, refusedChallengeFailsTransfer)`; Console passes
  `OperatingSystem.IsWindows()`. `HandBuiltNtlmSecurityContext` replaces
  `UnavailableSecurityContext` (deleted) in `HandBuiltSecurityContextFactory`, which now
  takes an `INtlmRandomSource`.
- Domain split for NTLM is curl's `NtlmUserName.SplitDomain` (backslash first, then slash),
  not Negotiate's first-of-either.
- Choices with a default taken: after Type 3, another Type 2 gets nothing (curl would loop;
  bounded here); a Type 3 past 1024 bytes ends on the 401 rather than curl's
  `CURLE_TOO_LARGE`; the `-v` NTLM lines are not written. The last two are filed as
  follow-ups BL-847 and BL-846.

### Touches added

`Curl.Protocol.Abstractions.UnitLibrary`/`.UnitTests` (the continuation member and the
exception: the HTTP handler cannot see the authenticator otherwise),
`Curl.Protocol.Http.UnitLibrary`/`.UnitTests` (the handler never retried a 401 after a
credential was sent), `Curl.Cli.UnitLibrary`/`.UnitTests` (`CurlVersionText` holds the `-V`
feature list), ADR-0180 and the decisions README. No task in Doing (BL-728, BL-747,
BL-787, BL-823) names any of them.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. --ntlm and --anyauth answer NTLM in three legs on one connection: SSPI on Windows, curl's own NTLM byte for byte elsewhere; -V lists NTLM (ADR-0180)
