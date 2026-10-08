---
id: BL-1517
title: Attack Curl.Protocol.Smtp.UnitLibrary with adversarial black-box tests in Curl.Protocol.Smtp.UnitTests
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1491, BL-1483]
touches: [Curl.Protocol.Smtp.UnitTests]
requirement: none
created: 2026-10-06
completed: 2026-10-07
---
# BL-1517 — Attack Curl.Protocol.Smtp.UnitLibrary with adversarial black-box tests in Curl.Protocol.Smtp.UnitTests

## Goal

`Curl.Protocol.Smtp.UnitTests` gains adversarial black-box tests that attack `Curl.Protocol.Smtp.UnitLibrary`'s public surface at its boundaries, with malformed input, in its invalid input partitions and, where it holds state, under repeated and interleaved calls, by the method in `Documentation/Wiki/Adversarial-Testing.md`; every defect they find is filed as its own task.

## Context

- Stewart's request, 2026-10-06: aggressive black-box testing for every test project, applied only after every other open task that changes that project's tests or library is done. This task depends on each of those open on 2026-10-06 (Backlog, Doing and Blocked; Deferred left out), so it attacks the suite in its final shape: BL-1483.
- The method, rules and oracle are in `Documentation/Wiki/Adversarial-Testing.md` (BL-1491). Black box means `Curl.Protocol.Smtp.UnitLibrary`'s public types and injected fakes only; where the behaviour is visible on the command line the expected answer is real curl's, measured with `Record-CurlExchange.ps1`.
- Where to attack, applying only the families that fit: body dot-stuffing (lines that start with `.`, a lone `.`, no final newline), multi-line `250-` replies, `--mail-from` and `--mail-rcpt` carrying CR or LF, lines past 998 octets, AUTH exchanges with malformed continuations, and replies with out-of-range codes.
- `touches` is the test project only. A defect is never fixed here: it becomes a follow-up task, and its test lands with the fix.

## Acceptance criteria

- [x] Notes list each of the four attack families (boundaries, malformed input, invalid partitions, state and concurrency) as applied, with the new test names, or as not applicable, with one line saying why.
- [x] Every new test is in `Curl.Protocol.Smtp.UnitTests`, uses only MSTest and the base class library, touches no real network, and passes on Windows, Linux and macOS (no drive-letter paths or Windows-only text outside an `[OSCondition]` test); tests on inputs over 1 MiB are in `TestCategory("Integration")`.
- [x] Every attack that exposed a defect is filed as a follow-up task (High for a crash, hang, unbounded memory or security issue) whose ID is in Notes; no failing, ignored or inconclusive test is committed for it.
- [x] `dotnet build Curl.Protocol.Smtp.UnitTests -warnaserror` is clean and `dotnet test Curl.Protocol.Smtp.UnitTests --filter "TestCategory!=Integration"` passes, and the project's test count is higher than before the task (before and after recorded in Notes).
- [x] The task's commits change only files under `Curl.Protocol.Smtp.UnitTests/` and this task file.

## Notes

All new tests are in `Curl.Protocol.Smtp.UnitTests/SmtpProtocolHandlerAdversarialTests.cs`, driving
`SmtpProtocolHandler` through the existing `ScriptedConnection` / `QueuedConnector` fakes; no input
exceeds 1 MiB (largest is a 70000-octet body line), so none is Integration.

- **Boundaries:** `ExecuteAsync_BodyLineAtAndPastTheRfcLimit_IsSentUnbrokenAsCurlDoes` (998, 999,
  70000 octets, each line dot-stuffed), `ExecuteAsync_MailReplyLineOneByteUnderTheLimit_IsAccepted`
  (65535 bytes), `ExecuteAsync_MailReplyLineAtTheLimit_IsExit100` (65536 bytes),
  `ExecuteAsync_EhloWithAThousandContinuationLines_IsReadAsOneReply`,
  `ExecuteAsync_TwoHundredRecipients_SendsOneRcptEachInOrder`.
- **Malformed input:** `ExecuteAsync_MailFromCarryingCrlf_SendsItRawAndReadsTheNextReplyForRcptAsCurlDoes`,
  `ExecuteAsync_MailRcptCarryingCrlf_SendsItRawAndReadsTheNextReplyForDataAsCurlDoes`,
  `ExecuteAsync_LineWithoutAReplyCodeBeforeTheReply_IsSkipped` (no code, two digits, a letter in the
  code, an empty line, a fourth digit). Malformed AUTH continuations were already attacked by
  `SmtpProtocolHandlerAuthenticationTests` (an unanswerable `334 `, `334 not*base64`, an oversized
  challenge) and `SmtpProtocolHandlerSaslCancelTests`, so no duplicate was added.
- **Invalid partitions:** `ExecuteAsync_MailAnsweredWithOutOfRangeCode_IsExit55AsCurlDoes` (199, 999, 600).
- **State and concurrency:** `ExecuteAsync_UploadReadOneByteAtATime_SendsTheSameBytesAsReadWhole`
  (dot-stuffing state carried across one-byte chunks, 5 bodies),
  `ExecuteAsync_EveryReplyDeliveredOneByteAtATime_SendsTheMessageAsWhenDeliveredWhole`,
  `ExecuteAsync_SameHandlerRunTwiceInParallel_KeepsEachSessionApart`.

Oracle, measured 2026-10-07 on curl 8.21.0 (Schannel) with `Record-CurlExchange.ps1 -Smtp`: a CR LF
in `mail-from`/`mail-rcpt` (given through `-K` with `\r\n` escapes) is sent raw, so the injected
line's 502 is read as the next command's reply - `RCPT failed: 502` and `DATA failed: 502`, exit 55;
`MAIL` answered 199 or 999 is `MAIL failed: <code>`, exit 55. 600 follows the same `code/100 != 2`
rule. Curl matched every measured answer.

Defects found: none, so no follow-up task was filed.

Test count (fast run of the project): 327 before, 351 after (24 new cases).

## Log

- 2026-10-06: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. SmtpProtocolHandler attacked with 24 adversarial black-box cases (dot-stuffing across 1-byte chunks, 998+/65536-byte lines, CRLF in addresses, out-of-range codes, parallel runs); no defects found
