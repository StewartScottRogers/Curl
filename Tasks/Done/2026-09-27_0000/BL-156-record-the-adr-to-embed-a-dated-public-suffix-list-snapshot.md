---
id: BL-156
title: Record the ADR to embed a dated Public Suffix List snapshot in Curl.Cookies
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-151]
touches: [Documentation/Planning/Decisions]
requirement: none
created: 2026-09-26
completed: 2026-09-27
---
# BL-156 — Record the ADR to embed a dated Public Suffix List snapshot in Curl.Cookies

## Goal

An Accepted ADR records that the Public Suffix List is an embedded, dated snapshot in `Curl.Cookies.UnitLibrary`, parsed by hand and refreshed by a script.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item D6. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- **Decision (already made):** embed a dated snapshot of the PSL data (https://publicsuffix.org/list/public_suffix_list.dat, MPL-2.0 data, fine alongside MIT code; attribution recorded) as an embedded resource in `Curl.Cookies.UnitLibrary`, parsed by hand. Refreshed by a small script - a loose file in the `Scripts` solution folder of `Curl.slnx` - run by a task when needed, not at build time. Decided by Claude under Stewart's delegation, 2026-09-26 (root `CLAUDE.md`, "Decisions").
- The reference build has the PSL feature (BL-153); curl uses it to refuse cookies set on a public suffix.
- BL-223 depends on this.

## Acceptance criteria

- [x] A new ADR under `Documentation/Planning/Decisions/` with the next free number, status Accepted, titled for the Public Suffix List source and refresh, states that it was decided by Claude under Stewart's delegation, and records the decision, the reasons and the alternatives rejected.
- [x] `Documentation/Planning/Decisions/README.md` lists the new ADR.
- [x] The ADR names the resource location, the refresh script's file name (`Update-PublicSuffixList.ps1` at the repository root), the MPL-2.0 attribution text and where it lives.

## Notes

- Record only; the decision is not reopened here.
- Plan item: D6 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.
- 2026-09-27: Recorded as ADR-0049. Measured the reference build's libpsl (`psl.exe --print-info`): built-in list only, dated 2024-01-13, 9556 suffixes. Chose to keep a refreshable current snapshot rather than pin that list (reasons in the ADR). Chose the resource path `Curl.Cookies.UnitLibrary/PublicSuffixList/public_suffix_list.dat`, a dated `//` comment line as the first line, and the attribution in the file's own MPL notice plus `PublicSuffixList/README.md` beside it, all inside BL-222's `touches`.

## Log

- 2026-09-26: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. ADR-0049 records the embedded, dated Public Suffix List snapshot, its MPL-2.0 attribution and Update-PublicSuffixList.ps1
