---
id: BL-3168
title: Report an unresolvable proxy name as exit 5 Could not resolve proxy for every proxy form
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: []
touches: [Curl.Networking.UnitLibrary/TcpConnector.cs, Curl.Networking.UnitLibrary/SocksProxyTunnel.cs, Curl.Networking.UnitLibrary/NameResolutionFailure.cs, Curl.Networking.UnitTests/ProxyNameResolutionFailureTests.cs, Curl.Networking.UnitTests/NameResolutionFailureTests.cs, Curl.Console.UnitTests/CurlCommandRunnerProxyResolveTests.cs]
requirement: none
created: 2026-10-22
completed: 2026-10-22
---
# BL-3168 — Report an unresolvable proxy name as exit 5 Could not resolve proxy for every proxy form

## Goal

When the name that cannot be resolved is the proxy's, Curl exits 5 with `curl: (5) Could not resolve proxy: <name>`, for every proxy form.

## Context

Stewart reported on 2026-10-22; measured the same day with the preview build and System32 curl 8.21.0 Schannel: `curl -sS --proxy1.0 badproxy.invalid http://x/` and `curl -sS -x badproxy.invalid http://x/` print `curl: (5) Could not resolve proxy: badproxy.invalid` and exit 5 in real curl; Curl prints `curl: (6) Could not resolve host: badproxy.invalid` and exits 6. Check each form against curl 8.22.0 with `Record-CurlExchange.ps1`: `-x`/`--proxy`, `--proxy1.0`, `--socks4`/`--socks4a`/`--socks5`/`--socks5-hostname` (curl resolves a SOCKS proxy's own name as the proxy, but the target's name as a host when `--socks5-hostname` leaves it to the proxy), `--preproxy`, `https://` proxies, and `--noproxy` cases that skip the proxy. The mapping lives in `Curl.Networking.UnitLibrary`: `NameResolutionFailure.Describe(notResolved, subject, host, port, failure)` is called with `CouldntResolveProxy`/`"proxy"` at `TcpConnector.cs` near line 1344 (first hop) and in `TcpConnector.UdpTunnel.cs`, but `TcpConnector.cs` near lines 689 and 1103 and `SocksProxyTunnel.cs` near line 149 pass `CouldntResolveHost`. Find which path a `-x` HTTP request takes and why it labels the proxy's name as the host's. `Curl.Core.UnitLibrary/TransferRetrier.cs` already treats exit 5 as retryable like 6; do not change it. `TcpConnector.cs` is also edited by the refused-connection task; the board serialises them by `touches`.

## Acceptance criteria

- [x] `Curl.Networking.UnitTests/ProxyNameResolutionFailureTests.cs` has one test per form (`-x` HTTP proxy, `--proxy1.0`, `--socks5`, `--preproxy`) with a failing fake resolver for the proxy's name: each returns `CurlExitCode.CouldntResolveProxy` (5) with message `Could not resolve proxy: <name>`.
- [x] A test for the target host failing to resolve behind a working proxy keeps exit 6 / `Could not resolve host:` (or whatever curl 8.22.0 measures for that case, noted in the test).
- [x] `Curl.Networking.UnitTests/NameResolutionFailureTests.cs` (extend) has unit tests on `Describe` for the `"proxy"` subject, including the DNS-failure suffix.
- [x] The exact line and exit code for `-x badproxy.invalid` are checked through the command runner in `Curl.Console.UnitTests/CurlCommandRunnerProxyResolveTests.cs`: stderr `curl: (5) Could not resolve proxy: badproxy.invalid`, exit 5.
- [x] `dotnet build Curl.Networking.UnitLibrary -warnaserror` is clean and the fast tests of `Curl.Networking.UnitTests` and `Curl.Console.UnitTests` pass; no test needs a real resolver.

## Notes

- Measured curl 8.22.0 (Schannel, System32) with Record-CurlExchange.ps1 -NoServer on 2026-10-22: `-x badproxy.invalid`, `--proxy1.0`, `--socks5`, `--socks5-hostname`, `--socks4a`, `-x https://badproxy.invalid` and `--preproxy socks5://badproxy.invalid -x http://127.0.0.1:1` (all with `http://x/`) each exit 5 `curl: (5) Could not resolve proxy: badproxy.invalid`; `-x http://127.0.0.1:1 --noproxy x.invalid http://x.invalid/` exits 6 `Could not resolve host: x.invalid`; `--socks5 127.0.0.1:1 http://x.invalid/` exits 7 (refused proxy). `-v -x badproxy.invalid` writes `Could not resolve proxy: badproxy.invalid` twice, then `Could not resolve: badproxy.invalid:80`.
- Cause: a plain `-x`/`--proxy1.0`/`https://` forward-proxy request connects to the proxy as the `ConnectTarget` itself (`IsForwardProxy`), so `TcpConnector.HostNotResolved` labelled its name `host` with exit 6. SOCKS proxies and pre-proxies already went through `ConnectThroughProxyTracedAsync` (exit 5). Fix: `HostNotResolved` takes the target and uses `CouldntResolveProxy`/`"proxy"` for a forward-proxy target, keeping the `-v` look-up lines (now naming the proxy) as curl writes them. `SocksProxyTunnel.CouldNotResolve` (the target resolved locally behind a working SOCKS4/5 proxy) stays exit 6 host, as measured; the QUIC path at line 689 never has a forward-proxy target. No ADR: a measured bug fix, no design choice.
- Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary: 0 failing members. CI on Linux and macOS: tests use no path, OS error text or certificate, so they are platform-neutral; fast tests of Curl.Networking.UnitTests (3384 passed) and Curl.Console.UnitTests (2827 passed) green on Windows.

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: f0f8ddee dc920ea8 8ef809a1 311307cd 6ca1d453.
- 2026-10-22: Doing -> Done. Forward, SOCKS and pre-proxy names that do not resolve now exit 5 Could not resolve proxy, measured on curl 8.22.0
