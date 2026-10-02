---
id: BL-663
title: Renumber the duplicate ADRs 0086, 0088, 0093 and 0109 and update every reference
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions, Curl.Console, Curl.Console.UnitTests, Curl.Core.UnitLibrary, Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Ftp.UnitLibrary, Curl.Protocol.Ftp.UnitTests, Curl.Protocol.Http.UnitLibrary]
requirement: none
created: 2026-09-28
completed:
---
# BL-663 — Renumber the duplicate ADRs 0086, 0088, 0093 and 0109 and update every reference

## Goal

Every ADR number in `Documentation/Planning/Decisions` names exactly one ADR: the second ADR of each duplicated pair gets the next free number, its title line and file name say so, `README.md` indexes all of them, and every reference in documents and code comments points at the ADR it meant.

## Context

- Conformance audit 2026-09-28, row 46 (Minor) named 0088, 0093 and 0109; 0086 is duplicated too. The pairs:
  - `ADR-0086-a-repeated-transfer-started-report-starts-the-next-redirect-hops-status-line.md` and `ADR-0086-the-schannel-build-checks-revocation-for-a-cacert-chain.md`
  - `ADR-0088-a-socket-error-mid-handshake-is-curls-recv-failure-line.md` and `ADR-0088-the-windows-z-file-lookup-calls-createfile-and-getfiletime-as-curl-does.md`
  - `ADR-0093-a-refused-multipart-byte-fails-the-send-with-exit-26.md` and `ADR-0093-ftp-downloads-hold-curls-measured-conversation-in-passive-mode-only.md`
  - `ADR-0109-a-failed-connect-takes-the-next-connection-number.md` and `ADR-0109-an-http-1-0-keep-alive-body-read-to-the-close-is-reported-left-intact.md`
- `README.md` indexes only one ADR of each pair today. Keep the number on the ADR the README already indexes (the older reference holder) and move the other; if the dates or git history say otherwise, follow the history and say why in the commit.
- References are in the Decisions folder and in code comments and `CLAUDE.md` files across the projects in `touches` (found with a search for the four numbers); read each to tell which ADR it means. Renaming a file is a docs change; no behaviour changes. The `verify` skill runs after, as for every docs task.
- Parallel lanes caused this; the ADR tasks on the board all touch `Documentation/Planning/Decisions` so they no longer run side by side.

## Acceptance criteria

- [ ] `Get-ChildItem Documentation/Planning/Decisions/ADR-*.md` shows no two files sharing a four-digit number.
- [ ] `Documentation/Planning/Decisions/README.md` has one row per ADR file, in number order.
- [ ] A search for each moved ADR's old number finds only references to the ADR that kept it; each moved ADR's references use its new number.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and the fast tests pass.

## Notes

## Log

- 2026-09-28: Created.
- 2026-10-01: Backlog -> Doing.
