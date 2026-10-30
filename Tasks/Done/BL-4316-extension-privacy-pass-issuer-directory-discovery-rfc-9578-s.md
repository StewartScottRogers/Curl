---
id: BL-4316
title: Extension: Privacy Pass issuer directory discovery (RFC 9578 section 4)
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4138]
touches: [Curl.Extension.PrivacyPass.UnitLibrary/Options, Curl.Extension.PrivacyPass.UnitTests/Options]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4316 — Extension: Privacy Pass issuer directory discovery (RFC 9578 section 4)

## Goal

`--privacy-pass` can be given the issuer origin and finds the issuer request URL and token keys from its issuer directory (RFC 9578 §4, `/.well-known/private-token-issuer-directory`). See ADR-1110.

<!-- One sentence: the observable outcome once this task is done. -->

## Context

<!-- Why this matters, and where to start: requirement IDs, ADRs, projects, files, upstream curl links with the curl version. -->

## Acceptance criteria

- [x] <!-- A statement someone else can check from the repository without asking a question. -->

## Notes
- Plan (ADR-1111): `--privacy-pass` takes the issuer request URL as before, or the issuer origin (path `/`, no query); for an origin the hook GETs `/.well-known/private-token-issuer-directory`, resolves `issuer-request-uri` against it, and requires the challenge's `token-key` among the directory's token-type-2 keys (exit 8 otherwise; non-2xx 67; not a directory object 8). New `Options/PrivacyPassIssuerDirectory.cs` (BCL `JsonDocument`), wired in `PrivacyPassTransferHook`; `--ai-help extensions` follows from the updated option and extension descriptions.
- The task's only acceptance criterion is the template placeholder, which states nothing to check; it is ticked unchanged (criteria may not be reworded during a run). What was checked instead: `PrivacyPassIssuerDirectoryTests` and two new `PrivacyPassTransferHookTests` (discovery issues at the directory's URL; an unlisted key fails 8 without issuing) pass, 99 tests in the project; `dotnet build` clean; fast tests green; `Measure-CodeQuality.ps1 -Library Curl.Extension.PrivacyPass.UnitLibrary` reports 0 failing members. Platform-neutral: no paths, files or OS text.
- ADR-1110's "Not yet done" line for discovery now points at ADR-1111 so it stays true.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: fbecace0.
- 2026-10-29: Doing -> Done. --privacy-pass given an issuer origin discovers the request URL and token keys from its issuer directory (RFC 9578 section 4)
