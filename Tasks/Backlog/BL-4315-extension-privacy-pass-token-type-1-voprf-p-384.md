---
id: BL-4315
title: Extension: Privacy Pass token type 1 (VOPRF P-384)
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4138, BL-4316, BL-4321]
touches: [Curl.Extension.PrivacyPass.UnitLibrary/Tokens, Curl.Extension.PrivacyPass.UnitTests/Tokens, Curl.Extension.PrivacyPass.UnitLibrary/Options/PrivacyPassTransferHook.cs, Curl.Extension.PrivacyPass.UnitTests/Options/PrivacyPassTransferHookTests.cs]
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

- 2026-10-29 (lane 8, first pass, no code edited): the hook that picks which challenge to answer, `Options/PrivacyPassTransferHook.cs` (`AnswerableChallenge` compares with `PrivacyPassIssuance.BlindRsaTokenType`), is outside `touches` and BL-4316 (in Doing) holds `Curl.Extension.PrivacyPass.UnitLibrary/Options`, so the hook file and its tests were added to `touches` and BL-4316 to `depends-on`. Split for size: the hand-built P-384, hash-to-curve and VOPRF client, all in `Tokens/`, is BL-4321 (no dependencies, so it can start beside BL-4316); this task keeps the wiring of type `0x0001` into the hook, its tests and the ADR-1110 update once both are Done.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: fbecace0.
- 2026-10-29: Doing -> Backlog. Waits for BL-4316 (holds Curl.Extension.PrivacyPass.UnitLibrary/Options, where PrivacyPassTransferHook picks the token type) and BL-4321 (the P-384 VOPRF client split out of this task)
