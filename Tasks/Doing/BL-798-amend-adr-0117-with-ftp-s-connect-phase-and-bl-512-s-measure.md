---
id: BL-798
title: Amend ADR-0117 with FTP's connect phase and BL-512's measurements
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-512]
touches: [Documentation/Planning/Decisions/ADR-0117-the-connector-owns-the-connect-timeout-and-the-runner-owns-max-time.md]
requirement: none
created: 2026-09-28
completed:
---
# BL-798 — Amend ADR-0117 with FTP's connect phase and BL-512's measurements

## Goal

ADR-0117 carries an amendment, "Decided by Claude under Stewart's delegation", recording what BL-512 measured and built: `--connect-timeout` also holds FTP's greeting, login, `PBSZ`, `PROT` and `PWD` (curl's states before `DO`), enforced in the handler by `FtpConnectPhaseLimit`, and a handler with a protocol-level connect phase reports `ReportTransferStarted` once its TCP connection is up.

## Context

- BL-512 could not edit the ADR: `Documentation/Planning/Decisions` was held by BL-617 in Doing at the time. Its Notes hold the measurements and the decisions to copy.
- The ADR's contract section says "Call `ITransferProgress.ReportTransferStarted` once the connection is up"; the amendment adds that a protocol whose login curl counts as connecting (FTP; later SMTP, POP3, IMAP, SSH) holds that phase to `--connect-timeout` itself, with the `Operation timed out ... with 0 bytes received` message, since the connector's limit ends with the TCP connect.
- It should also note the open gap BL-797 closes (the passive data connect is not held to `--connect-timeout` by curl).

## Acceptance criteria

- [ ] ADR-0117 has an "Amendment (BL-512, 2026-09-28)" section, marked "Decided by Claude under Stewart's delegation", quoting the measured stderr and exit codes from BL-512's Notes.
- [ ] The amendment names `FtpConnectPhaseLimit`, says where the phase ends (the `PWD` reply), and states the 300-second default when `--connect-timeout` is not given or is 0.
- [ ] The amendment names BL-797 as the open gap for the passive data connect.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
