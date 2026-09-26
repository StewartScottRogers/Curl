---
id: BL-220
title: Match stored cookies to a request and order the Cookie header
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-219, BL-161]
touches: [Curl.Cookies.UnitLibrary, Curl.Cookies.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-220 — Match stored cookies to a request and order the Cookie header

## Goal

A cookie store implements `ICookieStore`, matching cookies by domain, path, secure and expiry and ordering them as curl does.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item Q2. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- `ICookieStore` from BL-161.
- Where a criterion says *measured*, run curl 8.21.0 - the mingw build `/mingw64/bin/curl`, first on PATH, which is the Windows reference (ADR-0009 and the BL-153 ADR) - against a loopback server (the BL-165 recording script `Record-CurlExchange.ps1` once it exists), record the exact command and the bytes it produced in `Notes`, then pin them in a test. Never pin text that was not measured.

## Acceptance criteria

- [ ] The Cookie header order is measured on curl 8.21.0 and pinned; expiry uses the passed time.
- [ ] `dotnet build Curl.Cookies.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Cookies`.

## Notes

**Partial work from a cut-off run (2026-09-26):** local branch `factory/BL-220-wip` holds one commit of it. Start with `git cherry-pick --no-commit factory/BL-220-wip`, review it, then carry on from there rather than starting over.

- Plan item: Q2 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Backlog. Shift stopped while waiting for tokens (limit reset early); partial work saved on branch factory/BL-220-wip
- 2026-09-26: Backlog -> Doing.
