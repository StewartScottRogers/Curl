---
id: BL-872
title: Print CONNECT: no ALPN negotiated before a tunnel through an HTTPS proxy
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-753]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-09-29
completed:
---
# BL-872 — Print CONNECT: no ALPN negotiated before a tunnel through an HTTPS proxy

## Goal

`-v` through an HTTPS proxy tunnel prints curl's `CONNECT: no ALPN negotiated` line after the proxy's handshake and before the CONNECT request, as curl 8.21.0 does on both builds.

## Context

- Measured in BL-753 (Notes): Schannel prints `* CONNECT: no ALPN negotiated` then `* Establishing HTTP proxy tunnel to example.test:80`; OpenSSL prints `* CONNECT: no ALPN negotiated` then `* allocate connect buffer`. Printed with and without `--no-alpn` when the proxy picks no protocol. What it prints when the proxy picks `http/1.1` is not measured: measure it (the recorder's `-Tls` server would need to select ALPN).
- Where: `Curl.Networking.UnitLibrary/TcpConnector.cs` `OpenTunnelOverTlsAsync`; the negotiated protocol is on the handshake report (`TlsHandshakeEvent`). ADR-0190.
- The other CONNECT-phase lines (`Establishing HTTP proxy tunnel`, `CONNECT phase completed`, `CONNECT tunnel established, response 200`) are not printed either; check BL-863 and file them separately if no task covers them.

## Acceptance criteria

- [ ] The measured lines, with the proxy agreeing and not agreeing on `http/1.1`, are in Notes per build.
- [ ] A `TcpConnector` test pins the line's text and position for a tunnel through an HTTPS proxy.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
