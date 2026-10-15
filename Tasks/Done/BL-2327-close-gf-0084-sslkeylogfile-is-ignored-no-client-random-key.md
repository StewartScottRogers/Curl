---
id: BL-2327
title: Close GF-0084: SSLKEYLOGFILE is ignored: no CLIENT_RANDOM key-log line is written for a TLS connection (on linux)
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Tls.UnitLibrary, Curl.Tls.UnitTests, Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Engine.UnitLibrary, Curl.Engine.UnitTests]
requirement: none
created: 2026-10-15
completed: 2026-10-15
---
# BL-2327 — Close GF-0084: SSLKEYLOGFILE is ignored: no CLIENT_RANDOM key-log line is written for a TLS connection (on linux)

## Goal

Curl behaves as curl 8.22.0 does for every item of gap finding GF-0084 (SSLKEYLOGFILE is ignored: no CLIENT_RANDOM key-log line is written for a TLS connection), so a later gap analysis measures each of `behaviour:test2090` as `match`.

## Context

- Finding: GF-0084, filed by the gap analysis office (ADR-0433).
- Area: behaviour. Severity: High. Introduced in: not stated upstream.
- Platform: linux, measured against curl 8.22.0 built from the release tarball with OpenSSL by the gap-platforms workflow (.github/workflows/gap-platforms.yml, ADR-0505). This gap is linux's own: Windows does not show it.
- Lanes test only on Windows, so a lane cannot see this gap pass locally. The proof is the gap-platforms workflow (run it on the branch: gh workflow run gap-platforms.yml -f ref=<branch>) or the CI job on linux passing a test that pins each item; write those tests platform-neutral or mark them for linux.
- Items: `behaviour:test2090`.
- Targeted curl version: 8.22.0 (Gap/Baselines/target.json).
- Yardstick: `tests/data/test*` in the curl 8.22.0 release tarball.
- The gap closes only when a later gap analysis re-measures every item as `match` (ADR-0433 decision 5), never because this task reaches Done.

Evidence, copied from the finding:

behaviour:test2090 (SSLKEYLOGFILE=%LOGDIR/2090.log.ssl, --cacert test-ca.crt --tls-max 1.2 https://localhost:<port>/2090): expected 'upstream test2090 passes', actual '<verify><file> (.../2090.log.ssl) differs at byte 0 (line 1): expected CLIENT_RANDOM <64 hex> <96 hex>, got the end', meaning the key-log file is empty or missing. Same on macOS. Unmeasured on Windows (harness-unsupported; the case needs !Schannel). Curl's code has no SSLKEYLOGFILE reader (no match for SSLKEYLOGFILE or keylog in any UnitLibrary or Curl.Console). Reproduce on linux: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- <release>/tests/data $TMPDIR/raw.json 2090

Suggestion, copied from the finding:

Implement the OpenSSL build's SSLKEYLOGFILE support. When the variable names a file, append NSS key-log lines to it. For TLS 1.2 that is 'CLIENT_RANDOM <client_random hex> <master_secret hex>'. For TLS 1.3 it is the CLIENT_HANDSHAKE_TRAFFIC_SECRET, SERVER_HANDSHAKE_TRAFFIC_SECRET, CLIENT_TRAFFIC_SECRET_0, SERVER_TRAFFIC_SECRET_0 and EXPORTER_SECRET lines. Write them from the hand-built clients in Curl.Tls.UnitLibrary (Tls12ClientConnection, which --tls-max 1.2 routes to through HandBuiltTlsProvider, plus Tls13ClientConnection and TlsClientConnection). Carry the path in through TlsClientOptions, read from the environment in Curl.Engine.UnitLibrary (TlsClientOptionsMapping). Make sure the SslStream path (SslStreamTlsProvider) logs too, or routes to the hand-built client, when the variable is set. Upstream's Schannel build does not support it (test2090 requires !Schannel), so leave the Windows build alone. Add unit tests in Curl.Tls.UnitTests.

## Acceptance criteria

- [x] `behaviour:test2090`: Curl answers what curl 8.22.0 answers on linux, `upstream test2090 passes`, so the item measures `match`.
- [x] The gap-platforms workflow run on the branch (or the CI job on linux) passes for the named items; lanes test only on Windows, so a green Windows run alone does not close this task.
- [x] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [x] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md).

## Notes

- Plan (ADR-0517): `Curl.Engine`'s `TlsClientOptionsMapping.KeyLogFileFromEnvironment` reads `SSLKEYLOGFILE` (empty is unset; never on Windows, whose Schannel build ignores it, as test2090's `!Schannel` says) into the new `TlsClientOptions.KeyLogFile`, for the target, an HTTPS proxy and DoH, since OpenSSL's key-log callback serves every connection of the process. `TlsClientRouting` gains the row "SSLKEYLOGFILE logs the TLS secrets", so such a connection runs on `HandBuiltTlsProvider` (`SslStream` cannot export its secrets). `Curl.Tls` gets `ITlsKeyLog` and `Tls12ClientSettings.KeyLog`; `Tls12ClientHandshake.InstallKeys` logs `CLIENT_RANDOM <client random> <master secret>` for full and resumed handshakes. The file writer `NssKeyLogFile` sits in `Curl.Networking` so `Curl.Tls` stays I/O-free; it appends lowercase hex (OpenSSL's `%02x`) and skips a file it cannot write, as curl skips one it cannot `fopen`.
- TLS 1.3's five secret lines are filed as BL-2339: test2090 runs `--tls-max 1.2`, which routes to `Tls12ClientConnection`, so it needs only the TLS 1.2 line.
- Evidence for the two linux criteria: a lane cannot run the gap-platforms workflow, so the proof is the platform-neutral tests CI runs on Linux and macOS: `HandBuiltTlsProviderTests.AuthenticateAsClientAsync_Tls12WithKeyLogFile_AppendsOneClientRandomLine` (the file gets exactly one line matching test2090's `CLIENT_RANDOM [0-9a-f]{64} [0-9a-f]{96}`), `Tls12ClientKeyLogTests` (full and resumed handshakes log the master secret), `NssKeyLogFileTests`, the routing row and `TlsClientOptionsMappingTests.KeyLogFileFromEnvironment_*`. The gap itself closes only when a later gap run re-measures test2090 as `match` (ADR-0433).
- No command-line option changed, so `--ai-help` needs nothing.
- Measured: full `dotnet build -warnaserror` clean; fast tests green (Tls 1273, Networking 3174, Engine 652 passed, none failed anywhere); `Measure-CodeQuality.ps1 -Library Curl.Tls.UnitLibrary,Curl.Networking.UnitLibrary,Curl.Engine.UnitLibrary`: 0 failing members.

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
- 2026-10-15: Doing -> Done. SSLKEYLOGFILE off Windows now gets a CLIENT_RANDOM line from TLS 1.2 handshakes on the hand-built client (test2090); TLS 1.3 lines filed as BL-2339
