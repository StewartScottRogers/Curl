---
id: BL-2859
title: Add stand-ins for the https-mtls and socks5unix servers so upstream cases 2088, 2089, 1467, 1468 and 1470 run
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitTests/PassingUpstreamCases.txt, Curl.Conformance.UnitLibrary/UpstreamCaseScreening.cs, Curl.Conformance.UnitLibrary/UpstreamCaseRunner.cs, Curl.Conformance.UnitTests/UpstreamConformanceTests.MtlsSocksUnixCases.cs, Curl.Conformance.UnitTests/UpstreamCaseScreeningTests.cs, Curl.Conformance.UnitLibrary/UpstreamTestVariableSubstitution.cs, Curl.Conformance.UnitLibrary/HttpsServerConnector.cs, Curl.Conformance.UnitLibrary/SocksServerConnector.cs, Curl.Conformance.UnitLibrary/UpstreamStandInFronts.cs]
requirement: none
created: 2026-10-19
completed:
---
# BL-2859 — Add stand-ins for the https-mtls and socks5unix servers so upstream cases 2088, 2089, 1467, 1468 and 1470 run

## Goal

The case runner serves upstream's `https-mtls` and `socks5unix` servers in memory, so upstream cases 2088, 2089, 1467, 1468 and 1470 run, and each is on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` or has a recorded reason.

## Context

Gap scorecard 2026-10-19_1329: 2088 and 2089 are `excluded | reference-lacks:local-http`, 1467, 1468 and 1470 `excluded | reference-lacks:proxy`. The Linux evidence is `the harness has no value for %HTTPS-MTLSPORT` and `... %SOCKSUNIXPATH`; on Windows they are skipped by features (`!Schannel` for 2088 and 2089, `!win32` for 1467, 1468 and 1470), so only Linux and macOS can run them (ADR-0488: `Inconclusive` on Windows once listed, judged by `CI`).

- 2088 and 2089 ("HTTPS GET with client authentication (mtls)", features `SSL,!Schannel,local-http`, server `https-mtls`): upstream's `runtests.pl` starts a TLS server on `%HTTPS-MTLSPORT` that demands a client certificate (`--cert test-client-eku-only.crt`, and a second case for the same server). The harness has `HttpsServerConnector` and the TLS server stand-ins (`TlsServerConnection`, `Tls13ServerStream`, ADR-0522); add the client-certificate request and verification against `test-ca.crt` from `UpstreamTestData/certs` (generated per BL-1923) and the variable `HTTPS-MTLSPORT` (see `UpstreamTestVariableSubstitution.cs`).
- 1467, 1468 and 1470 ("HTTP GET via SOCKS5 proxy via Unix sockets", features `proxy,UnixSockets,!win32`, server `socks5unix`): the SOCKS5 stand-in `SocksServerConnector` listens on `%SOCKSPORT`; add the Unix-domain-socket variant bound to `%SOCKSUNIXPATH` (as `%HTTPUNIXPATH` is for `http-unix`) and the variable. `--socks5 localhost%SOCKSUNIXPATH` must reach it.

Read the cases at tag `curl-8_22_0` (https://github.com/curl/curl/tree/curl-8_22_0/tests/data/test2088, `test1467`) for the exact commands and expected bytes. Add the folders of any library you must change to `touches` in the same commit. Lanes run Windows only; the Linux pass is judged by `CI`.

## Acceptance criteria

- [ ] `UpstreamTestVariableSubstitution` gives `HTTPS-MTLSPORT` and `SOCKSUNIXPATH` values and `UpstreamCaseScreening` lists the two servers as emulated; unit tests pin both (an unknown server still skips with `the harness does not emulate the <server> server`).
- [ ] The mTLS stand-in demands, verifies and records the client certificate; a unit test shows a connection without a certificate fails the handshake and one with `test-client-eku-only.crt` succeeds.
- [ ] The Unix-socket SOCKS5 stand-in has a unit test with a SOCKS5 client over a Unix socket path.
- [ ] Cases 2088, 2089, 1467, 1468 and 1470 are on `PassingUpstreamCases.txt`, `Inconclusive` on Windows with the feature reason, and `Passed` in `CI` on Linux and macOS; a case that fails there is taken off the list again with its first difference in Notes.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and each changed library keeps 100% line and branch coverage and complexity at most 10 (`Curl.Conformance.UnitLibrary` is held to the same gates).

## Notes

## Log

- 2026-10-19: Created.
