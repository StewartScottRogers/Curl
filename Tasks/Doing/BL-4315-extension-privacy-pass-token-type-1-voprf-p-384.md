---
id: BL-4315
title: Extension: Privacy Pass token type 1 (VOPRF P-384)
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4138]
touches: [Curl.Extension.PrivacyPass.UnitLibrary/Tokens, Curl.Extension.PrivacyPass.UnitTests/Tokens]
requirement: none
created: 2026-10-29
completed:
---
# BL-4315 — Extension: Privacy Pass token type 1 (VOPRF P-384)

## Goal

`--privacy-pass` also answers a PrivateToken challenge of token type 0x0001 (privately verifiable, VOPRF(P-384, SHA-384), RFC 9578 §5, RFC 9497), with P-384 and hash-to-curve built by hand in the extension. See ADR-1110.

<!-- One sentence: the observable outcome once this task is done. -->

## Context

<!-- Why this matters, and where to start: requirement IDs, ADRs, projects, files, upstream curl links with the curl version. -->

## Acceptance criteria

- [ ] <!-- A statement someone else can check from the repository without asking a question. -->

## Notes

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: fbecace0.
