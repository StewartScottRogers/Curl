---
id: BL-876
title: Pass the connection's port into SaslRequest from the SMTP, IMAP and POP3 handlers
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-751]
touches: [Curl.Protocol.Smtp.UnitLibrary, Curl.Protocol.Smtp.UnitTests, Curl.Protocol.Imap.UnitLibrary, Curl.Protocol.Imap.UnitTests, Curl.Protocol.Pop3.UnitLibrary, Curl.Protocol.Pop3.UnitTests]
requirement: none
created: 2026-09-29
completed: 2026-09-29
---
# BL-876 — Pass the connection's port into SaslRequest from the SMTP, IMAP and POP3 handlers

## Goal

`smtp://`, `imap://` and `pop3://` transfers that authenticate with OAUTHBEARER send `port=<p>` in the initial response, as curl 8.21.0 does, because each handler fills `SaslRequest.Port`.

## Context

- BL-751 added `SaslRequest.Port` (default `0`, which leaves `port=` out) and made `SaslAuthenticator` send it. ADR-0123 point 6.
- The requests are built in `Curl.Protocol.Smtp.UnitLibrary/SmtpSaslAuthentication.cs` (`CreateRequest`), `Curl.Protocol.Imap.UnitLibrary/ImapAuthentication.cs` and `Curl.Protocol.Pop3.UnitLibrary/Pop3Login.cs`; none passes a port yet.
- The port is the URL's, or the scheme's default when the URL names none (25/465, 143/993, 110/995). Measured: `port=18125` against `smtp://127.0.0.1:18125/x`.

## Acceptance criteria

- [x] Each of the three handlers passes the connection's port into `SaslRequest`, pinned by a test per handler for an explicit port and for the scheme's default.
- [x] `Pop3ProtocolHandlerLoginTests` expects the port on the recorded `SaslRequest`.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage for the three libraries.

## Notes

- Each handler passes `context.Url.Port`, which `CurlUrl` already fills with the scheme's default when the URL names none, so no default-port table was needed in the handlers. The port is the URL's even through a proxy or `--connect-to`, as curl's `conn->remote_port` is.
- Delivered directly rather than through the full `/feature` agent stages: a three-line change in three handlers plus one test per handler; no design question arose, so no ADR.
- Tests: `SmtpProtocolHandlerAuthenticationTests.ExecuteAsync_Request_CarriesTheConnectionsPort`, `ImapProtocolHandlerAuthenticationTests.ExecuteAsync_SaslRequest_CarriesTheConnectionsPort`, `Pop3ProtocolHandlerLoginTests.ExecuteAsync_SaslRequest_CarriesTheConnectionsPort` (explicit port and scheme default each); `ExecuteAsync_SaslRequest_CarriesTheTransfersCredentialsAndOptions` now expects port 18110.
- Lane 6 re-applied lane 2's commit (ea3b04f3) onto the current branch: it applied cleanly, the build is clean and every fast test passes, so lane 2's integration failure came from other lanes' work at the time, not from this change.
- `Measure-CodeQuality.ps1 -Library` must be run once per library under `powershell -File`: a comma list arrives as one string and matches nothing.

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Backlog. Lane 2 could not integrate: fast tests failed after rebasing onto the other lanes' work. The work is on branch factory/BL-876-lane-2-20260929-023709; start with git cherry-pick --no-commit factory/BL-876-lane-2-20260929-023709 and fix it.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. SMTP, IMAP and POP3 pass the connection's port into SaslRequest, so OAUTHBEARER sends port=<p> as curl does
