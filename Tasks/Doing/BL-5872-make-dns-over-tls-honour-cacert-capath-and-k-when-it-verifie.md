---
id: BL-5872
title: Make --dns-over-tls honour --cacert, --capath and -k when it verifies the DNS-over-TLS server's certificate
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.EncryptedDns.UnitLibrary/DnsOverTls, Curl.Extension.EncryptedDns.UnitTests/DnsOverTls, Curl.Extension.EncryptedDns.UnitLibrary/EncryptedDnsExtension.cs, Curl.Extension.EncryptedDns.UnitLibrary/CLAUDE.md, Curl.Extension.Abstractions.UnitLibrary/ExtensionOptionValues.cs, Curl.Extension.Abstractions.UnitLibrary/ExtensionTlsTrust.cs, Curl.Extension.Abstractions.UnitLibrary/CLAUDE.md, Curl.Console/CurlComposition.cs, Curl.Console.UnitTests/ExtensionCommandLineOptionTests.cs]
requirement: none
created: 2026-11-24
completed:
---
# BL-5872 — Make --dns-over-tls honour --cacert, --capath and -k when it verifies the DNS-over-TLS server's certificate

## Goal

`--dns-over-tls <host:port>` verifies the DNS-over-TLS server's certificate against `--cacert`/`--capath` and skips verification under `-k`, as `--doh-url` already does, so a lookup through a test server with a private CA resolves instead of exiting 6.

## Context

From Surly cookbook bug report `2026-11-24T1330-Curl-bug-report.md` (batch 14 of the 1,000-recipe plan), finding 1 (Medium). Build tested: Curl `2145a49eae`, Surl v0.1.5 `d64faa4bb`, Windows 11. Re-run and confirmed the same day.

Repro: `surl -v --cert server.pem --key server.key --dns-transport tls dns://127.0.0.1:18853/`; then `curl -sS -m 10 --dns-over-tls 127.0.0.1:18853 --cacert server.pem http://api.example.test:18080/` exits 6 `Could not resolve host`, and so does `-k` in place of `--cacert`. Surl logs `TLS handshake failed: Received an unexpected EOF or 0 bytes from the transport stream.` for each attempt: the client hangs up in the handshake, so it rejected the certificate. Start in `Curl.Extension.EncryptedDns.UnitLibrary/DnsOverTls` (`DnsOverTlsResolver`, `DnsOverTlsResolverHook`) and compare how the DoH path passes the transfer's TLS trust settings to its connection. `--dns-over-tls` is an extension (curl has no such option), so the rule to follow is curl's `--doh-url`, which uses the transfer's `--cacert`/`--capath`/`-k` unless the `--doh-*` overrides apply; if the extension has its own `--dns-over-tls-insecure`-style override, keep it and document which wins.

## Acceptance criteria

- [ ] With a test-double DoT server whose certificate chains to a private CA, `--dns-over-tls` with `--cacert <ca>` resolves the name, pinned by a unit test.
- [ ] With `-k` and an untrusted certificate the lookup resolves; without `-k` or `--cacert` it still fails as now (exit 6), each pinned by a unit test.
- [ ] `--ai-help` and the extension's documentation say which trust options `--dns-over-tls` honours.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

- Done and committed (ADR-1353): `--dns-over-tls` gets the transfer's `-k`, `--cacert`, `--capath` through `ExtensionOptionValues.TlsTrust`; option help and CLAUDE.md say so. Build clean; Measure-CodeQuality for Abstractions and EncryptedDns: 0 failing members, its 69 test projects passed.
- Touches widened: the hook had no way to see the transfer's options; no other task in Doing named these files.
- Left: criteria 1 and 2. `DnsOverTlsTrustTests` pins only the mapping to `TlsClientOptions`. Add unit tests that resolve through a test-double DoT server over an in-memory duplex `IConnection` (server side `SslStream`, certificate from a private CA): resolves with `--cacert`, resolves with `-k` and an untrusted certificate, fails without either. `DnsOverTlsResolver` takes the connector, so inject a fake one and `new SslStreamTlsProvider(hook.TlsOptions, TimeProvider.System)`. Check whether Linux/macOS chain building answers the same.
- `--dns-over-quic` does not read `TlsTrust`; outside this task.
- Criterion 3 is partly done: the option help and CLAUDE.md say it, but `curl --ai-help` printed no `dns-over-tls` trust text; `--extended-help` shows only `EncryptedDnsExtension.Description`. Check where `--ai-help` should carry it.

## Log

- 2026-11-24: Created from Surly bug report 2026-11-24T1330, finding 1.
- 2026-11-24: Backlog -> Doing. Criteria at claim: d597ab5e 5829f2b6 3f81e2b0 3d836d5e.
- 2026-11-24: Doing -> Backlog. Code committed; left: end-to-end DoT handshake unit tests (criteria 1-2) and --ai-help trust text (criterion 3), see Notes
- 2026-11-24: Backlog -> Doing. Criteria at claim: d597ab5e 5829f2b6 3f81e2b0 3d836d5e.
