---
id: GF-0092
title: An --ech ecl: handshake the server does not accept exits 35 instead of 101 (ECH required) on macOS
area: behaviour
platform: macos
key: behaviour:ech-config-list-against-macos-tls12-only-server
severity: High
status: open
scope: target
introduced-in:
opened: 2026-10-15_0751
closed:
regression: true
items: [behaviour:test4001]
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests]
task: BL-4379
tasks: [BL-2335, BL-4379]
---
# GF-0092 - An --ech ecl: handshake the server does not accept exits 35 instead of 101 (ECH required) on macOS

## Summary

Curl differs from upstream curl in behaviour on macos: An --ech ecl: handshake the server does not accept exits 35 instead of 101 (ECH required) on macOS.

## Evidence

behaviour:test4001 expected 'upstream test4001 passes', actual '<verify><errorcode>: expected exit code 101, got 35'. The case runs '--ech ecl:AEv+DQBHdAAgACCCU49qdxKOUXJPs3wlsM06v/t42sMH5xQOL37MAd3HaAAEAAEAAQAYb3RoZXJwdWJsaWMudGVzdC5kZWZvLmllAAA= -k https://%HOSTIP:%HTTPSPORT/4001' against the https server and verifies exit 101 for an OpenSSL build. The same case is match on windows in this run, and test4000 (--ech grease, which may fall back to TLS 1.2) is match on macos. Reproduce on macOS from the repository root: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- <release>/tests/data $env:TEMP/raw.json 4001

## Suggestion

Platform-specific (macOS). The likely cause is the harness, not Curl's ECH client. With an ecl: config list, HandBuiltTlsProvider.HandshakeAsync (Curl.Networking.UnitLibrary) offers TLS 1.3 only (settings.EchConfigs is not null, so runsTls12 is false; BL-1950), as curl's OpenSSL build does. The harness's HTTPS stand-in, Curl.Conformance.UnitLibrary/TlsServerStream.cs, is an SslStream server, and on macOS .NET's server-side SslStream negotiates at most TLS 1.2. The handshake therefore fails on the protocol version (exit 35) before the client can see that the server ignored the ECH offer and send ech_required (exit 101). Change Curl.Conformance.UnitLibrary so its HTTPS server speaks TLS 1.3 on macOS, as upstream's stunnel/OpenSSL server does: for example, serve TLS through Curl's hand-built TLS 1.3 server code (Curl.Tls.UnitLibrary) when the platform's SslStream server cannot. Add a unit test in Curl.Conformance.UnitTests that the macOS HTTPS stand-in completes a TLS 1.3-only ClientHello. Then rerun test4001 on macOS. If it still exits 35, check Tls13ClientHandshake's ech_required decision (TlsAlertDescription.EchRequired) against a server that ignores the ECH extension, and add a focused test in Curl.Tls.UnitTests (ADR-0482). Explained by ADR-0327 (ECH runs only on the hand-built client) and ADR-0359 (exit 101 text as measured with OpenSSL); neither explains the macOS-only exit 35.

## Measurements

- 2026-10-15_0751: 1 of 1 items are gaps.
- 2026-10-15_1657: 0 of 1 items are gaps.
- 2026-10-17_2225: 0 of 1 items are gaps.
- 2026-10-19_0804: 0 of 1 items are gaps.
- 2026-10-19_1329: 0 of 1 items are gaps.
- 2026-10-22_0148: 0 of 1 items are gaps.
- 2026-10-23_1637: 0 of 1 items are gaps.
- 2026-10-25_1542: 0 of 1 items are gaps.
- 2026-10-30_2103: 1 of 1 items are gaps.

## Log

- 2026-10-15_0751: Opened by gap-behaviour.
- 2026-10-15_0900: Filed BL-2335.
- 2026-10-15_1657: Closed: run 2026-10-15_1657 measured every item as match or excluded on macos.
- 2026-10-30_2103: Reopened as a regression: run 2026-10-30_2103 measured behaviour:test4001 as gap again on macos.
- 2026-10-30_2202: Filed BL-4379.
