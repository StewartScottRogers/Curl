---
id: BL-610
title: Honour --cert-status and --ssl-auto-client-cert on every platform
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-607, BL-705, BL-708]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Console, Curl.Console.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-28
completed: 2026-09-29
---
# BL-610 — Honour --cert-status and --ssl-auto-client-cert on every platform

## Goal

`--cert-status` checks the server's stapled OCSP response and fails with exit 91 `CURLE_SSL_INVALIDCERTSTATUS` and curl 8.21.0's message when it is missing, revoked or invalid, and `--ssl-auto-client-cert` presents a client certificate picked from the user's certificate store when the server asks for one, both on Windows, Linux and macOS; and whether any Curl option can produce exit 83 (`CURLE_SSL_ISSUER_ERROR`) is measured and recorded.

## Context

- Conformance audit 2026-09-28, row 17 (Major; exits 83 and 91 never produced). Option: BL-607.
- Standing rule (root `CLAUDE.md`, "Decisions", 2026-09-28): if any official curl build supports a feature, Curl supports it on every platform, output text matching the platform's curl where both do the same thing. OpenSSL builds check the stapled response; `SslStream` exposes none, so `--cert-status` routes the transfer through the hand-built TLS client (BL-705 verifies the response; BL-708 routes; add the routing row). `--ssl-auto-client-cert` is Schannel's (`SslClientAuthenticationOptions.LocalCertificateSelectionCallback` over `X509Store(StoreName.My, StoreLocation.CurrentUser)` on Windows); off Windows the same store API reaches .NET's user store (and the keychain on macOS) and the option does the same there.
- Record how each is honoured per platform in an ADR marked "Decided by Claude under Stewart's delegation" (HOW, not WHETHER), with the measured text each platform matches.

## Acceptance criteria

- [x] Measured first with `Record-CurlExchange.ps1 -Tls -k` (and `-NoServer` against `openssl s_server -status_file <resp>` for stapling): `--cert-status` with a good, a revoked and no stapled response, and `--ssl-auto-client-cert` against a server requesting a client certificate, on Windows and on Linux or macOS, and with curl.se's official Windows build; stderr and exit code copied into Notes, plus a note on exit 83 (the CLI has no issuer-certificate option in 8.21.0 if that is what the manual shows).
- [x] Tests pin, on every platform, a transfer passing with a good stapled response and failing with exit 91 and the measured message for revoked and missing ones, and an automatically chosen client certificate presented from a fake store; where platforms' texts differ, each is pinned in its own `OSCondition` test.
- [x] The ADR exists in `Documentation/Planning/Decisions` (number checked unused) and is indexed in its `README.md`.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

- Measured 2026-09-29 against `openssl s_server -status_file <resp>` directly (the criterion's
  `-NoServer` route: `Record-CurlExchange.ps1` has no stapling server, and `-NoServer` only runs
  curl), with a throwaway CA and a `localhost` leaf, responses from `openssl ocsp -index`:
  - curl 8.18.0 / OpenSSL 3.5.5 (Ubuntu under WSL), `--cacert ca.crt --cert-status`: good exit 0;
    revoked `curl: (91) SSL certificate revocation reason: keyCompromise (1)`; no reason
    `curl: (91) SSL certificate revocation reason: (UNKNOWN) (-1)`; none stapled (also with `-k`)
    `curl: (91) No OCSP response received`; unknown `curl: (91) SSL server certificate status
    verification FAILED`; tryLater `curl: (91) Invalid OCSP response status: trylater (3)`;
    signed by another CA `curl: (91) OCSP response verification failed`; reasons 0-6 and 8
    print OpenSSL's names (`unspecified` ... `removeFromCRL`). `-v` adds `* SSL certificate
    status: good (0)` / `revoked (1)`.
  - curl.se's Windows build 8.18.0 / LibreSSL 4.2.1 (`--cacert`): the same texts and exit 91;
    with `-k` a stapled response fails `OCSP response verification failed` (no trusted CA).
  - curl 8.21.0 Schannel (System32 and MSYS2): `--cert-status` is ignored, exit 0 for good,
    revoked and none.
  - `--ssl-auto-client-cert` against `s_server -Verify 1`: Schannel presented a `CurrentUser\MY`
    certificate with no EKU, exit 0, `-v` shows `* schannel: enabled automatic use of client
    certificate`; OpenSSL and LibreSSL builds ignore it: `curl: (56) ... tlsv13 alert certificate
    required`.
  - Exit 83: only libcurl's `CURLOPT_ISSUERCERT` sets it, and the 8.21.0 manual names no
    command-line option for it (only the exit-code table's "Issuer check failed."), so no Curl
    option produces it.
- Decisions in ADR-0191 (Decided by Claude under Stewart's delegation). Lane 1 numbered it 0188; other
  lanes took 0188-0190 first, so it is 0191 now. `--cert-status` is a routing row to
  `HandBuiltTlsProvider` on every platform with OpenSSL's text (curl.se's Windows build checks,
  so the Schannel build's silence is not matched); `--ssl-auto-client-cert` and
  `--proxy-ssl-auto-client-cert` choose from `CurrentUser\MY` through `IClientCertificateStore`
  on every platform (`AutomaticClientCertificate`: private key, valid now, no EKU or client-auth
  or any), before the handshake, in `ClientCertificateLoader.Load`; `--cert` wins.
- Sensible default: the automatic choice ignores the server's acceptable-issuer list, because
  the hand-built client takes its certificate up front; recorded in the ADR.
- Tests: `Curl.Networking.UnitTests/Fakes/Tls13Server` is a copy of `Curl.Tls.UnitTests`'
  in-memory TLS 1.3 server, record server and OCSP builder (as `Fakes/QuicTestServer` copies
  Curl.Quic's), with the three internals it used replaced by BCL calls; `Curl.Tls.UnitTests`'
  own servers are internal to that project and `InternalsVisibleTo` would touch
  `Curl.Tls.UnitLibrary`, outside `touches`. A TLS 1.3 server stapling a status the client
  never asked for is refused by the client, so "no check without --cert-status" stays pinned in
  `Curl.Tls.UnitTests` only.
- Quality: `Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary,Curl.Console`: Networking
  100% line, 100% branch, 615 members, 0 failing (worst CRAP 10); Console 100%/100%, 603 members,
  0 failing. Fast tests: every assembly passed (Networking 1417 passed with Integration).
- Filed: BL-875 (the `-v` status line and the Schannel automatic-certificate line).
- Second run (lane 3, 2026-09-29): lane 1's commits were cherry-picked onto the current branch;
  the only conflict was the ADR index row, and the ADR was renumbered 0191 (0188-0190 taken).
  With the rebased tree `dotnet build Curl.slnx -warnaserror` is clean and every fast-test
  assembly passes (Networking 1419, Console 1533, Tls 746; Networking, Console and Tls rerun
  twice more, all green), so lane 1's integration failure came from another lane's work that has
  since been fixed. `Measure-CodeQuality.ps1`: Networking 100%/100%, 618 members, 0 failing;
  Console 100%/100%, 605 members, 0 failing. The follow-up lost with lane 1's board commit is
  refiled as BL-875.


## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Backlog. Lane 1 could not integrate: fast tests failed after rebasing onto the other lanes' work. The work is on branch factory/BL-610-lane-1-20260928-212155; start with git cherry-pick --no-commit factory/BL-610-lane-1-20260928-212155 and fix it.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. --cert-status checks the stapled OCSP response through the hand-built TLS client on every platform (exit 91, OpenSSL's text), and --ssl-auto-client-cert presents a CurrentUser\MY certificate in both providers
