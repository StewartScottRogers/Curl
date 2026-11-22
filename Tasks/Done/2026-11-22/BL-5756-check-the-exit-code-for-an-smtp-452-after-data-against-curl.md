---
id: BL-5756
title: Check the exit code for an SMTP 452 after DATA against curl 8.22.0
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Smtp.UnitLibrary/SmtpMailTransaction.cs]
requirement: none
created: 2026-11-22
completed: 2026-11-22
---
# BL-5756 — Check the exit code for an SMTP 452 after DATA against curl 8.22.0

## Goal

When the server refuses a message with `452` after the end of DATA (mailbox over quota), the port exits with curl 8.22.0's code and message.

## Context

From Surly cookbook bug report `2026-11-22T1116-Curl-bug-report.md` (batch 11 of the 1,000-recipe plan), finding 4 (check). Build tested: Curl `c84035f797`, Windows 11; server Surl `5da5b3eaf`. Check it still reproduces on the current branch first.

`surl --mail-domain example.com --mailbox "ann@example.com:annpass;quota=1k" smtp://127.0.0.1:2525/`, fill the mailbox, then `curl --mail-rcpt ann@example.com -T big.eml smtp://...`: exit 8 "Weird server reply"; the cookbook expected 55 or another send error. Upstream curl's `smtp_state_postdata_resp` returns `CURLE_WEIRD_SERVER_REPLY` (8) for any reply other than 250 after the data, as far as known, so the port may already match. Measure curl 8.22.0 with `Record-CurlExchange.ps1` (a loopback server answering `452 4.2.2 Mailbox full` after the `.`) and match it; if it already matches, pin it with a test and say so in the Notes.

## Acceptance criteria

- [x] The Notes record curl 8.22.0's exit code and stderr for a 452 after DATA.
- [x] The port gives the same, pinned by a test.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

Measured curl 8.22.0 (Schannel) with Record-CurlExchange.ps1 -Smtp -SmtpReply "DATADONE=452 4.2.2 Mailbox full": exit 8, stderr `curl: (8) Weird server reply`. The port already matched (SmtpMailTransaction returns WeirdServerReply for any non-250 after DATA); no production change. Pinned by a new 452 DataRow in SmtpProtocolHandlerUploadTests.ExecuteAsync_MessageAnsweredOtherThan250_IsExit8AndQuits. Surly's expectation of 55 does not match curl.

## Log

- 2026-11-22: Created from Surly bug report 2026-11-22T1116, finding 4 (check).
- 2026-11-22: Backlog -> Doing. Criteria at claim: e230aa96 1a5977bd 3d836d5e.
- 2026-11-22: Doing -> Done. 452 after DATA already exit 8 like curl 8.22.0; pinned by test
