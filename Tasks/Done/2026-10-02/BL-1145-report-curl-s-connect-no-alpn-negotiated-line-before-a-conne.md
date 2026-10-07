---
id: BL-1145
title: Report curl's CONNECT: no ALPN negotiated line before a CONNECT through a plain HTTP proxy
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-10-01
completed: 2026-10-02
---
# BL-1145 — Report curl's CONNECT: no ALPN negotiated line before a CONNECT through a plain HTTP proxy

## Goal

`-p` through a plain `http://` proxy reports `CONNECT: no ALPN negotiated` before each CONNECT, as curl 8.21.0 (Schannel) prints it.

## Context

Measured in BL-863 (Notes): curl 8.21.0 mingw Schannel, `Record-CurlExchange.ps1 -Port 18863` as the proxy, `-v -p -x http://127.0.0.1:18863 -U u:p --proxy-digest http://example.test/` prints `*   Trying 127.0.0.1:18863...`, `* CONNECT: no ALPN negotiated`, `* Proxy auth using Digest with user 'u'`, `* Establishing HTTP proxy tunnel to example.test:80` - and again after `Connect me again please` and its `Trying`. `TcpConnector` writes the ALPN line only in `OpenTunnelOverTlsAsync` (BL-872); `OpenTunnelAsync` and `ConnectTunnelVerboseLines` (BL-863) are where the plain proxy goes. Measure the OpenSSL build if one can be had (BL-872 Notes say it prints the line too, then `allocate connect buffer`, BL-964). Tests: `TcpConnectorTests.ProxyAuthVerbose.cs`, `TcpConnectorTests.Events.cs`.

## Acceptance criteria

- [x] A plain HTTP proxy tunnel reports `CONNECT: no ALPN negotiated` right after `Trying` and before `Proxy auth using`/`Establishing HTTP proxy tunnel to`, on each dial, pinned by the BL-863 transcript tests.
- [x] `dotnet build -warnaserror` clean, fast tests green, `Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary` 100% line and branch, no failing member.

## Notes

## Log

- 2026-10-01: Created.
- 2026-10-02: Backlog -> Doing.
- 2026-10-02: Doing -> Done. A plain HTTP proxy tunnel reports CONNECT: no ALPN negotiated after each Trying, as curl 8.21.0 does
