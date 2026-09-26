---
id: BL-154
title: Record the ADR that --compressed advertises deflate, gzip and br until a zstd decoder exists
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-153]
touches: [Documentation/Planning/Decisions, Documentation/Planning/Roadmap.md]
requirement: none
created: 2026-09-26
completed:
---
# BL-154 — Record the ADR that --compressed advertises deflate, gzip and br until a zstd decoder exists

## Goal

An Accepted ADR records that `--compressed` sends `Accept-Encoding: deflate, gzip, br` and decodes those three with BCL streams, and a hand-written zstd decoder is filed under the Roadmap's "Later / unscheduled".

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item D4. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- **Decision (already made):** `--compressed` sends `Accept-Encoding: deflate, gzip, br` on every platform and decodes those with `GZipStream`, `ZLibStream`/`DeflateStream` and `BrotliStream`. Decided by Claude under Stewart's delegation, 2026-09-26 (root `CLAUDE.md`, "Decisions").
- Measured with `curl --compressed` (curl 8.21.0 mingw build): `GET / HTTP/1.1\r\nHost: 127.0.0.1:18081\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\nAccept-Encoding: deflate, gzip, br, zstd\r\n\r\n`. The BCL has no zstd, so the `zstd` token is dropped until a hand-written decoder lands; then the header becomes the measured `deflate, gzip, br, zstd`.
- The divergence is one token in one request header; response output bytes do not differ for servers that honour the header.
- BL-177 (decoding) and BL-191 depend on this.

## Acceptance criteria

- [ ] A new ADR under `Documentation/Planning/Decisions/` with the next free number, status Accepted, titled for what `--compressed` advertises, states that it was decided by Claude under Stewart's delegation, and records the decision, the reasons and the alternatives rejected.
- [ ] `Documentation/Planning/Decisions/README.md` lists the new ADR.
- [ ] The ADR quotes the measured reference header and Curl's header, and states the condition for adding `zstd`.
- [ ] `Documentation/Planning/Roadmap.md`, "Later / unscheduled": an entry "Hand-written zstd decoder (then `--compressed` advertises `zstd`)" links the ADR.

## Notes

- Record only; the decision is not reopened here.
- Plan item: D4 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
