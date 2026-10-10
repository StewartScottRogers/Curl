---
id: BL-1926
title: Answer IMAP commands as ftpserver.pl does (ImapResponder)
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-1895]
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-09
completed:
---
# BL-1926 — Answer IMAP commands as ftpserver.pl does (ImapResponder)

## Goal

An `ImapResponder : ILineProtocolResponder` in Curl.Conformance.UnitLibrary answers tagged IMAP command lines exactly as the imap parts of upstream's `tests/ftpserver.pl` do, so BL-1910 only has to wire it into the case runner.

## Context

The IMAP half of BL-1910, split off the way BL-1925 (`SmtpResponder`) was split off BL-1909: the responder first, the runner wiring after. Model it on `SmtpResponder.cs` and reuse `LineProtocolServerCommands` (`TryFindReply`, `Capabilities`, `AuthenticationMechanisms`) and the case's `<reply>` parts as a dictionary. Read Curl.Conformance.UnitLibrary\CLAUDE.md first. Behaviour from the imap parts of tests/ftpserver.pl in the curl 8.21.0 tarball (https://curl.se/download/curl-8.21.0.tar.xz, extracted into an empty scratch folder, never into the repository): the `* OK` greeting, tagged commands and `<tag> OK`/`BAD`/`NO` replies, CAPABILITY, LOGIN, AUTHENTICATE exchanges, SELECT/EXAMINE, FETCH/UID FETCH (the `<reply>` data part as a literal `{n}`), LIST/LSUB, STATUS, SEARCH, STORE, COPY, CREATE/DELETE/RENAME, NOOP, CHECK, CLOSE, EXPUNGE, IDLE, LOGOUT, and APPEND with its `{n}` literal and `+` continuation, recording the uploaded literal for `<verify><upload>`. The cases are vendored in Curl.Conformance.UnitTests\UpstreamTestData (test800-test899 and others naming `imap`).

## Acceptance criteria

- [ ] `ImapResponder` answers every IMAP command ftpserver.pl handles, with the same reply bytes, and a `REPLY` servercmd overrides a command's answer as in ftpserver.pl.
- [ ] APPEND reads its `{n}` literal and keeps the uploaded bytes as ftpserver.pl stores them.
- [ ] Curl.Conformance.UnitTests covers every line and branch of `ImapResponder`; no method exceeds complexity 10; no test carries TestCategory=Integration and every test is platform-neutral.
- [ ] `dotnet build Curl.Conformance.UnitLibrary -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green.

## Notes

## Log

- 2026-10-09: Created.
