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
completed:
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

- [ ] A new ADR under `Documentation/Planning/Decisions/` with the next free number, status Accepted, titled for the Public Suffix List source and refresh, states that it was decided by Claude under Stewart's delegation, and records the decision, the reasons and the alternatives rejected.
- [ ] `Documentation/Planning/Decisions/README.md` lists the new ADR.
- [ ] The ADR names the resource location, the refresh script's file name (`Update-PublicSuffixList.ps1` at the repository root), the MPL-2.0 attribution text and where it lives.

## Notes

- Record only; the decision is not reopened here.
- Plan item: D6 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.

## Log

- 2026-09-26: Created.
