---
id: BL-155
title: Record the ADR for what -V/--version prints on each platform
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-152, BL-153]
touches: [Documentation/Planning/Decisions]
requirement: none
created: 2026-09-26
completed:
---
# BL-155 — Record the ADR for what -V/--version prints on each platform

## Goal

An Accepted ADR records curl's `-V` format as Curl will print it on Windows, Linux and macOS, with exact lines.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item D5. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- **Decision (already made):** `-V`/`--version` keeps curl's format and version number and tells the truth about the contents. Decided by Claude under Stewart's delegation, 2026-09-26 (root `CLAUDE.md`, "Decisions").
- Line 1: `curl 8.21.0 (<platform triple as curl prints it>) libcurl/8.21.0` followed only by components Curl actually uses - the TLS backend the BCL sits on (Schannel on Windows, OpenSSL on Linux; macOS as the BCL uses there) and nothing it does not contain. Then `Release-Date:`, `Protocols:` listing only the schemes Curl implements, and `Features:` listing only features Curl implements. Both lists grow as work lands, so scripts that feature-detect get true answers.
- Reference: `/mingw64/bin/curl -V` (curl 8.21.0, Release-Date 2026-06-24). Run it and quote its four lines in the ADR as the format model.
- BL-200 implements this.

## Acceptance criteria

- [ ] A new ADR under `Documentation/Planning/Decisions/` with the next free number, status Accepted, titled for what `-V`/`--version` prints, states that it was decided by Claude under Stewart's delegation, and records the decision, the reasons and the alternatives rejected.
- [ ] `Documentation/Planning/Decisions/README.md` lists the new ADR.
- [ ] The ADR quotes `/mingw64/bin/curl -V` as measured, then gives Curl's exact lines for Windows x64, Linux x64 and macOS arm64 as of today (protocols: those registered in `Curl.Console/CurlComposition.cs`), and the rule for keeping `Protocols:` and `Features:` current as features land.
- [ ] Any line not measurable on this machine (Linux, macOS triples) is marked "to confirm against an OpenSSL build of curl 8.21.0" rather than guessed.

## Notes

- Record only; the decision is not reopened here.
- Plan item: D5 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.

## Log

- 2026-09-26: Created.
