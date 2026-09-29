---
id: BL-602
title: Answer a CONNECT tunnel's 407 with the proxy auth scheme chosen
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-601]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-602 — Answer a CONNECT tunnel's 407 with the proxy auth scheme chosen

## Goal

A `CONNECT` answered `407` with `Proxy-Authenticate` is retried with `Proxy-Authorization` for the scheme the proxy auth set allows and libcurl's ranking picks (Basic sent up front; Digest and anyauth after the challenge), as curl 8.21.0 does, and a second `407` fails with curl's exit code and message.

## Context

- Conformance audit 2026-09-28, row 14 (Major). Options: BL-601. NTLM and Negotiate for proxies are BL-604.
- Code: `Curl.Networking.UnitLibrary/HttpProxyTunnel.cs`, `HttpProxyTunnelOptions.cs`, `HttpProxyTunnelReply.cs`; ranking and Digest in `Curl.Authentication.UnitLibrary` (ADR-0028, ADR-0025), composed in `Curl.Console` (ADR-0059: the tunnel takes credentials and encoding from the composition).
- FR-081 pins the current `407` failure.

## Acceptance criteria

- [ ] Measured first with `Record-CurlExchange.ps1 -Connections 2` as the proxy: `-p -x http://127.0.0.1:<P> -U u:p` with `--proxy-basic`, `--proxy-digest` (a `407` with a fixed Digest challenge then `200 Connection established`), `--proxy-anyauth` against Basic and against Digest, and a second `407`; request bytes, stderr and exit code copied into Notes.
- [ ] `Curl.Networking.UnitTests` pin the `CONNECT` requests and outcome for each case (Digest with an injected client nonce).
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
