---
id: BL-151
title: Record the ADR that moves Curl.Authentication and Curl.Cookies into Milestone 1
priority: High
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions, Documentation/Product/Product-Overview.md, Documentation/Planning/Roadmap.md]
requirement: none
created: 2026-09-26
completed:
---
# BL-151 — Record the ADR that moves Curl.Authentication and Curl.Cookies into Milestone 1

## Goal

An Accepted ADR records that `Curl.Authentication` and `Curl.Cookies` are built in Milestone 1 (Phase 1), and the Product Overview phasing table and the Roadmap say so.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item D1. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- **Decision (already made):** yes - Authentication and Cookies move from Phase 2 into Milestone 1. Decided by Claude under Stewart's delegation, 2026-09-26 (root `CLAUDE.md`, "Decisions").
- Why: `curl http(s)://` with `-u`, `--digest`, `-b`/`-c` is everyday curl; without them Curl is not a drop-in for HTTP. Both libraries are empty today and disjoint from `Curl.Protocol.Http.UnitLibrary`, so they add parallel work for the dark factory lanes.
- `Documentation/Product/Product-Overview.md`, "Phasing": Phase 2 lists `Ftp`, `Ssh`, `Authentication`, `Cookies`. `Documentation/Planning/Roadmap.md`, "Milestone 1", does not mention them.
- The Phase 1 HTTP plan keeps its Basic/Bearer, Digest and scheme-choice work (BL-216 to BL-218), cookie work (BL-219 to BL-223), handler seams (BL-181, BL-182) and Console composition (BL-237) in Milestone 1 on the strength of this decision.

## Acceptance criteria

- [ ] A new ADR under `Documentation/Planning/Decisions/` with the next free number, status Accepted, titled for moving Authentication and Cookies into Milestone 1, states that it was decided by Claude under Stewart's delegation, and records the decision, the reasons and the alternatives rejected.
- [ ] `Documentation/Planning/Decisions/README.md` lists the new ADR.
- [ ] `Documentation/Product/Product-Overview.md`, "Phasing": Phase 1 lists `Authentication` and `Cookies`; Phase 2 no longer does; the row links the ADR.
- [ ] `Documentation/Planning/Roadmap.md`, "Milestone 1": the opening paragraph and "Delivers" name `Curl.Authentication` and `Curl.Cookies`, citing the ADR.

## Notes

- Record only; the decision is not reopened here.
- Plan item: D1 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
