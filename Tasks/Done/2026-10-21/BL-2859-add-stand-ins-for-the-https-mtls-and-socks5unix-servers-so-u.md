---
id: BL-2859
title: Add stand-ins for the https-mtls and socks5unix servers so upstream cases 2088, 2089, 1467, 1468 and 1470 run
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2955]
touches: [Curl.Conformance.UnitTests/PassingUpstreamCases.txt, Curl.Conformance.UnitLibrary/UpstreamCaseScreening.cs, Curl.Conformance.UnitLibrary/UpstreamCaseRunner.cs, Curl.Conformance.UnitTests/UpstreamConformanceTests.MtlsSocksUnixCases.cs, Curl.Conformance.UnitTests/UpstreamCaseScreeningTests.cs, Curl.Conformance.UnitLibrary/UpstreamTestVariableSubstitution.cs, Curl.Conformance.UnitLibrary/HttpsServerConnector.cs, Curl.Conformance.UnitLibrary/SocksServerConnector.cs, Curl.Conformance.UnitLibrary/UpstreamStandInFronts.cs, Curl.Conformance.UnitLibrary/TlsServerOptions.cs, Curl.Conformance.UnitLibrary/TlsServerStream.cs, Curl.Conformance.UnitLibrary/TlsClientCertificateVerifier.cs, Curl.Conformance.UnitLibrary/UpstreamHttpsMtlsFront.cs, Curl.Conformance.UnitLibrary/CLAUDE.md, Curl.Conformance.UnitTests/HttpsServerConnectorTests.cs, Curl.Conformance.UnitTests/HttpsServerConnectorTests.Mtls.cs, Curl.Conformance.UnitTests/MtlsTestCertificates.cs, Curl.Conformance.UnitTests/UpstreamHttpsMtlsFrontTests.cs, Curl.Conformance.UnitTests/SocksServerConnectorTests.cs, Curl.Conformance.UnitTests/UpstreamCaseRunnerTests.cs, Curl.Conformance.UnitTests/UpstreamStandInFrontsTests.cs]
requirement: none
created: 2026-10-19
completed: 2026-10-21
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

- [x] `UpstreamTestVariableSubstitution` gives `HTTPS-MTLSPORT` and `SOCKSUNIXPATH` values and `UpstreamCaseScreening` lists the two servers as emulated; unit tests pin both (an unknown server still skips with `the harness does not emulate the <server> server`).
- [x] The mTLS stand-in demands, verifies and records the client certificate; a unit test shows a connection without a certificate fails the handshake and one with `test-client-eku-only.crt` succeeds.
- [x] The Unix-socket SOCKS5 stand-in has a unit test with a SOCKS5 client over a Unix socket path.
- [x] Cases 2088, 2089, 1467, 1468 and 1470 are on `PassingUpstreamCases.txt`, `Inconclusive` on Windows with the feature reason, and `Passed` in `CI` on Linux and macOS; a case that fails there is taken off the list again with its first difference in Notes.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and each changed library keeps 100% line and branch coverage and complexity at most 10 (`Curl.Conformance.UnitLibrary` is held to the same gates).

## Notes

- 2026-10-21 (lane 5, prerequisite pass before any edit): the mTLS half is ready - Curl sends client certificates (`ClientCertificateLoader`, `HandBuiltTlsProvider`) and `UpstreamTestCertificateGenerator` already generates `test-client-eku-only` and `test-client-cert` from their `.prm` files. The socks5unix half is not: Curl has no SOCKS-proxy-over-Unix-socket path (`TransferProxySelection`, `TcpConnector` and `SocksProxyTunnel` dial host and port only), so 1467, 1468 and 1470 cannot pass whatever the stand-in does. Filed BL-2955 for that engine work and added it to `depends-on`; nothing else is missing. Nothing was edited.
- 2026-10-21 (lane 5, delivery): `%SOCKSUNIXPATH` is `/log/server/socks5.sock` (`SocksServerConnector.SocksUnixPath`). Choice: absolute, as `runtests.pl` makes it, so `localhost%SOCKSUNIXPATH` reads as a host and a non-empty path. `SocksServerConnector` answers a dial whose host is that path, so the conformance dialer's `HttpUnixSocketTarget` (and the gap office's tool, which uses it too) reach socksd with no change to `InMemoryServerTcpDialer`. The runner supplies the value; `UpstreamTestVariableSubstitution` already knew both names, so it needed no change.
- `%HTTPS-MTLSPORT` is 9021 (`HttpsServerConnector.HttpsMtlsPort`; choice: the next port no stand-in uses), with a value only with a certificate directory, through the new `UpstreamHttpsMtlsFront`, registered after `UpstreamHttpsFront`, because the https front does not pass connections on to the connector it wraps. The new `TlsClientCertificateVerifier` (`TlsServerOptions.ClientCertificateVerifier`) records every certificate presented and accepts only one that chains to `test-ca.crt` (custom root trust, no revocation check), as stunnel does at `verify = 2`.
- Widened `touches` to the TLS server files, the new files, the library's `CLAUDE.md` and the test files that pin them. No task in Doing on `origin/work/dark-factory` names any of them. `UpstreamConformanceTests.MtlsSocksUnixCases.cs` was not needed: the ratchet runs the listed cases.
- Tests: `HttpsServerConnectorTests.Mtls.cs` (`test-client-eku-only` succeeds and is recorded; no certificate fails the handshake; a certificate the test CA did not sign fails and is recorded), `UpstreamHttpsMtlsFrontTests`, `SocksServerConnectorTests.Socks5_OverTheSocksUnixPath_ConnectsAndRelays`, `UpstreamCaseRunnerTests.RunAsync_SocksUnixCase_RunsCurlWithTheSocksUnixPath` (1467, 1468) and `RunAsync_HttpsMtlsCaseWithoutACertificateDirectory_SkipsForTheMtlsPort`, screening rows for both servers, and `UpstreamStandInFrontsTests` with `HTTPS-MTLSPORT`. `RunAsync_CaseTheHarnessCannotRun_IsSkippedWithTheReason` had used `%SOCKSUNIXPATH` as its unknown variable; it now uses `%HTTPTLS6PORT`.
- Measured on Windows with the conformance tests temporarily set to the Unix platform (then reverted): 1467, 1468 and 1470 pass. 2088 and 2089 fail there with `curl: (58) schannel: Failed to import cert file` because Windows Curl acts as the Schannel build, which these `!Schannel` cases exclude. So 2088 and 2089 were never measured on a lane. They are listed as the criterion says, and `CI` on Linux and macOS judges them. If either fails there, take it off with its first difference.
- Verified: `dotnet build Curl.slnx -warnaserror` clean. `Measure-CodeQuality.ps1 -Library Curl.Conformance.UnitLibrary` ran every test project that reaches it (Conformance 3898 passed, 0 failed; Http2Server 32, Http3Server 63, SshServer 139): 100% line and branch, 1376 members, 0 failing, worst CRAP 10. `dotnet format whitespace --verify-no-changes` is clean on both projects.

## Log

- 2026-10-19: Created.
- 2026-10-21: Backlog -> Doing. Criteria at claim: 27c464c3 fb93b735 8ce8c460 c768a82b 5ff94941.
- 2026-10-21: Doing -> Backlog. Waits on BL-2955: Curl cannot reach a SOCKS proxy over a Unix socket, which cases 1467, 1468 and 1470 need
- 2026-10-21: Backlog -> Doing. Criteria at claim: 27c464c3 fb93b735 8ce8c460 c768a82b 5ff94941.
- 2026-10-21: Doing -> Done. the https-mtls and socks5unix stand-ins serve upstream cases 2088, 2089, 1467, 1468 and 1470, all five on the ratchet
