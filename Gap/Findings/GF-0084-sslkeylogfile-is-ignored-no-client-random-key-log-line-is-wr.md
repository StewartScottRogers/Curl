---
id: GF-0084
title: SSLKEYLOGFILE is ignored: no CLIENT_RANDOM key-log line is written for a TLS connection
area: behaviour
platform: linux
key: behaviour:sslkeylogfile-not-written
severity: High
status: closed
scope: target
introduced-in:
opened: 2026-10-15_0751
closed: 2026-10-19_0804
regression: false
items: [behaviour:test2090]
touches: [Curl.Tls.UnitLibrary, Curl.Tls.UnitTests, Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Engine.UnitLibrary, Curl.Engine.UnitTests]
task: BL-2716
tasks: [BL-2327, BL-2716]
---
# GF-0084 - SSLKEYLOGFILE is ignored: no CLIENT_RANDOM key-log line is written for a TLS connection

## Summary

Curl differs from upstream curl in behaviour on linux: SSLKEYLOGFILE is ignored: no CLIENT_RANDOM key-log line is written for a TLS connection.

## Evidence

behaviour:test2090 (SSLKEYLOGFILE=%LOGDIR/2090.log.ssl, --cacert test-ca.crt --tls-max 1.2 https://localhost:<port>/2090): expected 'upstream test2090 passes', actual '<verify><file> (.../2090.log.ssl) differs at byte 0 (line 1): expected CLIENT_RANDOM <64 hex> <96 hex>, got the end', meaning the key-log file is empty or missing. Same on macOS. Unmeasured on Windows (harness-unsupported; the case needs !Schannel). Curl's code has no SSLKEYLOGFILE reader (no match for SSLKEYLOGFILE or keylog in any UnitLibrary or Curl.Console). Reproduce on linux: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- <release>/tests/data $TMPDIR/raw.json 2090

## Suggestion

Implement the OpenSSL build's SSLKEYLOGFILE support. When the variable names a file, append NSS key-log lines to it. For TLS 1.2 that is 'CLIENT_RANDOM <client_random hex> <master_secret hex>'. For TLS 1.3 it is the CLIENT_HANDSHAKE_TRAFFIC_SECRET, SERVER_HANDSHAKE_TRAFFIC_SECRET, CLIENT_TRAFFIC_SECRET_0, SERVER_TRAFFIC_SECRET_0 and EXPORTER_SECRET lines. Write them from the hand-built clients in Curl.Tls.UnitLibrary (Tls12ClientConnection, which --tls-max 1.2 routes to through HandBuiltTlsProvider, plus Tls13ClientConnection and TlsClientConnection). Carry the path in through TlsClientOptions, read from the environment in Curl.Engine.UnitLibrary (TlsClientOptionsMapping). Make sure the SslStream path (SslStreamTlsProvider) logs too, or routes to the hand-built client, when the variable is set. Upstream's Schannel build does not support it (test2090 requires !Schannel), so leave the Windows build alone. Add unit tests in Curl.Tls.UnitTests.

## Measurements

- 2026-10-15_0751: 1 of 1 items are gaps.
- 2026-10-15_1657: 1 of 1 items are gaps.
- 2026-10-17_2225: 1 of 1 items are gaps.
- 2026-10-19_0804: 0 of 1 items are gaps.
- 2026-10-19_1329: 0 of 1 items are gaps.
- 2026-10-22_0148: 0 of 1 items are gaps.
- 2026-10-23_1637: 0 of 1 items are gaps.
- 2026-10-25_1542: 0 of 1 items are gaps.
- 2026-10-30_2103: 0 of 1 items are gaps.

## Log

- 2026-10-15_0751: Opened by gap-behaviour.
- 2026-10-15_0900: Filed BL-2327.
- 2026-10-17_2318: Filed BL-2716.
- 2026-10-19_0804: Closed: run 2026-10-19_0804 measured every item as match or excluded on linux.
