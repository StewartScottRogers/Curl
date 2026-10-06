---
id: BL-413
title: Apply --resolve and --connect-to to the UDP connector TFTP uses
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-244]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-27
completed: 2026-09-27
---
# BL-413 — Apply --resolve and --connect-to to the UDP connector TFTP uses

## Goal

A `tftp://` transfer resolves through `--resolve` entries and follows `--connect-to` mappings, as curl 8.21.0 does for every connection it makes.

## Context

- Found in BL-244: `CurlComposition.CreateTcpConnector` hands `ResolveOverrides` and `ConnectToMappings` to `TcpConnector`, but `UdpDatagramConnector` (Curl.Networking.UnitLibrary) takes only an `IDnsResolver`, so TFTP still asks the system resolver and dials the URL's host and port.
- Measure real curl 8.21.0 with `Record-CurlExchange.ps1` (or a loopback UDP responder added to it) before pinning behaviour: whether `--connect-to` applies to TFTP, and the exit 49 messages for a malformed entry.

## Acceptance criteria

- [x] A `Curl.Networking.UnitTests` test shows `UdpDatagramConnector` opening its channel at a `--resolve` address and, if measured curl applies it, at a `--connect-to` mapped host and port.
- [x] A `Curl.Console.UnitTests` test shows `CurlComposition.CreateTransports` builds the UDP connector with the command line's `--resolve` and `--connect-to` values.
- [x] `dotnet build -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage with no failing member for `Curl.Networking.UnitLibrary` and `Curl.Console` (`-IncludeIntegration` for Curl.Console).

## Notes

- Measured curl 8.21.0 (Schannel, Windows) on 2026-09-27 with `tftp://tftp.test:6969/x` against loopback: `--resolve tftp.test:6969:127.0.0.1` opens at 127.0.0.1:6969; `--connect-to` applies to TFTP (`tftp.test:6969:127.0.0.1:6970` opens at 127.0.0.1:6970, ignoring a `--resolve` for the URL's host); `--connect-to tftp.test:6969:other:` with `--resolve other:6969:127.0.0.1` opens at 127.0.0.1:6969, so the mapped host and port are what `--resolve` matches; a mapped host that does not resolve gives `(6) Could not resolve host: <mapped host>`; `--resolve bad` gives `(49) Could not parse CURLOPT_RESOLVE entry 'bad'` and `--connect-to tftp.test:6969:[::1:9` gives `(49) Invalid IPv6 address format in '[::1:9'`. Record-CurlExchange.ps1 was not needed: no server bytes were recorded, only curl's `-v` lines and exit codes with nothing listening.
- `UdpDatagramConnector` takes optional `ResolveOverrides` and `ConnectToMappings` exactly as `TcpConnector` does and applies them the same way; `CurlComposition.CreateUdpDatagramConnector` builds it from the command line.
- Choice: the exit 7 message with a matching mapping reads `... via <mapped host>:<mapped port> after ...`, copied from `TcpConnector`'s measured TCP form. It could not be measured for UDP (curl only fails a UDP open when no socket can be created), so it follows the TCP wording as the closest measured case. No ADR: every other behaviour is measured, not chosen, and `Documentation` is outside this task's `touches`.

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. tftp:// transfers resolve through --resolve and follow --connect-to, failing with exit 49 on a malformed entry as curl 8.21.0 does
