---
id: BL-5783
title: Make the read-callback no-scheme perform test assert the callback is read exactly once
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Engine.UnitTests/ManagedEasyTransferTests.Mqtt.cs]
requirement: none
created: 2026-11-22
completed:
---
# BL-5783 — Make the read-callback no-scheme perform test assert the callback is read exactly once

## Goal

`PerformAsync_ReadCallbackPostToAUrlWithNoScheme_PostsTheWholeBodyReadingTheCallbackOnce` checks what its name claims: that the read callback's body is read once, not once up front and again while sending.

## Context

Found by the shift-end review of 2026-11-22 (ADR-0710; code-reviewer).

- `Curl.Engine.UnitTests/ManagedEasyTransferTests.Mqtt.cs:242-260`, added by this shift for BL-5765, ends with `Assert.IsTrue(calls >= 1)`. A regression that read the callback up front (treating `localhost:8080/x` as an extension URL) and then again while sending would still pass, which is exactly the bug the test is there to catch.
- CLAUDE.md's "say what it does, do what it says" applies to test names; audit-quality flags tests whose names claim more than they assert.
- Count the calls that return data (the reader's final zero-byte read may add one) and pin the exact number, or rename the test to what it checks.

## Acceptance criteria

- [ ] The test asserts an exact callback count (or an exact count of data-returning reads) that a read-up-front-and-again regression would fail, or its name no longer says "once".
- [ ] The task's Log records the exact count the test now pins and why that is the number curl's read pattern gives.
- [ ] `dotnet build` is clean and the fast tests are green.

## Notes

## Log

- 2026-11-22: Created.
- 2026-11-22: Backlog -> Doing. Criteria at claim: 638a1d3c 1b7de58a e4d345af.
