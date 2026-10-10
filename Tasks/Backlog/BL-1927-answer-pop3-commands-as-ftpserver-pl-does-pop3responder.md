---
id: BL-1927
title: Answer POP3 commands as ftpserver.pl does (Pop3Responder)
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-1895]
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-09
completed:
---
# BL-1927 — Answer POP3 commands as ftpserver.pl does (Pop3Responder)

## Goal

A `Pop3Responder : ILineProtocolResponder` in Curl.Conformance.UnitLibrary answers POP3 command lines exactly as the pop3 parts of upstream's `tests/ftpserver.pl` do, so BL-1911 only has to wire it into the case runner.

## Context

The POP3 half of BL-1911, split off the way BL-1925 (`SmtpResponder`) and BL-1926 (`ImapResponder`) were split off BL-1909 and BL-1910: the responder first, the runner wiring after. Model it on `ImapResponder.cs` and `SmtpResponder.cs`, and reuse `LineProtocolServerCommands` (`TryFindReply`, `Capabilities`, `AuthenticationMechanisms`), `LineProtocolReplyData.Select` for `getreplydata`, and the case's `<reply>` parts as a dictionary. Read Curl.Conformance.UnitLibrary\CLAUDE.md first. Behaviour from the pop3 parts of tests/ftpserver.pl in the curl 8.21.0 tarball (https://curl.se/download/curl-8.21.0.tar.xz, extracted into an empty scratch folder, never into the repository): the `+OK` greeting, CAPA (with the servercmd `CAPA` list), USER/PASS, APOP, AUTH exchanges (`+` continuations, `AUTH` mechanism list), STAT, LIST (with and without a message number), RETR and TOP (the `<reply>` data part as a multi-line reply ending in a lone `.`, dot-stuffed as ftpserver.pl sends it), UIDL, DELE, NOOP, RSET and QUIT, and `-ERR` for anything else. The cases are vendored in Curl.Conformance.UnitTests\UpstreamTestData (test850-test899 and others naming `pop3`).

## Acceptance criteria

- [ ] `Pop3Responder` answers every POP3 command ftpserver.pl handles, with the same reply bytes, and a `REPLY` servercmd overrides a command's answer as in ftpserver.pl.
- [ ] Curl.Conformance.UnitTests covers every line and branch of `Pop3Responder`; no method exceeds complexity 10; no test carries TestCategory=Integration and every test is platform-neutral.
- [ ] `dotnet build Curl.Conformance.UnitLibrary -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green.

## Notes

## Log

- 2026-10-09: Created.
