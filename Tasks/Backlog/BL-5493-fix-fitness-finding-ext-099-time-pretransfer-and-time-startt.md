---
id: BL-5493
title: Fix fitness finding EXT-099: %{time_pretransfer} and %{time_starttransfer} are always 0 for extension schemes; UDP schemes report %{local_port} 0, an empty %{local_ip} and %{time_connect} 0
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/EndPointReportingProtocolHandler.cs, Curl.Networking.UnitLibrary/UdpDatagramConnector.cs]
requirement: none
created: 2026-11-18
completed:
---
# BL-5493 — Fix fitness finding EXT-099: %{time_pretransfer} and %{time_starttransfer} are always 0 for extension schemes; UDP schemes report %{local_port} 0, an empty %{local_ip} and %{time_connect} 0

## Goal

EXT-099 from the outside fitness review of 2026-11-18 06:33 no longer reproduces: %{time_pretransfer} and %{time_starttransfer} are always 0 for extension schemes; UDP schemes report %{local_port} 0, an empty %{local_ip} and %{time_connect} 0. Curl matches the reference curl as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-18T0633` (commit `ec53afc`, preview release binaries). In the finding, PORT is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL on WSL Ubuntu. Mock servers bind 127.0.0.1 and port numbers are examples. Where the finding refers to another finding's setup (e.g. "the setup from STD-116"), find the task titled with that finding ID. Measure real curl with `Record-CurlExchange.ps1` before pinning output text. The finding, verbatim:

- **Severity:** Low
- **Confidence:** Confirmed (twice per scheme)
- **Protocol/area:** write-out for redis, kafka, mcp, dns, coap (all messaging schemes show the same zeros)
- **Repro:** redis as in EXT-098 (key `k` set); kafka plaintext on 127.0.0.1:46092 with topic `wo` holding one record; any MCP Streamable HTTP server on 46904; a CoAP server on 127.0.0.1:46905 answering 2.05 `hello coap`; a DNS server on 127.0.0.1:46908 answering one A record.
  - `curl -sS -m 5 -u :rpw -o NUL -w "[pt=%{time_pretransfer} st=%{time_starttransfer} t=%{time_total} down=%{size_download} lp=%{local_port} li=%{local_ip}]\n" redis://127.0.0.1:46379/GET/k`
  - the same format with `kafka://127.0.0.1:46092/wo`, `coap://127.0.0.1:46905/res`, `dns://127.0.0.1:46908/example.com`, `mcp://127.0.0.1:46904/mcp`
- **Expected:** consistent with the port's HTTP (`[pt=0.002834 st=0.003140 t=0.003250 …]`) and curl's definitions: a transfer that received bytes has 0 < pt ≤ st ≤ t; a UDP transfer has a real local port and address.
- **Actual (PORT):** redis `[pt=0.000000 st=0.000000 t=0.004349 down=4 lp=61330 li=127.0.0.1]`; coap `[pt=0.000000 st=0.000000 t=0.003136 down=10 lp=0 li=]`; dns over UDP `lp=0 li=` and `%{time_connect}` 0; mcp `pt=0 st=0`.
- **Likely location:** `Curl.Engine.UnitLibrary/EndPointReportingProtocolHandler.cs` builds `new TransferTimings(started, recorder.FirstConnectTimings, null, null, null, now)`; `ConnectionEndPointRecorder` / `Curl.Networking.UnitLibrary/UdpDatagramConnector.cs` do not record the UDP local end point.
- **Acceptance check:** the redis command prints pt > 0 and st > 0 with st ≤ t; the coap command prints `lp=<non-zero> li=127.0.0.1`.

## Acceptance criteria

- [ ] the redis command prints pt > 0 and st > 0 with st ≤ t; the coap command prints `lp=<non-zero> li=127.0.0.1`.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-18: Created from fitness review Curl-Fitness-Review-2026-11-18T0633, finding EXT-099 (Low).