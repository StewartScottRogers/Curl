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
completed:
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

- [ ] An `HttpProxyTunnel` test with the Windows-1252 encoding sends `--proxy-header "X-A: €"` in the CONNECT request as the byte `80`.
- [ ] An `HttpProxyTunnel` test with UTF-8 sends `--proxy-header "X-A: é"` and `-A é` as `C3 A9`.
- [ ] Existing `TcpConnectorTests.Proxy` tests pass unchanged.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` is green.

## Notes

- Filed by BL-373: the HTTP formatter's encoding does not reach the CONNECT request, which `Curl.Networking` builds and which was outside BL-373's `touches`.

## Log

- 2026-09-27: Created.
