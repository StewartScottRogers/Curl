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
completed:
---
# BL-4316 — Extension: Privacy Pass issuer directory discovery (RFC 9578 section 4)

## Goal

`--privacy-pass` can be given the issuer origin and finds the issuer request URL and token keys from its issuer directory (RFC 9578 §4, `/.well-known/private-token-issuer-directory`). See ADR-1095.

<!-- One sentence: the observable outcome once this task is done. -->

## Context

<!-- Why this matters, and where to start: requirement IDs, ADRs, projects, files, upstream curl links with the curl version. -->

## Acceptance criteria

- [ ] <!-- A statement someone else can check from the repository without asking a question. -->

## Notes

## Log

- 2026-10-29: Created.
