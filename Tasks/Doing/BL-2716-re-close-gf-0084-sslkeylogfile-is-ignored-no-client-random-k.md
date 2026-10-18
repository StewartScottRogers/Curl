---
id: BL-2716
title: Re-close GF-0084: SSLKEYLOGFILE is ignored: no CLIENT_RANDOM key-log line is written for a TLS connection (on linux)
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Tls.UnitLibrary, Curl.Tls.UnitTests, Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Engine.UnitLibrary, Curl.Engine.UnitTests]
requirement: none
created: 2026-10-17
completed:
---
# BL-2716 — Re-close GF-0084: SSLKEYLOGFILE is ignored: no CLIENT_RANDOM key-log line is written for a TLS connection (on linux)

## Goal

Curl behaves as curl 8.22.0 does for every item of gap finding GF-0084 (SSLKEYLOGFILE is ignored: no CLIENT_RANDOM key-log line is written for a TLS connection), so a later gap analysis measures each of `behaviour:test2090` as `match`.

## Context

This is a Re-close task: every earlier task for GF-0084 ([BL-2327]) is Done, but the latest gap analysis still measures a gap, so the fix did not hold.

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

- [ ] `behaviour:test2090`: Curl answers what curl 8.22.0 answers on linux, `upstream test2090 passes`, so the item measures `match`.
- [ ] The gap-platforms workflow run on the branch (or the CI job on linux) passes for the named items; lanes test only on Windows, so a green Windows run alone does not close this task.
- [ ] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [ ] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md).

## Notes

## Log

- 2026-10-17: Created.
- 2026-10-17: Backlog -> Doing. Criteria at claim: 4a2aa586 8b83e917 1fd5de8c 28346828.
