---
id: BL-447
title: Encode CONNECT tunnel --proxy-header and -A text in the platform curl's encoding
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-373]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-27
completed: 2026-09-27
---
# BL-447 — Encode CONNECT tunnel --proxy-header and -A text in the platform curl's encoding

## Goal

The CONNECT request sent to an HTTP proxy (`-p`, or an `https://` URL through `-x`) carries `--proxy-header` and `-A` text in the platform curl's encoding, as ADR-0067 decides and BL-373 did for the HTTP formatter.

## Context

- Decision: `Documentation/Planning/Decisions/ADR-0067-command-line-header-text-becomes-bytes-in-the-platform-curls-encoding-inside-the-http-formatter.md`.
- BL-373 added `HttpRequestOptions.CommandLineTextEncoding` and encodes `-H`, `--proxy-header`, `-A` and `-e` in `HttpRequestHeadFormatter`.
- The CONNECT request is built separately, in `Curl.Networking.UnitLibrary/HttpProxyTunnel.cs`, which ends with `Encoding.Latin1.GetBytes(request.ToString())`; `HttpProxyTunnelOptions` already carries `CredentialEncoding`, which `Curl.Console/CurlComposition.cs` (`CreateProxyTunnelOptions`) sets to `CredentialEncoding.ForPlatform(OperatingSystem.IsWindows())`.
- Encode each `--proxy-header` value and the `User-Agent` in that same encoding before parsing, one character per byte (as `HttpRequestHeadFormatter.HeadText` does), so the closing Latin-1 step puts those bytes on the wire.
- Measured Windows bytes (curl 8.21.0 mingw, Windows-1252): `é` -> `0xE9`, U+0100 -> `A`, `€` -> `0x80`, `中` -> `?`.

## Acceptance criteria

- [x] An `HttpProxyTunnel` test with the Windows-1252 encoding sends `--proxy-header "X-A: €"` in the CONNECT request as the byte `80`.
- [x] An `HttpProxyTunnel` test with UTF-8 sends `--proxy-header "X-A: é"` and `-A é` as `C3 A9`.
- [x] Existing `TcpConnectorTests.Proxy` tests pass unchanged.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` is green.

## Notes

- Filed by BL-373: the HTTP formatter's encoding does not reach the CONNECT request, which `Curl.Networking` builds and which was outside BL-373's `touches`.
- Delivered: `HttpProxyTunnelOptions.CommandLineTextEncoding` (init, default Latin-1) and `HttpProxyTunnel.RequestText`, which encodes each `--proxy-header` value before parsing and the `User-Agent`, one character per byte, as `HttpRequestHeadFormatter.HeadText` does. `CurlComposition.CreateProxyTunnelOptions` sets it to `CredentialEncoding.ForPlatform(OperatingSystem.IsWindows())`, the same encoding `TransferContextFactory` gives the HTTP formatter.
- Choice: a separate `CommandLineTextEncoding` property rather than reusing `CredentialEncoding`, so each name says what it encodes, matching the name `HttpRequestOptions` uses. Default Latin-1 keeps the bytes unchanged for any caller that does not set it (the old closing Latin-1 step), as `HttpRequestOptions` defaults. The behaviour itself is ADR-0067's decision, so no new ADR.
- Pipeline: the change is one property and one helper inside the path ADR-0067 already designed, so the feature stages ran in-session (plan from the task's Context, tests with the change, verify) with no subagents.
- Tests: `HttpProxyTunnelTests.BuildConnectRequest_WithWindows1252_SendsTheProxyHeaderInWindows1252`, `..._WithUtf8_SendsTheProxyHeaderAndUserAgentInUtf8`, `..._ByDefault_SendsTheProxyHeaderInLatin1`; `CurlCompositionProxyTests.CreateProxyTunnelOptions_EncodesCommandLineTextInThePlatformEncoding`. The existing proxy tunnel tests pass unchanged.

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. CONNECT requests carry --proxy-header and -A text in the platform curl's encoding
