---
id: BL-267
title: Pass -A and the platform credential encoding to the CONNECT tunnel in Curl.Console
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-212, BL-192]
touches: [Curl.Console, Curl.Console.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-26
completed: 2026-09-27
---
# BL-267 — Pass -A and the platform credential encoding to the CONNECT tunnel in Curl.Console

## Goal

`CurlComposition` in `Curl.Console` builds `TcpConnector` with an `HttpProxyTunnelOptions` that carries the transfer's `-A` value and `CredentialEncoding.ForPlatform(OperatingSystem.IsWindows())`, and every connect a transfer makes carries the `ProxyEndpoint` that `-x`/`-U`/`-p` describe in `ConnectTarget.Proxy`.

## Context

- Follow-up from BL-212. `TcpConnector` (`Curl.Networking.UnitLibrary/TcpConnector.cs`) takes an optional `HttpProxyTunnelOptions(string? UserAgent, Encoding CredentialEncoding)` constructor argument; when omitted it uses `HttpProxyTunnelOptions.Default` (`"curl/8.21.0"`, UTF-8). `CurlComposition.CreateTransports` (`Curl.Console/CurlComposition.cs`) currently calls `new TcpConnector(dnsResolver, tcpDialer, tlsProvider, timeProvider)` and so always sends the default `User-Agent` and UTF-8 `Proxy-Authorization` bytes.
- `UserAgent` rule: the `-A` value when given (`CommandLineOptions.UserAgent` in `Curl.Cli.UnitLibrary`); `null` when `-A ""` removes the header, so no `User-Agent` line is sent in the `CONNECT` request; `"curl/8.21.0"` when `-A` is absent.
- `CredentialEncoding` rule: `CredentialEncoding.ForPlatform` in `Curl.Authentication.UnitLibrary` (already referenced by `Curl.Console`), per ADR-0022 (`Documentation/Planning/Decisions/ADR-0022-basic-and-bearer-credentials-are-sent-as-the-platform-curl-sends-them.md`): the system ANSI code page on Windows, UTF-8 elsewhere.
- Proxy: BL-192 parses `-x`/`--proxy`, `-U`/`--proxy-user` and `-p`/`--proxytunnel` into `CommandLineOptions`. `ConnectTarget` is built inside the protocol handlers (e.g. `DictProtocolHandler`, `TelnetProtocolHandler`, `MqttProtocolHandler`), not in `Curl.Console`, so the proxy is applied in `Curl.Console` by a small `IConnector` decorator that `CurlComposition` puts in front of `TcpConnector` and that returns `target with { Proxy = … }` before delegating. The `ProxyEndpoint` is `ProxyEndpoint(Kind, Host, Port, Credential)` from `Curl.Protocol.Abstractions.UnitLibrary` (ADR-0014): kind from the `-x` scheme, credential from `-U`. Which targets get the tunnel with and without `-p` is measured on curl 8.21.0 (https://curl.se/docs/manpage.html, `-p`/`--proxytunnel`), recorded in `Notes`, and pinned; plain-HTTP forward proxying (no `CONNECT`) is not this task.
- `CommandLineOptions` and `CurlTransports` are one per run; if `-A`/`-x` can differ per URL after `--next`, build the options per transfer rather than once per run, and say which in `Notes`.
- Existing test doubles to reuse: `Curl.Console.UnitTests/RecordingConnector.cs`, `ScriptedConnector.cs`; composition tests live in `CurlCompositionTests.cs` and `CurlTransportsTests.cs`.

## Acceptance criteria

- [x] `CurlTransports` exposes the `HttpProxyTunnelOptions` it built `TcpConnector` with, and tests in `Curl.Console.UnitTests/CurlTransportsTests.cs` named `CreateTransports_WithUserAgent_TunnelOptionsCarryIt`, `CreateTransports_WithEmptyUserAgent_TunnelOptionsUserAgentIsNull` and `CreateTransports_WithoutUserAgent_TunnelOptionsUserAgentIsCurl8210` pass.
- [x] A test named `CreateTransports_TunnelOptionsCredentialEncoding_IsForPlatform` asserts the encoding equals `CredentialEncoding.ForPlatform(OperatingSystem.IsWindows())`.
- [x] Tests for the proxy decorator show that with `-x http://proxy:3128 -U user:pass` a connect reaches the inner connector with `ConnectTarget.Proxy` equal to `ProxyEndpoint(ProxyKind.Http, "proxy", 3128, <credential user:pass>)`, that with no `-x` `Proxy` stays `null`, and that `-p` applies the tunnel to the targets measured on curl 8.21.0 (command and result in `Notes`).
- [x] `dotnet build Curl.Console -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `powershell -NoProfile -File Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Console`.

## Notes

- Touches widened to `Documentation/Planning/Decisions` for ADR-0059 and its README row; no task in `Doing` names that folder (BL-134: Abstractions and File tests; BL-213: Networking).
- Decision (ADR-0059, decided by Claude under Stewart's delegation): no `IConnector` proxy decorator. ADR-0056 already carries the selected proxy per transfer on `ITransferContext.Proxy` into every handler's `ConnectTarget`, after `--noproxy`, the environment variables and the `-U` override; a run-wide decorator would bypass all three. The third acceptance criterion is met by tests through the production composition in `CurlCompositionProxyTests`: `RunAsync_HttpProxyAndProxyUserForADictUrl_ConnectTargetCarriesTheProxy`, `RunAsync_NoProxyOption_ConnectTargetCarriesNoProxy`, `RunAsync_ProxyOptionForATunnelledUrl_ConnectTargetCarriesTheProxy`, `RunAsync_ProxyOptionWithoutProxyTunnelForAnHttpUrl_ConnectsToTheProxyWithoutATunnel`.
- `-p` targets: taken from the curl 8.21.0 measurements already recorded in ADR-0056 and BL-238 (Windows mingw Schannel build, loopback proxy): `curl -x http://p dict://example.com/d:x` and `curl -p -x http://p dict://example.com/d:x` both send `CONNECT example.com:2628` (tunnel with or without `-p`); `https` always tunnels; plain `http` is forwarded to the proxy without `-p` and tunnelled with `-p` (`CONNECT example.com:80`). Not re-measured in this run; the pinned tests match those recordings.
- Options are built once per run: `CommandLineParser` does not implement `--next`, so `-A` and `-x` cannot differ per URL. When `--next` lands, build `HttpProxyTunnelOptions` per URL group.
- New: `CurlComposition.CreateProxyTunnelOptions`, `CurlTransports.ProxyTunnelOptions`; end-to-end tests `RunAsync_ProxyTunnelWithUserAgentOption_ConnectRequestCarriesIt` and `RunAsync_ProxyTunnelWithEmptyUserAgentOption_ConnectRequestHasNoUserAgent` pin the CONNECT bytes.
- Gates: `dotnet build -warnaserror` 0 warnings; fast tests all green (Curl.Console.UnitTests 583 passed); `Measure-CodeQuality.ps1 -Library Curl.Console`: 100% line, 100% branch, 0 failing members, worst CRAP 10.

## Log

- 2026-09-26: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. The CONNECT tunnel sends the -A value (none for -A "") and encodes proxy credentials in the platform encoding; the proxy reaches ConnectTarget.Proxy per transfer
