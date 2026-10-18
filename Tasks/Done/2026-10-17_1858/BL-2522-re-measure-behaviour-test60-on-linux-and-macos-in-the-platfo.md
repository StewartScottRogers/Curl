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
completed: 2026-10-17
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

- [x] A gap-platforms run on a full 40-character commit containing BL-2483 is named under Notes with its `behaviour:test60` outcome on Linux and macOS.
- [x] ADR-0648's Consequences state that outcome, and any follow-up task it needs is filed with the board script and named under Notes.

## Notes

- 2026-10-17: gap-platforms run 39063808349 (`gh workflow run gap-platforms.yml -f ref=777f9cf84c9a4cf4c210ac4d45ac53591f08b4ea -f request=BL-2522`; that commit contains BL-2483's ef4fa060d) finished green. `behaviour:test60` is `gap` on Linux and on macOS: "request differs at byte 179", evidence "was match; in-process and out-of-process differ".
- The artifacts hold no recordings, so the shorter side follows from the byte count. test60's head is 154 bytes, and curl 8.22.0's first write (the data chunk) is 25 more, ending at 179. Curl writes the data chunk and the closing chunk together. So the reference's `request.bin` is the shorter: the recorder answered on `Content-Length: 1` and closed (`swsclose`, no hold-open) before curl's 5-byte closing chunk.
- Upstream `tests/server/sws.c` at curl-8_22_0 (`ProcessRequest`) reads a chunked request to `\r\n0\r\n\r\n` even with a `Content-Length`. `Record-CurlExchange.ps1`'s `Test-RequestComplete` checks `Content-Length` first. Follow-up filed: BL-2647, lane-eligible, touching only `Record-CurlExchange.ps1`. The gap run after it re-measures test60.
- ADR-0648: the Context's claim that the recorder answered the closing chunk as a held request was wrong (test60's `swsclose` turns hold-open off), so it is corrected; the outcome is in Consequences.

## Log

- 2026-10-16: Created.
- 2026-10-17: Backlog -> Doing. Interactive session: re-measure test60 on Linux and macOS. Criteria at claim: f0615e83 7305b3ae.
- 2026-10-17: Doing -> Done. test60 still differs on Linux and macOS; the recorder, not Curl, is the cause; BL-2647 filed.
