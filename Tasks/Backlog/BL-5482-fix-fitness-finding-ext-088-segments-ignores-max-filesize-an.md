---
id: BL-5482
title: Fix fitness finding EXT-088: --segments ignores --max-filesize and writes the whole file with exit 0
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.SegmentedDownload.UnitLibrary/SegmentedDownloadTransferHook.cs]
requirement: none
created: 2026-11-18
completed:
---
# BL-5482 — Fix fitness finding EXT-088: --segments ignores --max-filesize and writes the whole file with exit 0

## Goal

EXT-088 from the outside fitness review of 2026-11-18 06:33 no longer reproduces: --segments ignores --max-filesize and writes the whole file with exit 0. Curl matches the reference curl as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-18T0633` (commit `ec53afc`, preview release binaries). In the finding, PORT is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL on WSL Ubuntu. Mock servers bind 127.0.0.1 and port numbers are examples. Where the finding refers to another finding's setup (e.g. "the setup from STD-116"), find the task titled with that finding ID. Measure real curl with `Record-CurlExchange.ps1` before pinning output text. The finding, verbatim:

- **Severity:** Medium (a size limit the user set is silently bypassed)
- **Confidence:** Confirmed (Windows 3 runs, Linux 1 run)
- **Protocol/area:** `--segments` (HTTP), Windows and Linux
- **Repro:** an HTTP/1.1 server on 127.0.0.1:45020 whose `/strong` serves a fixed 300,000-byte body (`byte[i] = (i*13+5) % 256`) with `ETag: "s"` and `Accept-Ranges: bytes`, answering `Range` with a correct 206 and `Content-Range: bytes a-b/300000`, and no Range with 200 and the full body.
  - `curl -sS -m15 --max-filesize 100000 http://127.0.0.1:45020/strong -o m2.out` → `curl: (63) Maximum file size exceeded`, exit 63.
  - `curl -sS -m15 --segments 4 --max-filesize 100000 http://127.0.0.1:45020/strong -o m.out -w "%{http_code} %{size_download}\n"`
- **Expected:** ADR-1087's list of unsplit transfers does not include `--max-filesize`, and split transfers keep curl's behaviour. The probe's `Content-Range` gives the complete length (300,000), above the limit: exit 63, nothing written.
- **Actual (PORT):** `206 300000`, exit 0; `m.out` holds 300,000 bytes (each 75,000-byte segment is under the limit).
- **Likely location:** `Curl.Extension.SegmentedDownload.UnitLibrary/SegmentedDownloadTransferHook.cs` `ExecuteAsync` / `DownloadSegmentsAsync`: compare `first.CompleteLength` with `context.MaxFileSize` before splitting and fail with exit 63 (or run `next(context)`).
- **Acceptance check:** the `--segments` command prints `curl: (63) Maximum file size exceeded`, exits 63, and the server logs only the one-byte probe (or one plain GET).

## Acceptance criteria

- [ ] the `--segments` command prints `curl: (63) Maximum file size exceeded`, exits 63, and the server logs only the one-byte probe (or one plain GET).
- [ ] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-18: Created from fitness review Curl-Fitness-Review-2026-11-18T0633, finding EXT-088 (Medium).