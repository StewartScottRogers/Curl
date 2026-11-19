---
id: BL-5481
title: Fix fitness finding EXT-087: --segments with --compressed fails with exit 61 on a gzip-encoded resource that downloads fine without --segments
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.SegmentedDownload.UnitLibrary/SegmentableRequest.cs, Curl.Extension.SegmentedDownload.UnitTests/SegmentedDownloadTransferHookTests.cs, Curl.Extension.SegmentedDownload.UnitLibrary/SegmentedDownloadTransferHook.cs, Curl.Extension.Abstractions.UnitLibrary/Transfer/TransferContextContentDecoding.cs]
requirement: none
created: 2026-11-18
completed: 2026-11-18
---
# BL-5481 — Fix fitness finding EXT-087: --segments with --compressed fails with exit 61 on a gzip-encoded resource that downloads fine without --segments

## Goal

EXT-087 from the outside fitness review of 2026-11-18 06:33 no longer reproduces: --segments with --compressed fails with exit 61 on a gzip-encoded resource that downloads fine without --segments. Curl matches the reference curl as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-18T0633` (commit `ec53afc`, preview release binaries). In the finding, PORT is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL on WSL Ubuntu. Mock servers bind 127.0.0.1 and port numbers are examples. Where the finding refers to another finding's setup (e.g. "the setup from STD-116"), find the task titled with that finding ID. Measure real curl with `Record-CurlExchange.ps1` before pinning output text. The finding, verbatim:

- **Severity:** Medium (a working download fails when the option is added)
- **Confidence:** Confirmed (Windows 3 runs, Linux 1 run)
- **Protocol/area:** `--segments` (HTTP), Windows and Linux
- **Repro:** an HTTP/1.1 server on 127.0.0.1:45020 whose `/gzip` serves a fixed byte string `GZ` = gzip of the 20,000 lines `line %06d hello world\n` (51,830 bytes), always with `Content-Encoding: gzip`, `Content-Type: text/plain`, `ETag: "g"`, `Accept-Ranges: bytes`; it answers `Range: bytes=a-b` with 206, `Content-Range: bytes a-b/51830` and `GZ[a..b]`, and no Range with 200 and all of `GZ` (how S3 or nginx `gzip_static` serve stored compressed objects).
  - `curl -sS -m15 --compressed http://127.0.0.1:45020/gzip -o g2.out` → exit 0, 480,000 bytes.
  - `curl -sS -m15 --segments 4 --compressed http://127.0.0.1:45020/gzip -o g.out`
- **Expected:** ADR-1087: "Without `--segments`, and for every transfer it does not split, curl's behaviour is unchanged", and a segment that does not fit "runs the transfer again as curl runs it". The ranges are byte ranges of the encoded representation, so they must be joined before decoding (or the transfer not split). Result: exit 0, `g.out` = `g2.out`.
- **Actual (PORT):** `curl: (61) Error while processing content unencoding: incorrect header check`, exit 61, no output file — each segment is decoded on its own.
- **Likely location:** `Curl.Extension.SegmentedDownload.UnitLibrary/SegmentedDownloadTransferHook.cs` (`DownloadSegmentsAsync`, `CapturedResponse.CaptureAsync`). Capture segments with decoding off (`Curl.Extension.Abstractions.UnitLibrary/Transfer/TransferContextContentDecoding.cs`) and decode the joined body once, or exclude `--compressed` in `SegmentableRequest.Is` / fall back to a plain GET when a segment carries `Content-Encoding`.
- **Acceptance check:** the `--segments 4 --compressed` command exits 0 and `g.out` equals `g2.out` byte for byte (480,000 bytes).

## Acceptance criteria

- [x] the `--segments 4 --compressed` command exits 0 and `g.out` equals `g2.out` byte for byte (480,000 bytes).
- [x] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Decision: a `--compressed` transfer is not split (SegmentableRequest.Is), so it runs as curl runs it; the ranges are of the encoded body and cannot be decoded alone. Simplest option the finding offers. Added SegmentableRequest.cs and the hook tests file to touches. Criterion 1 follows from the unsplit path being plain curl behaviour; the unit test pins that it is passed through unchanged. Quality run: 0 failing members. Platform-neutral.

## Log

- 2026-11-18: Created from fitness review Curl-Fitness-Review-2026-11-18T0633, finding EXT-087 (Medium).
- 2026-11-18: Backlog -> Doing. Criteria at claim: 72aa2f2e 69116afe 3d836d5e.
- 2026-11-18: Doing -> Done. --compressed transfers are no longer split; unit test added
