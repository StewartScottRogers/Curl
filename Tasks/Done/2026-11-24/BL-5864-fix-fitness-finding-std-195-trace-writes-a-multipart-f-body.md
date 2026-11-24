---
id: BL-5864
title: Fix fitness finding STD-195: --trace writes a multipart -F body with file parts as one "Send data" block, where curl splits it per part
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Core.UnitLibrary/Multipart/ConcatenatedReadStream.cs, Curl.Core.UnitTests/Multipart/ConcatenatedReadStreamChunkReadTests.cs]
requirement: none
created: 2026-11-24
completed: 2026-11-24
---
# BL-5864 — Fix fitness finding STD-195: --trace writes a multipart -F body with file parts as one "Send data" block, where curl splits it per part

## Goal

STD-195 from the outside fitness review of 2026-11-24 07:08 no longer reproduces: --trace writes a multipart -F body with file parts as one "Send data" block, where curl splits it per part. Curl behaves as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-24T0708` (commit `1e5d591`, the `preview` release binaries).

In the finding, .NET is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL/OpenLDAP on WSL Ubuntu. Measure real curl with `Record-CurlExchange.ps1` before pinning output text.

The reviewer's mock servers were Python; rebuild any you need in PowerShell or C# (never Python). Mock servers bind 127.0.0.1 and port numbers are examples. Where the finding refers to another finding's setup, find the task titled with that finding ID. Check that it still reproduces on the current branch first. The finding, verbatim:


- **Severity:** Low (trace format) · **Confidence:** Confirmed (serial and `-Z`) · **Area:** `--trace` / `-F`. Neighbour of this round's BL-5800.
- **Setup:** an HTTP server echoing requests on 127.0.0.1:23000; files `f1.txt` = `AAAA`, `f2.txt` = `BBBBBBBB`, `f3.txt` = `CC`.
- **Repro:** `curl -sS -o NUL -F "a=@f1.txt,f2.txt" -F "b=@f3.txt" -F "c=<f2.txt" --trace t1.txt http://127.0.0.1:23000/echo/1`, then `grep "=> Send data" t1.txt`
- **Expected (REF):** `Send data, 448`, `203`, `98`, `62` bytes.
- **Actual (.NET):** a single `Send data, 811 bytes`. Also `-F b=@f3.txt -F c=<f2.txt`: REF 237 + 62, port 299; `-F a=@f1.txt,f2.txt`: REF 448 + 116, port 564. Single-file `-F a=@f1.txt` matches (197). Wire bytes are identical.
- **Likely location:** the HTTP mime body sender / trace-size hand-off (BL-5800): one debug callback per request body instead of one per mime read chunk that breaks at each file part.
- **Acceptance check:** the repro's trace shows four Send data blocks: 448, 203, 98, 62.

## Acceptance criteria

- [x] the repro's trace shows four Send data blocks: 448, 203, 98, 62.
- [x] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- The fix is in ConcatenatedReadStream.cs (the read-once-per-fill rule for file segments applied only when a callback segment existed; now it always applies, as curl's mime_read stops filling after a file read), so touches was moved there from MultipartFormBodyBuilder.cs. Measured: the repro now traces 448, 203, 98, 62. Pinned by a unit test in ConcatenatedReadStreamChunkReadTests. Full fast suite green; Measure-CodeQuality for Curl.Core.UnitLibrary: 0 failing members. CI on Linux/macOS: change is platform-neutral.

## Log

- 2026-11-24: Created from fitness review 2026-11-24T0708.
- 2026-11-24: Backlog -> Doing. Criteria at claim: 2d5f458c 69116afe 3d836d5e.
- 2026-11-24: Doing -> Done. Trace splits multipart body per file part: 448, 203, 98, 62
