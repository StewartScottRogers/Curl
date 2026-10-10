---
id: BL-1985
title: Close GF-0055: A reply pipelined behind STARTTLS/AUTH TLS is not refused with exit 8, and a pre-authenticated FTP server skips AUTH under --ssl-reqd
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Smtp.UnitLibrary, Curl.Protocol.Smtp.UnitTests, Curl.Protocol.Pop3.UnitLibrary, Curl.Protocol.Pop3.UnitTests, Curl.Protocol.Ftp.UnitLibrary, Curl.Protocol.Ftp.UnitTests, Curl.Console.UnitTests]
requirement: none
created: 2026-10-10
completed:
---
# BL-1985 — Close GF-0055: A reply pipelined behind STARTTLS/AUTH TLS is not refused with exit 8, and a pre-authenticated FTP server skips AUTH under --ssl-reqd

## Goal

Curl behaves as curl 8.21.0 does for every item of gap finding GF-0055 (A reply pipelined behind STARTTLS/AUTH TLS is not refused with exit 8, and a pre-authenticated FTP server skips AUTH under --ssl-reqd), so a later gap analysis measures each of `behaviour:test980`, `behaviour:test982`, `behaviour:test983`, `behaviour:test986` as `match`.

## Context

- Finding: GF-0055, filed by the gap analysis office (ADR-0433).
- Area: behaviour. Severity: High. Introduced in: not stated upstream.
- Items: `behaviour:test980`, `behaviour:test982`, `behaviour:test983`, `behaviour:test986`.
- Targeted curl version: 8.21.0 (Gap/Baselines/target.json).
- Yardstick: `tests/data/test*` in the curl 8.21.0 release tarball.
- The gap closes only when a later gap analysis re-measures every item as `match` (ADR-0433 decision 5), never because this task reaches Done.

Evidence, copied from the finding:

Every item expects 'upstream test<N> passes'. test980 (SMTP, STARTTLS answered '454' with more replies pipelined): '<verify><protocol> differs at byte 20 (line 3): expected the end, got "AUTH PLAIN AHVzZXIAc2VjcmV0\r\n"'; upstream expects exit 8. test983 (FTP, AUTH answered with pipelined lines): expected the end, got 'AUTH TLS'; upstream expects exit 8. test982 (POP3 STARTTLS pipelined): 'curl did not finish within 20 seconds'. test986 (welcome '230', --ssl-reqd): expected 'AUTH SSL', got 'PWD'; upstream expects exit 64. Reproduce: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "$env:LOCALAPPDATA/Curl/gap/upstream/8.21.0/tests/data" C:/Temp/gap/raw.json 980,982,983,986

Suggestion, copied from the finding:

In Curl.Protocol.Smtp.UnitLibrary, Curl.Protocol.Pop3.UnitLibrary and Curl.Protocol.Ftp.UnitLibrary, after sending STARTTLS/STLS/AUTH, fail with exit 8 when more bytes are already buffered behind its reply (a pipelined server response), as curl 8.21.0 does, and send nothing more. In FtpSession, when the server greets with 230 (pre-authenticated) and --ssl-reqd is set, still send AUTH SSL / AUTH TLS and fail with exit 64 when both are refused.

## Acceptance criteria

- [ ] `behaviour:test980`: Curl answers what curl 8.21.0 answers, `upstream test980 passes`, so the item measures `match`.
- [ ] `behaviour:test982`: Curl answers what curl 8.21.0 answers, `upstream test982 passes`, so the item measures `match`.
- [ ] `behaviour:test983`: Curl answers what curl 8.21.0 answers, `upstream test983 passes`, so the item measures `match`.
- [ ] `behaviour:test986`: Curl answers what curl 8.21.0 answers, `upstream test986 passes`, so the item measures `match`.
- [ ] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [ ] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md).

## Notes

- 2026-10-10 (lane 5): Implemented, uncommitted (the shift stashes it). curl 8.21.0 checks
  `pp.overflow` first in `smtp_state_starttls_resp`, `pop3_state_starttls_resp` and
  `ftp_state_auth_resp` and returns CURLE_WEIRD_SERVER_REPLY ("Weird server reply", exit 8)
  whatever the code; and in FTP_WAIT220 a 230 greeting only means logged-in when
  `use_ssl <= CURLUSESSL_TRY` or the control is already TLS. So:
  - `SmtpControlChannel`/`Pop3ControlChannel`/`FtpControlChannel.HasBufferedBytes`
    (`bufferStart < bufferEnd`); `SmtpSession.StartTlsAsync`, `Pop3Session.StartTlsAsync`
    and `FtpSession.SecureControlAsync` fail exit 8 when it is true after the reply.
  - `FtpSession.GreetAndLogInAsync`: 230 under `--ssl-reqd`/`--ftp-ssl-control` on a
    plaintext control goes through `SecureControlAsync` like 220 (test986: AUTH SSL,
    AUTH TLS, exit 64).
  - Tests: new pipelined cases in `SmtpProtocolHandlerSessionTests`,
    `Pop3ProtocolHandlerSessionTests`, `FtpProtocolHandlerTlsTests` (also 230 under
    `--ssl-reqd` and `--ssl`). Existing tests that scripted a reply to a later command in
    the same read as the STARTTLS/STLS/AUTH reply now split reads (FTP:
    `ScriptedConnection.FromReplies` with a `NextRead` mark, used by `FtpRun`).
    Smtp, Pop3 and Ftp unit tests green; build -warnaserror clean.
- What is left: `Curl.Console.UnitTests` `CurlCommandRunnerSmtpTransferEventTests`
  `RunAsync_VerboseMailUploadWithStartTlsOnWindows_...` fails (exit 8): its `RunAsync`
  helper (line ~269) scripts `"220 Ready to start TLS\r\n" + SecureEhloReply + Transaction`
  as one read. Fix: give `ScriptedConnector` two reads split after the 220 line (e.g.
  split `replies` on a marker). That project is held by BL-1987 (in Doing), so
  `Curl.Console.UnitTests` was added to `touches` and the task went back to Backlog.
- Could not rerun Measure-UpstreamCases.cs here: the audit guard refuses lanes any `Gap/`
  path, the upstream test data's included. Behaviour matches curl's source as above.

## Log

- 2026-10-10: Created.
- 2026-10-10: Backlog -> Doing.
- 2026-10-10: Doing -> Backlog. Needs Curl.Console.UnitTests (one SMTP STARTTLS test helper splits its reads), held by BL-1987 in Doing; code is done in the stash
- 2026-10-10: Backlog -> Doing.
