---
id: BL-5869
title: Fix fitness finding EXT-119: --resumable-upload silently bypasses --har, --record, -v and --trace-ascii
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.ResumableUpload.UnitLibrary/Transfer/ResumableUploadFlow.cs, Curl.Extension.Composition.UnitLibrary/ExtensionTransferHookCatalogue.cs]
requirement: none
created: 2026-11-24
completed:
---
# BL-5869 — Fix fitness finding EXT-119: --resumable-upload silently bypasses --har, --record, -v and --trace-ascii

## Goal

EXT-119 from the outside fitness review of 2026-11-24 07:08 no longer reproduces: --resumable-upload silently bypasses --har, --record, -v and --trace-ascii. Curl behaves as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-24T0708` (commit `1e5d591`, the `preview` release binaries).

This is an extension (Part B): it is judged against Curl's own documentation (`curl --extended-help`, `Documentation/Protocols.md`, `Documentation/Conformance/Extended/*.md`, the ADRs), never against C curl.

The reviewer's mock servers were Python; rebuild any you need in PowerShell or C# (never Python). Mock servers bind 127.0.0.1 and port numbers are examples. Where the finding refers to another finding's setup, find the task titled with that finding ID. Check that it still reproduces on the current branch first.

Shared setup: **Shared mock for EXT-115, EXT-116, EXT-119, EXT-109, EXT-111** (HTTP, port 27020, logs every request line and header to a file): `OPTIONS /tus` → 204 with tus 1.0.0 headers offering `creation`; `POST /tus` → 201 with `Location: /files/1`; `PATCH /files/1` → 204; `/strong` → 300,000 bytes where `byte[i] = (i*13+5) % 256`, with ETag and Range support; `/plain` → a body with `Cache-Control: max-age=60`. `up.txt` is a 6-byte file. The finding, verbatim:


- **Severity:** Low · **Confidence:** Confirmed (Windows twice, Linux once) · **Area:** `--resumable-upload` with diagnostic options.
- **Repro:**
  - `curl -sS --har ru.har --record rurec --resumable-upload -T up.txt -o NUL http://127.0.0.1:27020/tus; ls ru.har rurec`
  - `curl -sv --resumable-upload -T up.txt -o NUL http://127.0.0.1:27020/tus`
  - `curl -s --trace-ascii tr.txt --resumable-upload -T up.txt -o NUL http://127.0.0.1:27020/tus`
- **Expected:** `--extended-help`: har "Records each HTTP request and response"; record-replay "Records HTTP exchanges to a directory". A plain `-T`, `--segments` and oci all produce HAR entries and `-v` lines. The OPTIONS, POST and PATCH exchanges should appear (or a warning should say they cannot).
- **Actual (.NET):** exit 0; `ru.har` and `rurec` are never created; `-v` prints no `>`/`<`/`*` lines; `tr.txt` is 0 bytes; `--log-file` says `transfer 0 ended: exit 0, 0 bytes` although 6 bytes were uploaded.
- **Likely location:** the resumable hook (`ExtensionTransferHookCatalogue.cs` line 29) completes the transfer without calling `next`, so the HAR (line 39) and RecordReplay (line 46) hooks never run, and its client writes no trace.
- **Acceptance check:** the first command creates `ru.har` with 3 entries (OPTIONS, POST, PATCH) and a non-empty `rurec`; `-v` shows `> PATCH /files/1`.

## Acceptance criteria

- [ ] the first command creates `ru.har` with 3 entries (OPTIONS, POST, PATCH) and a non-empty `rurec`; `-v` shows `> PATCH /files/1`.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-24: Created from fitness review 2026-11-24T0708.