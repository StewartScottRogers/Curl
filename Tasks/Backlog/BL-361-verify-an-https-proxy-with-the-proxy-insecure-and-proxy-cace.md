---
id: BL-361
title: Verify an HTTPS proxy with the --proxy-insecure and --proxy-cacert family
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-266]
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests, Curl.Console, Curl.Console.UnitTests, Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-09-27
completed:
---
# BL-361 — Verify an HTTPS proxy with the --proxy-insecure and --proxy-cacert family

## Goal

`curl --proxy-insecure -x https://<proxy> <url>` skips verification of the proxy's certificate and `--proxy-cacert <file>` trusts only that file for it, while `-k` and `--cacert` keep applying only to the target, as curl 8.21.0 does.

## Context

- Filed by BL-266 (2026-09-27), which made `TcpConnector` run the handshake to a `ProxyKind.Https` proxy through the same injected `ITlsProvider` as the target's (ADR-0060). Give `TcpConnector` a separate proxy `ITlsProvider` (e.g. an optional `proxyTlsProvider` constructor parameter defaulting to `tlsProvider`) in `Curl.Networking.UnitLibrary`; note `CurlCompositionTests.CapturedDependency<ITlsProvider>` in `Curl.Console.UnitTests` expects exactly one `ITlsProvider` field on `TcpConnector`, so adjust that test in the same change.
- Measured in BL-266's Notes: `curl -s -S -k -x https://localhost:18404 https://example.com/` against a self-signed proxy fails with exit 60 `schannel: SEC_E_UNTRUSTED_ROOT ...`, so `-k` does not reach the proxy; `--proxy-insecure` makes the same run reach CONNECT.
- Parse `--proxy-insecure`, `--proxy-cacert`, `--proxy-capath` (and whichever of the `--proxy-*` TLS family the reference build accepts) in `Curl.Cli.UnitLibrary`; in `Curl.Console`'s composition build a second `SslStreamTlsProvider` from a `TlsClientOptions` made of those options and pass it as `proxyTlsProvider`.
- Upstream: https://curl.se/docs/manpage.html (`--proxy-insecure`, `--proxy-cacert`, `--proxy-capath`). Measure with `/mingw64/bin/curl` (curl 8.21.0, ADR-0009) against a TLS loopback proxy and record the commands in Notes before pinning anything.

## Acceptance criteria

- [ ] `--proxy-insecure` and `--proxy-cacert <file>` parse, and a named test in `Curl.Console.UnitTests` shows the composition builds `TcpConnector` with a proxy TLS provider made from them, not from `-k`/`--cacert`.
- [ ] Without any `--proxy-*` TLS option, the proxy handshake verifies against the system store even when `-k` is given (measured exit 60 above), pinned by a named test.
- [ ] `dotnet build -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for every member this task adds or changes.

## Notes

## Log

- 2026-09-27: Created.
