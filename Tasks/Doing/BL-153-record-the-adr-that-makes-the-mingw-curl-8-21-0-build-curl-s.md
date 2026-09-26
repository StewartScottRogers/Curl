---
id: BL-153
title: Record the ADR that makes the mingw curl 8.21.0 build Curl's Windows HTTP reference
priority: High
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions]
requirement: none
created: 2026-09-26
completed:
---
# BL-153 — Record the ADR that makes the mingw curl 8.21.0 build Curl's Windows HTTP reference

## Goal

An ADR (or an amendment to ADR-0009) records that the mingw build of curl 8.21.0 is the reference for HTTP behaviour on Windows, and where System32 `curl.exe` differs.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item D3. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- **Decision (already made):** the Windows HTTP reference is the mingw build `/mingw64/bin/curl` (curl 8.21.0, first on PATH), as ADR-0009 already names. Decided by Claude under Stewart's delegation, 2026-09-26 (root `CLAUDE.md`, "Decisions").
- Measured 2026-09-26: two Windows builds are installed. `/mingw64/bin/curl` has brotli, zstd, PSL and NTLM, and no HTTP2/HTTP3. `C:\Windows\System32\curl.exe` (also 8.21.0) lacks brotli, zstd, PSL and NTLM, and lacks the rtsp, scp and sftp protocols.
- Curl's roadmap implements those features, so Curl targets the fuller build and is a superset of System32's. `ADR-0009-tls-behaviour-matches-the-platforms-usual-curl-build.md` covers TLS only.
- BL-154 and BL-155 depend on this.

## Acceptance criteria

- [ ] Either ADR-0009 gains a dated "Amendment" section, or a new ADR with the next free number is added and ADR-0009 links it; either way the text states that it was decided by Claude under Stewart's delegation.
- [ ] The record names `/mingw64/bin/curl` as the Windows reference for HTTP, lists the protocols and features System32 `curl.exe` 8.21.0 lacks (rtsp, scp, sftp; brotli, zstd, NTLM, PSL) as measured, and says Curl may differ from System32 `curl.exe` exactly where that build lacks a feature.
- [ ] `Documentation/Planning/Decisions/README.md` lists the new ADR or notes the amendment.

## Notes

- Record only; the decision is not reopened here.
- Plan item: D3 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
