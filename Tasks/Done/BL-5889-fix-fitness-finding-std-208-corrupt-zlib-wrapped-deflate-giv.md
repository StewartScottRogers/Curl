---
id: BL-5889
title: Fix fitness finding STD-208: corrupt zlib-wrapped deflate gives a generic message and leaves partly decoded bytes in the -o file
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Http.UnitLibrary/HttpContentDecoder.cs, Curl.Original.Protocol.Http.UnitLibrary/HttpContentCodingDecoder.cs, Curl.Original.Protocol.Http.UnitTests/CorruptZLibDeflateOutputTests.cs]
requirement: none
created: 2026-11-24
completed: 2026-11-24
---
# BL-5889 — Fix fitness finding STD-208: corrupt zlib-wrapped deflate gives a generic message and leaves partly decoded bytes in the -o file

## Goal

STD-208 from the outside fitness review of 2026-11-24 13:58 no longer reproduces: corrupt zlib-wrapped deflate gives a generic message and leaves partly decoded bytes in the -o file. Curl behaves as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-24T1358` (commit `1e5d591`, the `preview` release binaries). That review was a deeper second pass of the same commit as `Curl-Fitness-Review-2026-11-24T0708`, covering areas the earlier pass did not test.

In the finding, .NET is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL on WSL Ubuntu. Measure real curl with `Record-CurlExchange.ps1` before pinning output text.

The reviewer's mock servers were Python, and some findings used real servers in Docker (Dovecot, GreenMail, Samba, Mosquitto, Kafka, Prosody, SeaweedFS, Azurite); rebuild any mock you need in PowerShell or C# (never Python). Mock servers bind 127.0.0.1 and port numbers are examples. Check that it still reproduces on the current branch first.

The finding, verbatim:

- **Severity:** Low · **Confidence:** Confirmed (Linux 2 runs, Windows 1) · **Area:** HTTP `--compressed`. Both platforms.
- **Mock:** `/enc?t=deflate&garbage=1` answers `Content-Encoding: deflate` with `zlib.compress(b"The quick brown fox…")` where garbage bytes are inserted after byte 10.
- **Repro:** `curl -sS --compressed -o o.txt "http://127.0.0.1:32080/enc?t=deflate&garbage=1"; echo $?; cat o.txt`
- **Expected (REF 8.22 and REF2):** `curl: (61) Error while processing content unencoding: invalid stored block lengths`, exit 61, and no `o.txt` is created.
- **Actual (.NET):** `curl: (61) Unrecognized or bad HTTP Content or Transfer-Encoding`, exit 61, and `o.txt` exists holding `The q` (the bytes decoded before the error). The gzip form of the same corruption already matches REF.
- **Likely location:** `Curl.Original.Protocol.Http.UnitLibrary/HttpContentDecoder.cs` / `HttpContentCodingDecoder.cs`: the zlib-wrapper deflate path falls back to the generic bad-encoding error and writes output before the error is detected.
- **Acceptance check:** the repro prints REF's message, exits 61, and `o.txt` does not exist.

## Acceptance criteria

- [x] the repro prints REF's message, exits 61, and `o.txt` does not exist.
- [x] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Delivered directly in one pass (small, single-file fix; budget): no architect or sub-agent stages.
- Added the new test file to `touches`; no task in Doing on origin/work/dark-factory names Http.UnitTests files.
- Cause: `FeedLengths` feeds the first 8 bytes singly, so `The q` was decoded and written before the corrupt byte.
- Fix: a `deflate` zlib stream holds its first decoded bytes until the piece decodes (ADR-1365, which records the multi-block limit). `HttpContentDecoder.cs` needed no change.
- Measured 2026-11-24 with `Record-CurlExchange.ps1`, Git's curl 8.22.0 Schannel and the built Curl, on the pangram zlib-compressed with eight bytes inserted after byte 10. Bytes 0x05: both exit 61 `invalid stored block lengths`, no `o.txt`. Bytes 0xFF: both exit 61 `incorrect data check`, `o.txt` created.
- Quality: `Measure-CodeQuality.ps1` found one member over the gate (`ReadAll`, 16); split it, then confirmed with a Cobertura run of Http.UnitTests that every `HttpContentCodingDecoder` member is at most 10 with 100% line and branch coverage. The full script was not rerun.
## Log

- 2026-11-24: Created from fitness review 2026-11-24T1358.
- 2026-11-24: Backlog -> Doing. Criteria at claim: 0035ce7d 69116afe 3d836d5e.
- 2026-11-24: Doing -> Done. A deflate zlib body corrupt in its first piece writes nothing and reports curl's message.
