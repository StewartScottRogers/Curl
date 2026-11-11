---
id: BL-4935
title: Send IMAP LOGOUT after -I on a message download even when the reply is fully buffered
priority: Low
assignee: Claude
pipeline: protocol
depends-on: []
touches: [Curl.Original.Protocol.Imap.UnitLibrary/ImapSession.cs, Curl.Original.Protocol.Imap.UnitTests/ImapHeadLogoutTests.cs, Curl.Original.Protocol.Imap.UnitTests/Fakes/ScriptedConnection.cs]
requirement: none
created: 2026-11-11
completed:
---
# BL-4935 — Send IMAP LOGOUT after -I on a message download even when the reply is fully buffered

## Goal

`-I` on an IMAP `;UID=` fetch ends with `A005 LOGOUT`, as in curl.

## Context

From the outside fitness review of 2026-11-11 (commit 6ce7cfcf, the `preview` `curl-win-x64.zip`, Windows 11 x64; REF = curl 8.22.0 Schannel), finding STD-025 (Low, confirmed: reproduced at least twice).

- Repro: IMAP mock on 18500 that sends the whole FETCH response (literal plus tagged OK) in one write and logs client lines. `curl -sS "imap://127.0.0.1:18500/INBOX/;UID=1" -u u:p -I`
- Expected: REF 8.22.0: exit 8, `curl: (8) Weird server reply`; server receives A001 CAPABILITY, A002 LOGIN, A003 SELECT INBOX, A004 UID FETCH 1 BODY[], A005 LOGOUT.
- Actual: Same exit and stderr, but no `A005 LOGOUT`.
- Where to start: `ImapSession.DownloadAsync`: `failure.ExitCode == WeirdServerReply && !channel.HasUnreadBytes` skips `LogoutIfBodyRefusedAsync`.

## Acceptance criteria

- [x] Same exit 8 and stderr, and the mock log ends with `A005 LOGOUT`.
- [x] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Mock servers in the review were Python; build stand-ins in PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md). Measure real curl (the platform build) before pinning any output text, and add a focused unit test in the fixed library's own `.UnitTests` project.

Decision (Claude): measured curl 8.22.0 against a loopback PowerShell mock: LOGOUT is sent whenever the server is still open, even with the whole response in one write; a server that hung up got none (matches the earlier recorder run). The rule is now `!Connection.HasPeerClosed` instead of `!HasUnreadBytes`. ScriptedConnection (test fake) gained PeerStaysOpen/HasPeerClosed, added to touches; old tests stay valid as the exhausted script reads as hung up.

## Log

- 2026-11-11: Created.
- 2026-11-11: Backlog -> Doing. Criteria at claim: 4cabf188 a0b49a29.
- 2026-11-11: Done; build clean, Imap tests and quality run green.
