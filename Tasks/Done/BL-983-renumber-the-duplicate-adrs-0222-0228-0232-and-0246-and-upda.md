---
id: BL-983
title: Renumber the duplicate ADRs 0222, 0228, 0232 and 0246 and update every reference
priority: Normal
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions, Curl.Networking.UnitLibrary, Curl.Quic.UnitLibrary, Curl.Console, Curl.Kerberos.UnitLibrary, Tasks/Backlog/BL-940-check-ml-dsa-ed448-brainpool-tls-1-3-and-sha-224-signatures.md, Tasks/Backlog/BL-941-send-the-tls-1-2-only-hand-built-clienthello-in-the-platform.md, Tasks/Backlog/BL-1072-log-proxy-choices-alt-svc-alternatives-and-hsts-entry-expiry.md, Tasks/Done/BL-921-log-retries-redirects-watchdog-limits-hsts-and-alt-svc-decis.md, Tasks/Done/BL-979-forward-repeatauthorization-through-awssigv4httpauthenticato.md]
requirement: none
created: 2026-09-29
completed: 2026-10-01
---
# BL-983 — Renumber the duplicate ADRs 0222, 0228, 0232 and 0246 and update every reference

## Goal

ADR numbers 0222, 0228, 0232 and 0246 each name exactly one record in `Documentation/Planning/Decisions`: in each pair the record fewer references cite gets the next free number, its title line and file name say so, `README.md` indexes both in number order above `## Template`, and every reference points at the record it meant.

## Context

- Parallel lanes numbered ADRs from their own copies of the folder (found during BL-887, 2026-09-29). The pairs:
  - `ADR-0222-curl-s-own-diagnostic-log-is-log-level-and-log-file-off-by-default-with-zero-extra-bytes.md` and `ADR-0222-the-hand-built-tcp-client-sends-the-platform-profile-hello-cut-to-what-it-can-honour.md`
  - `ADR-0228-a-websocket-upgrade-answers-no-401-as-curl-8-21-0-sends-it-once.md` and `ADR-0228-the-runner-opens-the-diagnostic-log-once-the-command-line-is-accepted-and-writes-it-holding-the-write-gate.md`
  - `ADR-0232-an-empty-authorization-value-sends-the-request-again-without-one.md` and `ADR-0232-the-hand-built-kerberos-has-des3-cbc-sha1-as-mit-1-22-does.md`
  - `ADR-0246-a-kept-digest-answer-is-counted-on-from-the-value-as-sent.md` and `ADR-0246-styled-output-bolds-header-names-and-links-location-on-a-terminal-as-curl-does.md`
- BL-663 owns 0086, 0088, 0093 and 0109; BL-886 owns 0187 and 0193; BL-887 renumbered 0158, 0165 and 0200 (to 0254, 0255, 0256).
- `README.md` has the 0228 (runner) and 0248 rows below `## Template` rather than in the index; move them into the index too.
- Find references with `git grep -n "ADR-0222\b"` (and the others) outside `Tasks/Done/<timestamp>/`; read each to tell which record it means. Fill `touches` with the project folders that cite them before starting. Code changes are comments only.

## Acceptance criteria

- [x] `Get-ChildItem Documentation/Planning/Decisions -Filter 'ADR-02[24]*.md'` and `-Filter 'ADR-023*.md'` show 0222, 0228, 0232 and 0246 once each.
- [x] `Documentation/Planning/Decisions/README.md` has one row per record of the four pairs, and the 0248 row, in number order in the index above `## Template`.
- [x] A search for each renumbered record's old number finds only references to the record that kept it; each renumbered record's references use its new number.
- [x] `dotnet build Curl.slnx -warnaserror` is clean and the fast tests pass.

## Notes

- Plan (lane 2, 2026-09-29), from `git grep` of each number outside `Tasks/Done/<timestamp>/`:
  - 0222: the diagnostic log is cited ~80 times and keeps 0222; the TCP-client hello (BL-820) becomes **0235**. Its references: `Curl.Networking.UnitLibrary/CLAUDE.md` line 30 ("Per ADR-0222 (BL-820)"; line 297 is the log and stays), `Curl.Quic.UnitLibrary/QuicClientSettings.cs` line 106 (comment), `Tasks/Backlog/BL-940-…` and `BL-941-…` (each "ADR-0222 (BL-820)" twice).
  - 0228: the WebSocket 401 record keeps 0228 (Ws handler and tests, ADR-0231, BL-953); the runner's diagnostic-log record becomes **0236**. Its references: `Curl.Console/CLAUDE.md` line 422 ("ADR-0222, ADR-0228, BL-919"), and its README row below `## Template`.
  - 0232: the empty-`Authorization` record keeps 0232; the Kerberos des3-cbc-sha1 record becomes **0237**. Its references: `Curl.Kerberos.UnitLibrary/CLAUDE.md` line 29, `Curl.Kerberos.UnitLibrary/Des3CbcSha1KerberosEncryption.cs` line 18.
  - 0246: styled output keeps 0246 (Console, Output); the kept-Digest record becomes **0238**. Its references: `ADR-0239` line 51 ("superseded by ADR-0246 … (BL-869)"), `Tasks/Backlog/BL-979-…` line 21. Neither 0246 record has a README row yet; add both.
- New numbers are the lowest free ones, 0235–0238: no branch, lane worktree or task cites them, and lanes numbering a new ADR take the highest plus one, so the gap avoids a fresh collision.
- `touches` widened to the folders above that cite the renumbered records. `Curl.Networking.UnitLibrary` and `Curl.Console` are in BL-717's `touches` (in Doing, lane 1), so the task went back to Backlog until BL-717 is done.
- Done (lane 1, 2026-10-01) as planned: 0222 hello -> ADR-0235, 0228 runner -> ADR-0236, 0232 des3 -> ADR-0237, 0246 Digest -> ADR-0238 (`git mv`, title lines renumbered). 0235-0238 were still free on `origin/work/dark-factory`. References updated since the plan moved: `Curl.Networking.UnitLibrary/CLAUDE.md` line 34; `ADR-0290` line 34 ("as ADR-0222 cuts them over TCP" meant the hello); `Curl.Console/CLAUDE.md` line 436; `BL-941` (three); `BL-1072` line 21 and `Tasks/Done/BL-921` line 41 (both meant the runner record); `Tasks/Done/BL-979` line 21 (the Digest record); Kerberos `CLAUDE.md` and `Des3CbcSha1KerberosEncryption.cs`; `ADR-0239` line 51. `QuicClientSettings.cs` no longer cites it, and BL-940 is now archived, so neither changed. Archived tasks under `Tasks/Done/<timestamp>/` keep their history as written.
- `touches` gained `BL-1072`, `Tasks/Done/BL-921` and `Tasks/Done/BL-979` (each cites a renumbered record); no task in Doing names them.
- README: rows for 0235-0238 and 0246 (styled output) added; the runner row (now 0236) and the 0248 Negotiate row moved from below `## Template` into the index. The 0289 (SSH reset), 0290 and 0291 rows are still below `## Template`, and the 0248 SSH known-hosts record has no row: left for the 0248/0254/0289 duplicate task.
- Also duplicated, outside this task: 0289 (SSH reset after key exchange / HTTP/3 CONNECT-UDP). Filed BL-1075 for 0248, 0254 and 0289.
- Also duplicated, outside this task: 0248 (Negotiate 2xx / SSH known hosts) and 0254 (TcpConnector happy eyeballs / TLS 1.2 byte stream). Worth their own task.

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Backlog. Needs Curl.Networking.UnitLibrary and Curl.Console, both in BL-717's touches (Doing, lane 1); renumbering plan is in Notes
- 2026-10-01: Backlog -> Doing.
- 2026-10-01: Doing -> Done. ADR numbers 0222, 0228, 0232 and 0246 name one record each; the hello, runner, des3 and Digest records are ADR-0235 to ADR-0238, indexed and cited by their new numbers
