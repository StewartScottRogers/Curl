---
id: BL-527
title: Answer Negotiate challenges for HTTP as the platform's curl does
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-525, BL-691, BL-692, BL-684, BL-694]
touches: [Curl.Authentication.UnitLibrary, Curl.Authentication.UnitTests, Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Console, Curl.Console.UnitTests, Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests, Curl.Protocol.Http.UnitLibrary, Curl.Cli.UnitLibrary, Curl.Cli.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-28
completed: 2026-09-28
---
# BL-527 — Answer Negotiate challenges for HTTP as the platform's curl does

## Goal

`--negotiate -u :` (and `--anyauth` when Negotiate ranks first) answers a `WWW-Authenticate: Negotiate` challenge with a SPNEGO token for `HTTP@<host>`, through the mechanism BL-525's ADR decides, carrying a Kerberos token (or NTLM when SPNEGO falls back to it), and fails as curl 8.21.0 does when no credentials or no ticket are available, on every platform.

## Context

- Prerequisite of audit rows 14, 16, 23 and 34 (see BL-525). Ranking: ADR-0028, `Curl.Authentication.UnitLibrary/RankedHttpAuthenticator.cs`.
- Standing rule (root `CLAUDE.md`, "Decisions", 2026-09-28): Negotiate works on every platform, hand-built where the BCL lacks it. Pieces: SPNEGO (BL-692), the GSS-API Kerberos mechanism (BL-691, `Curl.Kerberos.UnitLibrary`), NTLM (BL-684, `Curl.Ntlm.UnitLibrary`); `Curl.Authentication.UnitLibrary` references both libraries.
- This task also composes the production adapters the Kerberos library needs, in `Curl.Networking.UnitLibrary`: the KDC transport (UDP, then TCP with the length prefix, RFC 4120 section 7.2.1) behind a thin datagram/TCP seam, and the SRV lookup over the hand-built DNS client (BL-694); both wired in `Curl.Console/CurlTransports.cs` or `CurlComposition.cs`.
- Unit tests cannot reach a KDC; the ADR's seam lets tests supply tokens. The only real-curl facts that can be measured without a domain are the failure paths: `--negotiate -u :` against a `401 Negotiate` with no ticket (stderr, exit code, whether a second request is sent).
- `--service-name` and `--delegation` (row 23) are applied later by the service-name task; leave a place for them in the options this task adds.

## Acceptance criteria

- [x] Measured first with `Record-CurlExchange.ps1`: `--negotiate -u : -v` against a `401` with `WWW-Authenticate: Negotiate`, on Windows and on Linux or macOS; request bytes, stderr and exit code copied into Notes.
- [x] `Curl.Authentication.UnitTests` with a fake token source pin the `Authorization: Negotiate <base64>` header, the service principal name built from the host, and the measured failure behaviour.
- [x] A `Curl.Console.UnitTests` test runs the exchange through the HTTP handler with fake connector and token source.
- [x] Each platform's measured failure is pinned in its own `OSCondition` test; a successful Kerberos exchange (fake KDC through the transport seam, fake acceptor) passes on every platform, and no platform refuses Negotiate.
- [x] `Curl.Networking.UnitTests` cover the KDC transport adapter's UDP-to-TCP switch and framing through its seam.
- [x] `curl -V` lists `Kerberos`, `SPNEGO` and `GSS-API` among the features on every platform (ADR-0021), with a test.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

- Measured 2026-09-28 with `Record-CurlExchange.ps1`, every connection answered `HTTP/1.1 401 Unauthorized`, `WWW-Authenticate: Negotiate`, `Content-Length: 4`, body `deny`; no ticket, no domain.
  - Windows, curl 8.21.0 (mingw, SSPI), `--negotiate -u : -v -m 5 http://127.0.0.1:48527/`: one request, `GET / HTTP/1.1` / `Host: 127.0.0.1:48527` / `User-Agent: curl/8.21.0` / `Accept: */*`, no `Authorization`; stdout `deny`; exit 0. Stderr (progress meter lines left out): `*   Trying 127.0.0.1:48527...`, `* Established connection to 127.0.0.1 (127.0.0.1 port 48527) from 127.0.0.1 port 58931 `, `* using HTTP/1.x`, `* InitializeSecurityContext failed: SEC_E_NO_CREDENTIALS (0x8009030e) - No credentials are available in the security package`, `* Server auth using Negotiate with user ''`, the `> ` request lines, `* Request completely sent off`, `< HTTP/1.1 401 Unauthorized`, the `InitializeSecurityContext failed` line again, `< WWW-Authenticate: Negotiate`, `< Content-Length: 4`, `< `, `{ [4 bytes data]`, `* Connection #0 to host 127.0.0.1:48527 left intact`.
  - Linux, curl 8.18.0 (OpenSSL, mit-krb5 1.22.1) in WSL against the Windows host: the same request bytes (`User-Agent: curl/8.18.0`), stdout and exit 0; the failure line is `* gss_init_sec_context() failed: No credentials were supplied, or the credentials were unavailable or inaccessible. SPNEGO cannot find mechanisms to negotiate. ` (trailing space), before the request and again after the 401.
  - Windows, also: `--negotiate` with no `-u`, `-u u:p`, and `http://localhost:...` all give `SEC_E_NO_CREDENTIALS` and one request. `--anyauth -u :` sends two requests, neither with `Authorization` (filed as BL-841).
- Found: the BCL's `NegotiateAuthentication` on the same machine answers Negotiate with an NTLM Type 1 (bare for `HTTP/127.0.0.1`, inside a NegTokenInit for `HTTP/localhost`) where curl's SSPI stops. Decided in ADR-0173 (by Claude under Stewart's delegation): on Windows a first Negotiate token that carries NTLM counts as no credentials, so Curl sends nothing, as curl does. That departs from the Goal's "or NTLM when SPNEGO falls back to it": measured curl never falls back here. A Kerberos token passes untouched.
- Decided in ADR-0173: `ISecurityContext.NextTokenAsync` is asynchronous (the hand-built route asks a KDC), not ADR-0142's synchronous `NextToken`; `Wrap`/`Unwrap` are left to BL-538 and BL-615. `IHttpAuthenticator` gains a default `CreateAuthorizationAsync` that the HTTP handler now calls, so no other authenticator or fake changes. `--negotiate` alone is tried before the first request (libcurl's picked-equals-wanted rule), and after a 401 only with `-u`.
- The seam is `ISecurityContextFactory`/`ISecurityContext`/`SecurityContextRequest`/`SecurityContextStep` in `Curl.Protocol.Abstractions.UnitLibrary`, as ADR-0142 places it. `SecurityContextRequest.ServiceName` (`NegotiateHttpAuthenticator.HttpServiceName`) and `.Delegation` are the places BL-631 fills in from `--service-name` and `--delegation`. The hand-built route ignores `Delegation` until BL-831.
- `touches` grew, per rule 3, with no task in Doing naming any of them: `Curl.Protocol.Abstractions.UnitLibrary` and `.UnitTests` (ADR-0142 puts the seam there and says BL-527 adds it), `Curl.Protocol.Http.UnitLibrary` (the handler must call the async authenticator), `Curl.Cli.UnitLibrary` and `.UnitTests` (`-V`'s features line lives in `CurlVersionText`), and `Documentation/Planning/Decisions` (ADR-0173).
- Sensible defaults taken: the KDC UDP reply wait is one second (MIT's first per-KDC wait); SRV lookups ask the system's DNS servers, whatever `--dns-servers` says; the `<uid>` of `/tmp/krb5cc_<uid>` comes from `/proc/self/status`, since the BCL has no `getuid`, and is 0 where that file is absent (Windows, macOS, where the hand-built route's default cache is never reached).
- The hand-built route's tests reuse `Curl.Kerberos.UnitTests`' `FakeKdc`, `FakeGssAcceptor` and friends by linking the files into `Curl.Authentication.UnitTests`, not copying them.
- The Linux/macOS-only tests (`OSCondition` excluding Windows) could not run here: WSL has no .NET. CI runs them.
- Follow-ups filed: BL-840 (`-v` lines), BL-841 (`--anyauth` second request), BL-842 (continuation legs and `ws://`).

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. --negotiate and --anyauth answer Negotiate through SSPI, the system GSS-API or hand-built SPNEGO+Kerberos, and send nothing without a ticket as both platform curls do
