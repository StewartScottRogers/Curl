---
id: BL-1105
title: Send --tls-earlydata as 0-RTT early data on a resumed --ssl-sessions session
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-710]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Tls.UnitLibrary, Curl.Tls.UnitTests, Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests, Curl.Cli.UnitLibrary, Curl.Cli.UnitTests, Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-10-01
completed: 2026-10-01
---
# BL-1105 — Send --tls-earlydata as 0-RTT early data on a resumed --ssl-sessions session

## Goal

`--tls-earlydata` routes to the hand-built TLS client and, on a TLS 1.3 session resumed from `--ssl-sessions` whose server allows early data, sends the first HTTP request as 0-RTT early data, as curl 8.21.0's OpenSSL build does, on every platform.

## Context

- Split from BL-710, which delivered `--ssl-sessions` (ADR-0319): `TlsSessionCache`, `HandBuiltTlsProvider` offering `ResumptionSession`, and the file load and save in `Curl.Console`.
- `Curl.Tls.UnitLibrary/Tls13ClientConnection.ConnectWithEarlyDataAsync` (BL-701) already sends early data, but needs the request bytes before the handshake: the HTTP handler today hands the TLS provider a connection and writes the request after the handshake. A deferred-handshake connection (the handshake runs on the first write, carrying it as early data) is the likely seam.
- The `Curl.Networking.Fakes` TLS 1.3 test server sends no NewSessionTicket and accepts no PSK; extend it (or reuse `Curl.Tls.UnitTests`' resumption server) so a resumed handshake runs through `HandBuiltTlsProvider` end to end.
- curl's `lib/vtls/openssl.c` at tag `curl-8_21_0` (`ossl_connect_step2` early data path) and `docs/cmdline-opts/tls-earlydata.md` give the `-v` lines; measure with an OpenSSL build through `Record-CurlExchange.ps1 -Tls -k` when one is available, and say so in Notes when not.

## Acceptance criteria

- [x] A `TlsClientRoutingTests` row sends `--tls-earlydata` to the hand-built client.
- [x] A test resumes a session through `HandBuiltTlsProvider` (second handshake offers the ticket the first received) and pins the early-data request bytes and the `-v` lines.
- [x] Without a resumable session, or when the server rejects early data, the request is sent after the handshake and the transfer still succeeds, pinned by a test.
- [x] `--ai-help` describes `--tls-earlydata` as honoured.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

- 2026-10-01 (lane 1, run ended on its budget; the code is in the shift's stash, uncommitted):
  - Measured curl 8.21.0 source (`lib/vtls/vtls.c` `Curl_on_session_reuse`, `ssl_cf_connect_deferred`;
    `lib/vtls/openssl.c` `ossl_send_earlydata`). `-v` lines, in order: `SSL session allows N bytes of early
    data, reusing ALPN 'P'` at connect (only when the session has an ALPN the connection offers; the hello
    then offers that ALPN alone and the connection reports it as negotiated), `SSL sending N bytes of early
    data` on the first write, then the handshake lines, then `Server accepted N bytes of TLS early data.` or
    `Server rejected TLS early data.`
  - Done in the stash, building clean: `TlsClientConnection.ConnectWithEarlyDataAsync` and an `earlyData`
    argument on `Tls13ClientConnection.Create` (Curl.Tls, two new tests in `TlsClientConnectionTests`);
    `TlsClientRouting` row for `AllowEarlyData` with its `TlsClientRoutingTests` row; `HandBuiltTlsProvider`
    split into `CompleteHandshakeAsync` + `DeferHandshake` + `HandshakeWithEarlyDataAsync` with a
    `HandshakeRun` record, early_data appended to the profile's extension order; new
    `EarlyDataTlsConnection` (handshake on first write); new Abstractions
    `DeferredTlsHandshakeFailedException`, mapped by `HttpConnectionSend` to the handshake's exit code
    (test in `HttpProtocolHandlerTests.Timeouts`); `Fakes/Tls13Server` now resumes tickets and reads or
    skips early data (`Tickets`, `AcceptEarlyData`, `EarlyData`, `SkippedRecords`).
  - Still to do: `HandBuiltTlsProviderTests.EarlyData.cs` (accepted, rejected, no session, ALPN mismatch,
    failed deferred handshake: seed the cache with `Track` + `Export`, then `Take`), `EarlyDataTlsConnectionTests`,
    `--ai-help` (Curl.Cli, add to touches), ADR, coverage run, and a follow-up task for `-w tls_earlydata`
    (Curl.Output, still a fixed 0).
  - Touches widened: Curl.Tls.UnitLibrary, Curl.Tls.UnitTests, Curl.Protocol.Abstractions.UnitLibrary,
    Curl.Protocol.Abstractions.UnitTests (no task in Doing on origin/work/dark-factory named them).
- 2026-10-01 (lane 6): restored lane 1's work from its stash entry by diff (no stash command), then:
  - Fixed `ClientSettings.ToTls13` not passing `OfferEarlyData` on, so the deferred hello offered no early_data.
  - Added `HandBuiltTlsProviderTests.EarlyData` (accepted, rejected, partly over `max_early_data_size`, no
    session, a session allowing none, without ALPN, of another ALPN, a TLS 1.2 ceiling, `--tls-earlydata` off,
    a failing deferred handshake) and `EarlyDataTlsConnectionTests`; and
    `CurlAiHelpTextTests.TryGetMarkdown_TlsEarlyData_DescribesItAsHonoured` (the `--ai-help` section already
    described it as supported, since the option parses; the test pins it).
  - Complexity: the early-data row moved into `TlsClientRouting.ControlsSessionsEarlyDataOrTheBeastSplit`, and
    `EarlyDataProtocolOf` split out of `EarlyDataApplicationProtocol`; its TLS 1.3 version check dropped, as
    `TlsSessionCache.Take` returns only TLS 1.3 sessions.
  - No OpenSSL-build curl was available to `Record-CurlExchange.ps1`; the `-v` lines are pinned from the
    curl-8_21_0 source reading above (ADR-0337 says so).
  - ADR-0337 records the design. Follow-up filed: BL-1150 (`-w tls_earlydata` still prints 0).
  - Coverage: Curl.Networking, Curl.Tls, Curl.Protocol.Abstractions, Curl.Protocol.Http and Curl.Cli are all
    100% line and branch with no failing member.

## Log

- 2026-10-01: Created.
- 2026-10-01: Backlog -> Doing.
- 2026-10-01: Doing -> Backlog. Run ended on its budget mid-implementation; code is in the shift stash, remaining steps under Notes
- 2026-10-01: Backlog -> Doing.
- 2026-10-01: Doing -> Done. --tls-earlydata sends the first request as 0-RTT early data on a resumed --ssl-sessions session through the hand-built TLS client
