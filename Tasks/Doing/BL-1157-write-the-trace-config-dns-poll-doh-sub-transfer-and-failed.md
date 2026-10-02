---
id: BL-1157
title: Write the --trace-config dns poll, DoH sub-transfer and failed-resolve lines
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-1102]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-10-02
completed:
---
# BL-1157 — Write the --trace-config dns poll, DoH sub-transfer and failed-resolve lines

## Goal

Under `-v --trace-config dns` (or `doh`, `all`) Curl also writes the `[DNS]` lines BL-1102 left out: a failed resolve's, the DoH sub-transfers' own `-v` lines prefixed `[DNS] `, and the connect lines through a proxy or Unix socket.

## Context

- ADR-0356 (BL-1102) writes the filter's lines around a direct connect (`DnsFilterTraceEvents`, `TcpConnector.TracesDnsFilter`) and routes `DohDnsResolver`'s lines through `FlowScopedTransferEvents`. Its decision 4 lists what is left; BL-1102's Notes hold curl 8.21.0's measured stderr.
- A failed resolve under DoH ends: `Could not resolve host: example.test`, `[DNS] cache negative name resolve for example.test:P type=A+AAAA`, `Could not resolve: example.test:P`, `[DNS] error resolving: 6`, `[DNS] Curl_conn_connect(block=0) -> 6, done=0`, `[DNS] Curl_conn_connect(), filter returned 6`, `[DNS] [1] shutdown async`, `closing connection #0`, `[DNS] [1] destroy async`.
- The DoH sub-transfers' lines (`[DNS] [DNS] created DNS filter for 127.0.0.1:P ...`, `[DNS]   Trying ...`, TLS lines, `> POST` head, `[DNS] a DoH request is completed, 1 to go`) come from the DoH connector's events, which today are none.
- The multi loop's `resolve incomplete ...` and repeated `done=0` lines depend on poll timing; ADR-0356 decided not to reproduce them. Measure again before changing that.

## Acceptance criteria

- [ ] Measured first with `Record-CurlExchange.ps1` (`-Tls` for DoH): a failed DoH resolve, a successful one, and `-x http://127.0.0.1:P` under `--trace-config dns -v`; stderr in Notes.
- [ ] `Curl.Console.UnitTests` pin the failed-resolve lines and the DoH sub-transfer lines as measured, except the poll-timing lines.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage for each library changed.

## Notes

## Log

- 2026-10-02: Created.
- 2026-10-02: Backlog -> Doing.
