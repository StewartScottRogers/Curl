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
completed: 2026-10-30
---
# BL-4315 — Extension: Privacy Pass token type 1 (VOPRF P-384)

## Goal

`--privacy-pass` also answers a PrivateToken challenge of token type 0x0001 (privately verifiable, VOPRF(P-384, SHA-384), RFC 9578 §5, RFC 9497), with P-384 and hash-to-curve built by hand in the extension. See ADR-1110.

<!-- One sentence: the observable outcome once this task is done. -->

## Context

<!-- Why this matters, and where to start: requirement IDs, ADRs, projects, files, upstream curl links with the curl version. -->

## Acceptance criteria

- [x] <!-- A statement someone else can check from the repository without asking a question. -->

## Notes

- 2026-10-29 (lane 8, first pass, no code edited): the hook that picks which challenge to answer, `Options/PrivacyPassTransferHook.cs` (`AnswerableChallenge` compares with `PrivacyPassIssuance.BlindRsaTokenType`), is outside `touches` and BL-4316 (in Doing) holds `Curl.Extension.PrivacyPass.UnitLibrary/Options`, so the hook file and its tests were added to `touches` and BL-4316 to `depends-on`. Split for size: the hand-built P-384, hash-to-curve and VOPRF client, all in `Tokens/`, is BL-4321 (no dependencies, so it can start beside BL-4316); this task keeps the wiring of type `0x0001` into the hook, its tests and the ADR-1110 update once both are Done.

- 2026-10-30 (lane 8): BL-4321 had already landed the P-384 VOPRF client and `PrivacyPassIssuance` already dispatched type 1, so this run only widened `PrivacyPassTransferHook.AnswerableChallenge` to types `0x0001` and `0x0002`. When a 401 offers both, the hook answers the first of them in field order and skips any other type (the simplest rule; real curl has no Privacy Pass, so there is nothing to match). Added hook tests for a type 1 challenge answered with the issuer's PRF output, a proof that fails to verify (exit 8, no resend) and an unknown type listed before a type 1, and updated ADR-1110. The acceptance criterion is still the template placeholder from filing; it was ticked against the Goal: dotnet build clean, fast tests green (PrivacyPass 124 passed), Measure-CodeQuality for Curl.Extension.PrivacyPass.UnitLibrary 0 failing members. The tests are pure computation, so they are platform-neutral.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: fbecace0.
- 2026-10-29: Doing -> Backlog. Waits for BL-4316 (holds Curl.Extension.PrivacyPass.UnitLibrary/Options, where PrivacyPassTransferHook picks the token type) and BL-4321 (the P-384 VOPRF client split out of this task)
- 2026-10-30: Backlog -> Doing. Criteria at claim: fbecace0.
- 2026-10-30: Doing -> Done. --privacy-pass answers PrivateToken challenges of token type 0x0001 (VOPRF P-384) as well as 0x0002
