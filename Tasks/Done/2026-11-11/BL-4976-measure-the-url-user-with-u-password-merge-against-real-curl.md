---
id: BL-4976
title: Measure the URL user with -u password merge against real curl and hold the quality gates
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4975]
touches: [Curl.Engine.UnitLibrary/TransferCredentialLookup.cs, Curl.Engine.UnitLibrary/CLAUDE.md, Curl.Engine.UnitLibrary/TransferContextFactory.cs, Curl.Engine.UnitTests/UrlUserWithOptionPasswordTests.cs]
requirement: none
created: 2026-11-11
completed: 2026-11-11
---
# BL-4976 — Measure the URL user with -u password merge against real curl and hold the quality gates

## Goal

`http://user@host -u :secret` and the IMAP form authenticate as user:secret, checked with the real Curl.Console, and the engine library stays inside its quality gates.

## Context

Split from BL-4915; needs BL-4975's fix landed. Measure with a PowerShell or C# file-based stand-in server, never Python.

## Acceptance criteria

- [x] `curl -sS http://user@127.0.0.1:18500/ -u :secret` sends `Authorization: Basic dXNlcjpzZWNyZXQ=`.
- [x] `curl -sS "imap://user@127.0.0.1:18500/INBOX/;UID=1" -u :secret` makes the IMAP stand-in log `A002 LOGIN user secret`.
- [x] `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary` reports no member outside a gate.

## Notes

Split from BL-4915 (over its cost cap).

Measured with Record-CurlExchange.ps1 against the built Curl.Console: HTTP sent `Authorization: Basic dXNlcjpzZWNyZXQ=`. IMAP with the default stand-in (advertises AUTH=PLAIN) sent AUTHENTICATE PLAIN of `Split from BL-4915 (over its cost cap).userSplit from BL-4915 (over its cost cap).secret`; with CAPABILITY overridden to omit AUTH it logged `A002 LOGIN user secret`. Quality run: 0 failing members. No code change needed (BL-4975 held the fix).

## Log

- 2026-11-11: Created.
- 2026-11-11: Backlog -> Doing. Criteria at claim: 1a064b66 325bbd17 2009a370.
- 2026-11-11: Doing -> Done. Measured: HTTP and IMAP authenticate as user:secret; quality gates clean
