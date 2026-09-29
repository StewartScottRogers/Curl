---
id: BL-875
title: Pass the connection's port into SaslRequest from the SMTP, IMAP and POP3 handlers
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-751]
touches: [Curl.Protocol.Smtp.UnitLibrary, Curl.Protocol.Smtp.UnitTests, Curl.Protocol.Imap.UnitLibrary, Curl.Protocol.Imap.UnitTests, Curl.Protocol.Pop3.UnitLibrary, Curl.Protocol.Pop3.UnitTests]
requirement: none
created: 2026-09-29
completed:
---
# BL-875 — Pass the connection's port into SaslRequest from the SMTP, IMAP and POP3 handlers

## Goal

`smtp://`, `imap://` and `pop3://` transfers that authenticate with OAUTHBEARER send `port=<p>` in the initial response, as curl 8.21.0 does, because each handler fills `SaslRequest.Port`.

## Context

- BL-751 added `SaslRequest.Port` (default `0`, which leaves `port=` out) and made `SaslAuthenticator` send it. ADR-0123 point 6.
- The requests are built in `Curl.Protocol.Smtp.UnitLibrary/SmtpSaslAuthentication.cs` (`CreateRequest`), `Curl.Protocol.Imap.UnitLibrary/ImapAuthentication.cs` and `Curl.Protocol.Pop3.UnitLibrary/Pop3Login.cs`; none passes a port yet.
- The port is the URL's, or the scheme's default when the URL names none (25/465, 143/993, 110/995). Measured: `port=18125` against `smtp://127.0.0.1:18125/x`.

## Acceptance criteria

- [ ] Each of the three handlers passes the connection's port into `SaslRequest`, pinned by a test per handler for an explicit port and for the scheme's default.
- [ ] `Pop3ProtocolHandlerLoginTests` expects the port on the recorded `SaslRequest`.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage for the three libraries.

## Notes

## Log

- 2026-09-29: Created.
