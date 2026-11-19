---
id: BL-5494
title: Fix fitness finding EXT-100: dns://localhost and coap://localhost fail with exit 7 against a server listening only on 127.0.0.1; the IPv4 address is never tried
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/UdpDatagramConnector.cs]
requirement: none
created: 2026-11-18
completed:
---
# BL-5494 — Fix fitness finding EXT-100: dns://localhost and coap://localhost fail with exit 7 against a server listening only on 127.0.0.1; the IPv4 address is never tried

## Goal

EXT-100 from the outside fitness review of 2026-11-18 06:33 no longer reproduces: dns://localhost and coap://localhost fail with exit 7 against a server listening only on 127.0.0.1; the IPv4 address is never tried. Curl matches the reference curl as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-18T0633` (commit `ec53afc`, preview release binaries). In the finding, PORT is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL on WSL Ubuntu. Mock servers bind 127.0.0.1 and port numbers are examples. Where the finding refers to another finding's setup (e.g. "the setup from STD-116"), find the task titled with that finding ID. Measure real curl with `Record-CurlExchange.ps1` before pinning output text. The finding, verbatim:

- **Severity:** Low
- **Confidence:** Confirmed (dns twice, coap twice)
- **Protocol/area:** dns (UDP), coap (and probably every UDP extension scheme)
- **Repro:** a UDP DNS server bound to 127.0.0.1:46908 only (also listening on TCP for the control) and a CoAP server bound to 127.0.0.1:46905 only.
  - `curl -sS -m 5 dns://localhost:46908/example.com`
  - `curl -sS -m 5 --dns-transport tcp dns://localhost:46908/example.com` (control)
  - `curl -sS -m 4 coap://localhost:46905/res`
  - Control: `curl -sS -m 4 tftp://localhost:46905/f` reaches the IPv4 server.
- **Expected:** consistent with the port's TCP schemes and TFTP, which move on to 127.0.0.1 after `::1` refuses; `--extended-help` (dns: "sends an RFC 1035 query over UDP"; coap: "Makes CoAP requests over UDP") makes no exception for localhost.
- **Actual (PORT):** `curl: (7) Failed to connect to localhost:46908 after 2 ms: Could not connect to server`, exit 7; `-v` shows `Established connection to localhost (::1 port 46905)` then exit 7. The TCP control and `127.0.0.1` URLs succeed.
- **Likely location:** `Curl.Networking.UnitLibrary/UdpDatagramConnector.cs` `OpenFirstAvailable` (~line 210) opens only the first resolved address; `DatagramPortUnreachableException` becomes exit 7 in `EndPointReportingProtocolHandler` without trying the next address.
- **Acceptance check:** `curl -sS -m 5 dns://localhost:46908/example.com` prints the answer section, exit 0, with the server bound only to 127.0.0.1.

## Acceptance criteria

- [ ] `curl -sS -m 5 dns://localhost:46908/example.com` prints the answer section, exit 0, with the server bound only to 127.0.0.1.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-18: Created from fitness review Curl-Fitness-Review-2026-11-18T0633, finding EXT-100 (Low).
- 2026-11-18: Backlog -> Doing. Criteria at claim: 4cc38eaa 69116afe 3d836d5e.
