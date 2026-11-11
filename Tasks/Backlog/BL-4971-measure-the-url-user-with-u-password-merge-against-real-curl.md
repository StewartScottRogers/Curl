---
id: BL-4971
title: Measure the URL user with -u password merge against real curl and hold the quality gates
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4970]
touches: [Curl.Engine.UnitLibrary/TransferCredentialLookup.cs, Curl.Engine.UnitLibrary/CLAUDE.md, Curl.Engine.UnitLibrary/TransferContextFactory.cs, Curl.Engine.UnitTests/UrlUserWithOptionPasswordTests.cs]
requirement: none
created: 2026-11-11
completed:
---
# BL-4971 — Measure the URL user with -u password merge against real curl and hold the quality gates

## Goal

`http://user@host -u :secret` and the IMAP form authenticate as user:secret, checked with the real Curl.Console, and the engine library stays inside its quality gates.

## Context

Split from BL-4915; needs BL-4970's fix landed. Measure with a PowerShell or C# file-based stand-in server, never Python.

## Acceptance criteria

- [ ] `curl -sS http://user@127.0.0.1:18500/ -u :secret` sends `Authorization: Basic dXNlcjpzZWNyZXQ=`.
- [ ] `curl -sS "imap://user@127.0.0.1:18500/INBOX/;UID=1" -u :secret` makes the IMAP stand-in log `A002 LOGIN user secret`.
- [ ] `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary` reports no member outside a gate.

## Notes

Split from BL-4915 (over its cost cap).

## Log

- 2026-11-11: Created.
