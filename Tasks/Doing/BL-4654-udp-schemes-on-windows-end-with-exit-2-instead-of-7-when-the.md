---
id: BL-4654
title: UDP schemes on Windows end with exit 2 instead of 7 when the port is closed
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/UdpChannelOpener.cs, Curl.Networking.UnitLibrary/UdpDatagramConnector.cs, Curl.Networking.UnitLibrary/TcpConnector.UdpTunnel.cs, Curl.Networking.UnitTests/UdpChannelOpenerTests.cs]
requirement: none
created: 2026-11-02
completed:
---
# BL-4654 — UDP schemes on Windows end with exit 2 instead of 7 when the port is closed

## Goal

A `coap`, `coaps`, `dns` or `webtransport` transfer to a closed UDP port on Windows ends with exit 7 (manual, EXIT CODES 7), not exit 2.

## Context

Found by BL-4612 running Curl.Console's curl.exe: `curl -sS coap://localhost:1/x` (and dns, webtransport) ends with `curl: (2) An existing connection was forcibly closed by the remote host.` - Windows reports the ICMP port-unreachable as SocketError.ConnectionReset on the UDP socket, which escapes as exit 2. Measure what curl does for a closed UDP port (HTTP/3 and TFTP) with Record-CurlExchange.ps1 before pinning the text; the in-process conformance harness cannot show this (no real socket).

## Acceptance criteria

- [ ] A closed UDP port maps to exit 7 with curl's text, pinned by a unit test with a fake datagram socket that reports ConnectionReset.
- [ ] Build clean and fast tests green; coverage gates hold.

## Notes

## Log

- 2026-11-02: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: 01bb1e05 8a80dee3.
