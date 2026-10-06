---
id: BL-663
title: Renumber the duplicate ADRs 0086, 0088, 0093 and 0109 and update every reference
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions, Curl.Console, Curl.Console.UnitTests, Curl.Core.UnitLibrary, Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Ftp.UnitLibrary, Curl.Protocol.Ftp.UnitTests, Curl.Protocol.Http.UnitLibrary, Record-CurlExchange.ps1]
requirement: none
created: 2026-09-28
completed: 2026-10-01
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

- [x] `Get-ChildItem Documentation/Planning/Decisions/ADR-*.md` shows no two files sharing a four-digit number.
- [x] `Documentation/Planning/Decisions/README.md` has one row per ADR file, in number order.
- [x] A search for each moved ADR's old number finds only references to the ADR that kept it; each moved ADR's references use its new number.
- [x] `dotnet build Curl.slnx -warnaserror` is clean and the fast tests pass.

## Notes

- Kept the older ADR of each pair (git history: first-add commit times) and moved the other to the next free numbers: 0086 Schannel revocation -> 0321, 0088 Windows `-z` lookup -> 0322, 0093 FTP conversation -> 0323, 0109 HTTP/1.0 keep-alive left intact -> 0324. For 0109 the README indexed the newer one (keep-alive, 21:32) while the failed-connect ADR was added first (21:28), so history wins: failed-connect keeps 0109 and its README row was rewritten.
- ADR-0208 was duplicated too (Kerberos credential cache 05:48, alt-svc seams 05:49, both 2026-09-29); the first acceptance criterion covers every number, so alt-svc seams moved to 0325. Its references (ADR-0214, Networking CLAUDE.md) are in `touches` already; the Kerberos references keep 0208.
- Each moved ADR gains a `- **Renumbered:** from ADR-NNNN ...` line under its date; title lines and file names carry the new number.
- README was missing rows for ADR-0108, 0110, 0218 and 0245 as well; added so it has one row per file (320 files, 320 rows, in order, no dangling links).
- `Record-CurlExchange.ps1` cites the Schannel revocation ADR; added to `touches` (no task in Doing on `origin/work/dark-factory` names it; BL-654 touches only Curl.Cli).
- Archived `Tasks/Done` files keep the old numbers: they record what was true when written.

## Log

- 2026-09-28: Created.
- 2026-10-01: Backlog -> Doing.
- 2026-10-01: Doing -> Done. Every ADR number names one ADR: duplicates moved to 0321-0325, references and README index updated
