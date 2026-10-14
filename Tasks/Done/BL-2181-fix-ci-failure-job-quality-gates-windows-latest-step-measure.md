---
id: BL-2181
title: Fix CI failure job Quality gates (windows-latest) step Measure coverage, complexity and CRAP on Windows
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Imap.UnitLibrary, Curl.Protocol.Mqtt.UnitLibrary, Curl.Protocol.Smtp.UnitLibrary]
requirement: none
created: 2026-10-13
completed: 2026-10-13
---
# BL-2181 — Fix CI failure job Quality gates (windows-latest) step Measure coverage, complexity and CRAP on Windows

## Goal

`job Quality gates (windows-latest) step Measure coverage, complexity and CRAP` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `job Quality gates (windows-latest) step Measure coverage, complexity and CRAP` failed on Windows in CI run 38502834786 (https://github.com/StewartScottRogers/Curl/actions/runs/38502834786). First failing commit: bc52953f.

    [36;1mforeach ($m in @((Get-Content "$env:RUNNER_TEMP\coverage.json" -Raw | ConvertFrom-Json).failing)) {[0m

Lanes test only on Windows, so reproduce with `gh run view 38502834786 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [x] `job Quality gates (windows-latest) step Measure coverage, complexity and CRAP` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix. (Locally: 0 failing members, 100/100 in all three libraries; CI is the shift's to confirm.)

## Notes

- Cause: four members over complexity 10, all from BL-2177's credential and address checks:
  `ImapProtocolHandler.TransferAsync` (14), `SmtpProtocolHandler.TransferAsync` (12),
  `SmtpCommandTransfer.SendCommandsAsync` (14) and `MqttTransferMessages.IsStrerrorText` (12).
- Fix, behaviour unchanged: both `TransferAsync`s keep the credential check and hand the
  connect and session to a new `ConnectAndTransferAsync`; the SMTP recipient loop moved to
  `SendRecipientCommandsAsync` with `IsRefusedVerifyAddress` and `RecipientCommand`;
  `IsStrerrorText` looks the message up in an ordinal `HashSet` instead of a six-way `or`.
- Touches set to the three libraries; no other task was in Doing on `origin/work/dark-factory`.
- Measured with `Measure-CodeQuality.ps1 -Library` on the three: Imap 202 members, Mqtt 99,
  Smtp 167, each 100% line and branch, 0 failing, highest complexity 10. Fast tests green.

## Log

- 2026-10-13: Created.
- 2026-10-13: Backlog -> Doing.
- 2026-10-13: Doing -> Done. Split the four methods BL-2177 pushed past complexity 10; the three libraries measure 0 failing members at 100/100
