---
id: BL-6071
title: Print one Connection timed out after N in both lines when -m ends an HTTP connect, redirect hops included
priority: Low
assignee: Claude
pipeline: direct
depends-on: [BL-6039]
touches: [Curl.Original.Protocol.Http.UnitLibrary/HttpTransferDeadline.cs, Curl.Original.Protocol.Http.UnitTests/HttpConnectTimedOutLineTests.cs]
requirement: none
created: 2026-11-26
completed:
---
# BL-6071 — Print one Connection timed out after N in both lines when -m ends an HTTP connect, redirect hops included

## Goal

An HTTP connect `-m` ends, a redirect hop's included, prints `Connection timed out after N milliseconds` as a `-v` line and in `curl: (28)` with the same N, never below the `-m` limit, as curl does.

## Context

Found in BL-6039 (ADR-1453). With a linux-x64 publish in WSL, `curl -sSv -m 3 http://192.0.2.20:1/` printed `* Connection timed out after 2855 milliseconds` from `TcpConnector` and `curl: (28) Connection timed out after 2942 milliseconds` from `HttpTransferDeadline.WithinConnectTimeoutAsync`, which counts its own N after the connect unwound and can print one below the limit. A redirect hop that starts well before `-m` runs out gets no `-v` line at all, on every platform: the connector's limit is the whole `-m` again (ADR-0117, BL-510 amendment), so the cancellation reaches it early and escapes. Measure real curl for the hop with `Record-CurlExchange.ps1`.

## Acceptance criteria

- [ ] A unit test pins that the failure's message carries the connector's N when the connector returned the timed-out result.
- [ ] A unit test pins that a connect ended by `-m` that the connector did not report gets the `-v` line once, with the same N as the failure.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-26: Created.
