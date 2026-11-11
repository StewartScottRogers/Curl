---
id: BL-4900
title: Make xmpp honour --ssl-reqd and never send PLAIN credentials without TLS
priority: High
assignee: Claude
pipeline: protocol
depends-on: []
touches: [Curl.Extended.Protocol.Xmpp.UnitLibrary/Client/XmppClientFlow.cs, Curl.Extended.Protocol.Xmpp.UnitTests/XmppSslRequiredTests.cs]
requirement: none
created: 2026-11-11
completed:
---
# BL-4900 — Make xmpp honour --ssl-reqd and never send PLAIN credentials without TLS

## Goal

`--ssl-reqd` on `xmpp://` fails with exit 64 when the server offers no STARTTLS, before any `<auth>` is sent.

## Context

From the outside fitness review of 2026-11-11 (commit 6ce7cfcf, the `preview` `curl-win-x64.zip`, Windows 11 x64; REF = curl 8.22.0 Schannel), finding EXT-002 (High, confirmed: reproduced at least twice).

- Repro: XMPP mock on 127.0.0.1:18663 whose `<stream:features>` offers only `<mechanisms><mechanism>PLAIN</mechanism></mechanisms>` (no `<starttls/>`) and logs everything received. `curl -sS --ssl-reqd -u alice:s3cret xmpp://127.0.0.1:18663/`
- Expected: curl's `--ssl-reqd` convention for STARTTLS protocols (which the port follows for smtp/imap/pop3/ftp; RFC 6120 section 13.8): `curl: (64) Requested SSL level failed`, exit 64, no `<auth>` sent.
- Actual: The server receives `<auth mechanism="PLAIN" ...>AGFsaWNlAHMzY3JldA==</auth>` (alice / s3cret); exit 56, or 0 if the server answers `<success/>`. A network attacker stripping `<starttls/>` gets the password.
- Where to start: `XmppClientFlow.cs` ~line 43 upgrades only "when offered"; add a TLS-required branch.

## Acceptance criteria

- [ ] The repro prints `curl: (64) Requested SSL level failed`, exits 64, and the mock receives no `<auth`.
- [ ] Without `--ssl-reqd` the existing behaviour is unchanged, and a unit test covers both paths.
- [ ] The xmpp conformance document states the `--ssl-reqd` rule.
- [ ] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Mock servers in the review were Python; build stand-ins in PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md). Measure real curl (the platform build) before pinning any output text, and add a focused unit test in the fixed library's own `.UnitTests` project.

## Log

- 2026-11-11: Created.
