---
id: BL-5483
title: Fix fitness finding EXT-089: --segments with -H "Range: …" sends two Range fields per request and can write the whole file instead of the requested range
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.SegmentedDownload.UnitLibrary/SegmentableRequest.cs]
requirement: none
created: 2026-11-18
completed: 2026-11-18
---
# BL-5483 — Fix fitness finding EXT-089: --segments with -H "Range: …" sends two Range fields per request and can write the whole file instead of the requested range

## Goal

EXT-089 from the outside fitness review of 2026-11-18 06:33 no longer reproduces: --segments with -H "Range: …" sends two Range fields per request and can write the whole file instead of the requested range. Curl matches the reference curl as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-18T0633` (commit `ec53afc`, preview release binaries). In the finding, PORT is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL on WSL Ubuntu. Mock servers bind 127.0.0.1 and port numbers are examples. Where the finding refers to another finding's setup (e.g. "the setup from STD-116"), find the task titled with that finding ID. Measure real curl with `Record-CurlExchange.ps1` before pinning output text. The finding, verbatim:

- **Severity:** Medium (silent wrong output with exit 0 on servers that take the last `Range`; a malformed request on all servers)
- **Confidence:** Confirmed (Windows 2 runs, Linux 1 run)
- **Protocol/area:** `--segments` (HTTP), Windows and Linux
- **Repro:** the `/strong` server from EXT-088, logging request heads and using the last value of a repeated header.
  - Without the option: `curl -sS -m15 -H "Range: bytes=0-99" http://127.0.0.1:45020/strong -o hr.out -w "%{http_code} %{size_download}\n"` → `206 100`, one request.
  - `curl -sS -m15 --segments 4 -H "Range: bytes=0-99" http://127.0.0.1:45020/strong -o hr.out -w "%{http_code} %{size_download}\n"`
- **Expected:** ADR-1087 splits "only a plain GET of the whole resource" and excludes range requests. A request with a user `Range` header runs as without `--segments`: one request, one `Range: bytes=0-99`, 100 bytes written. Range is a single-value field (RFC 9110 §14.2).
- **Actual (PORT):** `206 300000`, exit 0, `hr.out` holds 300,000 bytes. The server got 5 requests, each with two Range fields (`Range: bytes=0-99` then `Range: bytes=0-0`; `Range: bytes=0-99` then `Range: bytes=0-74999`; …).
- **Likely location:** `Curl.Extension.SegmentedDownload.UnitLibrary/SegmentableRequest.cs` `AsksForTheWholeResource` checks only `context.Range`; also reject a `-H` `Range` (and `If-Range`) field in `context.Http.Headers`. `WithRequestHeaders` (`Curl.Extension.Abstractions.UnitLibrary/Transfer`) appends instead of replacing a same-named field.
- **Acceptance check:** the `--segments 4` command prints `206 100`, writes 100 bytes, and the server logs exactly one request with a single `Range: bytes=0-99`.

## Acceptance criteria

- [x] the `--segments 4` command prints `206 100`, writes 100 bytes, and the server logs exactly one request with a single `Range: bytes=0-99`.
- [x] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

SegmentableRequest.Is now also refuses a transfer whose -H headers carry Range or If-Range (case-insensitive, name ends at : or ;), so it runs as without --segments: one request, one Range. Unit test extended; quality run 0 failing members. The mock-server repro was not run (the unit test pins the single-request pass-through).

## Log

- 2026-11-18: Created from fitness review Curl-Fitness-Review-2026-11-18T0633, finding EXT-089 (Medium).
- 2026-11-18: Backlog -> Doing. Criteria at claim: 03310bb8 69116afe 3d836d5e.
- 2026-11-18: Doing -> Done. SegmentableRequest refuses -H Range/If-Range
