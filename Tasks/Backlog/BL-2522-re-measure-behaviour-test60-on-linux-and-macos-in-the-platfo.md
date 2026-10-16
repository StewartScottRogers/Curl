---
id: BL-2522
title: Re-measure behaviour:test60 on Linux and macOS in the platforms workflow after BL-2483
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-2483]
touches: [Documentation/Planning/Decisions/ADR-0648-chunked-stdin-upload-keeps-closing-chunk-out-of-process.md]
lane: no
requirement: none
created: 2026-10-16
completed:
---
# BL-2522 — Re-measure behaviour:test60 on Linux and macOS in the platforms workflow after BL-2483

## Goal

A gap-platforms run on the commit that carries BL-2483 records `behaviour:test60` on Linux and macOS, and its outcome is written into ADR-0648's Consequences.

## Context

- BL-2483 found no product defect: measured on Windows and on Linux (WSL, real pipe), Curl and curl both send test60's closing chunk; ADR-0648 attributes the cross-check's byte-179 difference to the recorder answering between curl 8.22.0's two sends (25 bytes, then 5).
- Interactive only because the measurement and its findings live in the gap office's paths, which lanes may not read or run. Keep `lane: no`: the whole task is the gap office's work, so no step makes it a lane's.
- Run `gh workflow run gap-platforms.yml -f ref=<full 40-character commit>` with the first `work/dark-factory` (or `master`) commit that contains BL-2483's commits, then read `behaviour:test60` on both platforms.
- If it still differs, read which side's `request.bin` is the shorter: if it is the reference's, the cross-check's recorder settings (answering on the first body byte of a request whose `Content-Length: 1` contradicts its chunked coding) need changing in the gap office; if it is Curl's, file a lane task with the recordings.

## Acceptance criteria

- [ ] A gap-platforms run on a full 40-character commit containing BL-2483 is named under Notes with its `behaviour:test60` outcome on Linux and macOS.
- [ ] ADR-0648's Consequences state that outcome, and any follow-up task it needs is filed with the board script and named under Notes.

## Notes

## Log

- 2026-10-16: Created.
