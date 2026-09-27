---
id: BL-266
title: Tunnel through an HTTPS proxy in the connector
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-212]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-26
completed: 2026-09-27
---
# BL-266 — Tunnel through an HTTPS proxy in the connector

## Goal

`TcpConnector` in `Curl.Networking.UnitLibrary` tunnels through a `ProxyKind.Https` proxy the way curl 8.21.0 does - TLS to the proxy host first, then the `CONNECT` exchange BL-212 built over that TLS stream, then (when `ConnectTarget.UseTls` is true) TLS to the target over the tunnel - instead of throwing `NotSupportedException`.

## Context

- Follow-up from BL-212, which made `TcpConnector` tunnel through `ProxyKind.Http` and `ProxyKind.Http10` proxies with `CONNECT` (`Curl.Networking.UnitLibrary/HttpProxyTunnel.cs`, `HttpProxyTunnelOptions.cs`, `TcpConnector.cs`). Today `TcpConnector` throws `NotSupportedException("Tunnelling through a Https proxy is not implemented yet.")` for `ProxyKind.Https`; this task removes that case. SOCKS kinds are BL-213 and stay out of scope.
- Proxy model: `ConnectTarget.Proxy` (`ProxyEndpoint(Kind, Host, Port, Credential)`) in `Curl.Protocol.Abstractions.UnitLibrary`, ADR-0014. TLS comes from the injected TLS provider (`SslStreamTlsProvider` in production); the handshake to the proxy is keyed on the proxy host name, the second handshake on the target host. Never construct a `Socket` or `SslStream` outside the existing dialer/TLS provider seams, so the tests stay off the network.
- Upstream: https://curl.se/docs/manpage.html (`-x`/`--proxy`, "HTTPS proxy"; the `--proxy-cacert`, `--proxy-insecure` family exists but is not parsed yet, so this task uses the transfer's TLS settings for the proxy handshake unless measurement shows otherwise, and records that in Notes) and https://curl.se/libcurl/c/libcurl-errors.html. Checked against curl 8.21.0.
- Measuring: run curl 8.21.0 - the mingw build `/mingw64/bin/curl`, the Windows reference (ADR-0009) - against a TLS loopback proxy (or with `Record-CurlExchange.ps1` at the repository root), record the exact command and the bytes and messages it produced in `Notes`, then pin them in a test. Never pin text that was not measured.

## Acceptance criteria

- [x] `TcpConnector.ConnectAsync` with `ProxyKind.Https` performs TLS to the proxy, sends the same `CONNECT` request bytes BL-212's `HttpProxyTunnel` sends for `ProxyKind.Http` (as measured from curl 8.21.0 through an HTTPS proxy), and, when `UseTls` is true, performs a second TLS handshake to the target host over the tunnel; a named test in `Curl.Networking.UnitTests` asserts each step with fakes, and no test needs `TestCategory=Integration`.
- [x] A failed TLS handshake to the proxy, a refused `CONNECT` and a failed TLS handshake to the target each return a `ConnectResult.Failed` with the `CurlExitCode` and message measured on curl 8.21.0 (the commands and output recorded in `Notes`), each pinned by a named test.
- [x] No `ProxyKind.Https` path throws `NotSupportedException`; the XML doc on `TcpConnector` no longer lists `Https` as unsupported.
- [x] `dotnet build Curl.Networking.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary` reports 100% line and branch coverage and no failing member for every member this task adds or changes.

## Notes

- 2026-09-27, plan: in `TcpConnector.ConnectThroughProxyAsync`, `ProxyKind.Https` resolves and dials the proxy like the other kinds, then `OpenTunnelOverTlsAsync` runs `ITlsProvider.AuthenticateAsClientAsync(dialed, proxy.Host)` and hands the secured connection (with the socket's local end point) to BL-212's `OpenTunnelAsync`, which sends CONNECT, reads the reply and applies target TLS inside the tunnel. Every handshake failure is the provider's result as it is. New fake `Fakes/SequencedTlsProvider` answers each handshake in turn; tests in `TcpConnectorTests.HttpsProxy.cs`. The `NotSupportedException` test in `TcpConnectorTests.Proxy.cs` is gone.
- Measured with curl 8.21.0 (`/mingw64/bin/curl`, Schannel, ADR-0009) against a throwaway loopback proxy (a small .NET program in `C:\Templ266proxy`, self-signed `CN=localhost` cert, outside the repository):
  - `curl -s -S -x https://127.0.0.1:18401 http://example.com/`, proxy closes at once -> `curl: (35) Recv failure: Connection was aborted`.
  - `curl -s -S -x https://localhost:18402 http://example.com/`, self-signed proxy -> `curl: (60) schannel: SEC_E_UNTRUSTED_ROOT (0x80090325) - The certificate chain was issued by an authority that is not trusted.` plus the usual `More details here` block. Same with `-k ... https://example.com/` (port 18404): `-k` does not reach the proxy.
  - `curl -s -S --proxy-insecure -x https://localhost:18405 https://example.com/`, proxy answers 407 -> sent inside the proxy's TLS `CONNECT example.com:443 HTTP/1.1
Host: example.com:443
User-Agent: curl/8.21.0
Proxy-Connection: Keep-Alive

` (identical to BL-212's HTTP-proxy bytes) and printed `curl: (7) CONNECT tunnel failed, response 407`. `-p ... http://example.com:8080/` and `-U u:p ... https://[::1]:8443/` sent the matching bytes (`CONNECT [::1]:8443`, `Proxy-Authorization: Basic dTpw`).
  - `... -x https://localhost:18408 https://example.com/`, proxy answers 200 then plaintext -> `curl: (35) schannel: next InitializeSecurityContext failed: SEC_E_INVALID_TOKEN (0x80090308) - The token supplied to the function is invalid`; proxy answers 200 then closes (18407) -> `curl: (35) Recv failure: Connection was reset`.
  - `curl -s -S -o /dev/null --proxy-insecure -p -x https://localhost:18411 http://example.com/ -w "connect=%{time_connect} appconnect=%{time_appconnect} ... code=%{http_connect}"` -> `appconnect=0.000000 code=200`; the plaintext GET went through the tunnel.
- Decision (ADR-0060, decided by Claude under Stewart's delegation): the proxy's TLS failures keep the provider's exit code and message (measured identical to a target's), timings and peer certificates are the target handshake's only, and the proxy handshake uses the transfer's `ITlsProvider`. That last part differs from curl when `-k`/`--cacert`/`--capath` is given; the `--proxy-*` TLS options are not parsed yet, so filed BL-361 to add a separate proxy TLS provider together with its wiring. A separate `proxyTlsProvider` constructor parameter was tried here and dropped: `CurlCompositionTests.CapturedDependency<ITlsProvider>` in `Curl.Console.UnitTests` (outside this task's touches, and in another lane's) requires exactly one `ITlsProvider` field on `TcpConnector`.
- Added `Documentation/Planning/Decisions` to `touches` for ADR-0060 and its README row; no task in Doing names it.
- Gates: `dotnet build` clean (0 warnings); fast tests all green (Curl.Networking.UnitTests 559 passed, 6 skipped; 6 new HTTPS-proxy test cases). `Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary`: every `TcpConnector` member at 100% line and branch; the 4 failing members (`TcpDialer.DialAsync`, `UdpDatagramChannel.SendAsync`/`ReceiveAsync`, `SslStreamTlsProvider.CreateCipherSuitesPolicy`) predate this task and are untouched (loopback-only / platform-only code).
- Follow-up filed: BL-361 (`--proxy-insecure`/`--proxy-cacert` for the HTTPS proxy). BL-328 already covers removing the console's exit 4 for HTTPS proxies.

## Log

- 2026-09-26: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. TcpConnector tunnels through an HTTPS proxy: TLS to the proxy, CONNECT inside it, then TLS to the target
