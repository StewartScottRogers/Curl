---
id: AF-0005
title: Retry delay ignores Retry-After
auditor: conformance
severity: High
status: blocked
reason: Waits on the HTTP retry rework.
---
# AF-0005 — Retry delay ignores Retry-After

## Evidence

Fixture text for the board page's findings tab. A long unbroken token to prove wrapping: aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa

## Reproduction

    curl --fixture

## Log

- 2026-09-30: Raised by the conformance auditor.
- 2026-10-01: proposed -> blocked. Waits on the HTTP retry rework.
