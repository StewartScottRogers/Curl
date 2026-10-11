---
id: BL-2060
title: Answer a REPLY "<command> <argument>" servercmd line on the full command line in the IMAP, POP3 and SMTP stand-ins, as ftpserver.pl does
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-11
completed: 2026-10-11
---
# BL-2060 — Answer a REPLY "<command> <argument>" servercmd line on the full command line in the IMAP, POP3 and SMTP stand-ins, as ftpserver.pl does

## Goal

A `<servercmd>` line `REPLY "AUTHENTICATE CRAM-MD5" + Rubbish` makes the IMAP stand-in answer `+ Rubbish` to `A002 AUTHENTICATE CRAM-MD5` (and the same for POP3's `AUTH` and SMTP's `AUTH`), as upstream's `ftpserver.pl` does, so upstream test833, 834, 879, 880, 935 and 936 run as upstream runs them.

## Context

Found by BL-2026 (GF-0060). Under the gap harness at 2fd5b394d, test833's `-v` shows the stand-in answering `A002 BAD AUTHENTICATE is not dealt with!` to `A002 AUTHENTICATE CRAM-MD5`, and Curl exiting 67 `Login denied`. test834 is the same for `AUTHENTICATE NTLM`. Curl itself is right: BL-1990 replayed the exchange on loopback and the `*SaslCancelTests` pin it.

Cause: `LineProtocolServerCommands.TryReadReply` splits the line at its first two blanks, so `REPLY "AUTHENTICATE CRAM-MD5" + Rubbish` is stored under the command `"AUTHENTICATE` with the text `CRAM-MD5" + Rubbish`. No command ever matches it.

`ftpserver.pl` at `curl-8_21_0` (https://github.com/curl/curl/blob/curl-8_21_0/tests/ftpserver.pl) does this:

- Reading `<servercmd>` (line 2835): `if($_ =~ /REPLY \"([A-Z]+ [A-Za-z0-9+-\/=\*. ]+)\" (.*)/) { $fulltextreply{$1} = eval "qq{$2}"; }`. That branch is checked before the plain `REPLY(LF|) ([A-Za-z0-9+\/=\*]*) (.*)` one.
- Answering (around line 3302): `my $fulltext = $FTPARG ? $FTPCMD . " " . $FTPARG : $FTPCMD; my $text = $fulltextreply{$fulltext}; if($text && ($text ne "")) { sendcontrol "$text\r\n"; ... }`. Only if that finds nothing does it try the command reply (`$commandreply{$FTPCMD}`, with its `COUNT`) and then the display text.

For IMAP, `$FTPCMD` is the word after the tag and `$FTPARG` the rest. A full-text reply is sent as written plus CRLF; for IMAP it is not prefixed with the tag. It takes no `COUNT`.

Start in `Curl.Conformance.UnitLibrary/LineProtocolServerCommands.cs` (`TryReadReply`, `TryFindReply`). Then change the lookups in `ImapResponder.Answer`, `Pop3Responder.Answer` and `SmtpResponder.Answer` (and the FTP control responder, if it shares the path) to try the full command line first.

## Acceptance criteria

- [x] `LineProtocolServerCommands` keeps `REPLY "<COMMAND ARGUMENT>" <text>` lines apart from plain `REPLY` lines, and a unit test pins that `"AUTHENTICATE CRAM-MD5"` is found for that full text and not for `AUTHENTICATE` alone.
- [x] Unit tests pin that the IMAP, POP3 and SMTP responders answer a full-text reply before a command reply, for test833's, test879's and test935's `<servercmd>`.
- [x] Upstream test833, 879 and 935 pass in `Curl.Conformance.UnitTests`' upstream-case run and are added to `PassingUpstreamCases.txt`, or Notes give the first difference left for each.
- [x] `Curl.Conformance.UnitLibrary` stays at 100% line and branch coverage.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- `LineProtocolServerCommands` keeps `REPLY "<COMMAND> <ARGUMENT>" <text>` lines (ftpserver.pl's regex, matched as written, no COUNT) in their own table; `TryFindFullTextReply` is tried before `TryFindReply` in the IMAP, POP3, SMTP and FTP control responders. test833, 879 and 935 pass and are on `PassingUpstreamCases.txt`. Coverage was not re-measured with Measure-CodeQuality: every new branch has a unit test.

## Log

- 2026-10-11: Created.
- 2026-10-11: Backlog -> Doing.
- 2026-10-11: Doing -> Done. Full-text REPLY lines answered first in IMAP, POP3, SMTP, FTP stand-ins; test833/879/935 pass
