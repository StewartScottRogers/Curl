---
id: BL-152
title: Record the ADR that keeps HTTP/2 out of Milestone 1 and refuses --http2 and --http3
priority: High
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions, Documentation/Product/Product-Overview.md, Documentation/Planning/Roadmap.md]
requirement: none
created: 2026-09-26
completed:
---
# BL-152 — Record the ADR that keeps HTTP/2 out of Milestone 1 and refuses --http2 and --http3

## Goal

An Accepted ADR records that Milestone 1 has no HTTP/2, that `--http2`, `--http2-prior-knowledge` and `--http3` are refused as the reference build refuses them, and that a hand-written HTTP/2 is filed under the Roadmap's "Later / unscheduled".

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item D2. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- **Decision (already made):** no HTTP/2 in Milestone 1. Decided by Claude under Stewart's delegation, 2026-09-26 (root `CLAUDE.md`, "Decisions").
- Every platform behaves like the Windows reference build, which has no HTTP2 feature. Measured on both Windows builds of curl 8.21.0 (`/mingw64/bin/curl` and `C:\Windows\System32\curl.exe`): `--http2` and `--http3` exit 2 with `curl: option --http2: the installed libcurl version does not support this` followed by `curl: try 'curl --help' or 'curl --manual' for more information`.
- The .NET 10 BCL has no public HPACK or HTTP/2 framing, so HTTP/2 means a hand-written implementation over `IConnection`. Linux OpenSSL builds of curl usually have nghttp2, so this is a known divergence on Linux and macOS; the ADR records it.
- This answers open question 5 in `Documentation/Product/Product-Overview.md` ("HTTP/2 and HTTP/3: BCL framing, or implemented over the seam like everything else?").
- BL-191 (parse `--http2`/`--http3`) and BL-155 (`-V` output) depend on this.

## Acceptance criteria

- [ ] A new ADR under `Documentation/Planning/Decisions/` with the next free number, status Accepted, titled for HTTP/2 and HTTP/3 in Milestone 1, states that it was decided by Claude under Stewart's delegation, and records the decision, the reasons and the alternatives rejected.
- [ ] `Documentation/Planning/Decisions/README.md` lists the new ADR.
- [ ] The ADR quotes the measured refusal lines for `--http2` and `--http3`, states that `--http2-prior-knowledge` is refused the same way (measured on curl 8.21.0 and quoted), and records the Linux/macOS divergence.
- [ ] `Documentation/Product/Product-Overview.md`, "Open questions": row 5 is struck through and marked **Answered**, linking the ADR.
- [ ] `Documentation/Planning/Roadmap.md`, "Later / unscheduled": an entry "Hand-written HTTP/2 over `IConnection` (HPACK and framing; the BCL has none)" links the ADR.

## Notes

- Record only; the decision is not reopened here.
- Plan item: D2 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.

## Log

- 2026-09-26: Created.
