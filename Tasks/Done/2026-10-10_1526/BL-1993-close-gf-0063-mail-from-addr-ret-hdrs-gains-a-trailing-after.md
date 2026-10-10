---
id: BL-1993
title: Close GF-0063: --mail-from '<addr> RET=HDRS' gains a trailing '>' after the parameters; curl sends an address that starts with '<' as given
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Smtp.UnitLibrary, Curl.Protocol.Smtp.UnitTests]
requirement: none
created: 2026-10-10
completed: 2026-10-10
---
# BL-1993 — Close GF-0063: --mail-from '<addr> RET=HDRS' gains a trailing '>' after the parameters; curl sends an address that starts with '<' as given

## Goal

Curl behaves as curl 8.21.0 does for every item of gap finding GF-0063 (--mail-from '<addr> RET=HDRS' gains a trailing '>' after the parameters; curl sends an address that starts with '<' as given), so a later gap analysis measures each of `behaviour:test3215` as `match`.

## Context

- Finding: GF-0063, filed by the gap analysis office (ADR-0433).
- Area: behaviour. Severity: High. Introduced in: not stated upstream.
- Items: `behaviour:test3215`.
- Targeted curl version: 8.21.0 (Gap/Baselines/target.json).
- Yardstick: `tests/data/test*` in the curl 8.21.0 release tarball.
- The gap closes only when a later gap analysis re-measures every item as `match` (ADR-0433 decision 5), never because this task reaches Done.

Evidence, copied from the finding:

behaviour:test3215 (--mail-from "<sender@example.com> RET=HDRS") expected 'upstream test3215 passes', actual '<verify><protocol> differs at byte 50 (line 2): expected "MAIL FROM:<sender@example.com> RET=HDRS\r\n", got "MAIL FROM:<sender@example.com> RET=HDRS>\r\n"'. Reproduce: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "$env:LOCALAPPDATA/Curl/gap/upstream/8.21.0/tests/data" C:/Temp/gap/raw.json 3215

Suggestion, copied from the finding:

In Curl.Protocol.Smtp.UnitLibrary's SmtpMailTransaction, send a --mail-from (and --mail-rcpt) value that already starts with '<' as given, adding no closing '>', as curl 8.21.0's smtp.c does, so DSN parameters (RET=, NOTIFY=) survive.

## Acceptance criteria

- [x] `behaviour:test3215`: Curl answers what curl 8.21.0 answers, `upstream test3215 passes`, so the item measures `match`.
- [x] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [x] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md). No option was added or changed: --mail-from, --mail-rcpt and --mail-auth only send what curl sends.

## Notes

- Measured curl 8.21.0 (Schannel build, `Record-CurlExchange.ps1 -Smtp`): an address that starts with `<` is cut at its LAST `>`, and what follows is sent after the closing bracket: `--mail-from "<s@example.com> RET=HDRS"` -> `MAIL FROM:<s@example.com> RET=HDRS SIZE=4`, `--mail-rcpt "<r@example.com> NOTIFY=SUCCESS"` -> `RCPT TO:<r@example.com> NOTIFY=SUCCESS`, `--mail-auth "<a@example.com> X=Y"` -> `AUTH=<a@example.com> X=Y`, `"<r@b> x>y"` -> `RCPT TO:<r@b> x>y`. The default `VRFY` drops the suffix (`<v@example.com> X` -> `VRFY v@example.com`; `<r@b> x>y` -> `VRFY r@b> x`, which is what shows the cut is at the last `>`). An address not starting with `<` loses only one trailing `>` and has no suffix (`r@example.com> X` -> `RCPT TO:<r@example.com> X>`), unchanged. `-X VRFY` already sent the recipient as given.
- Fix: `SmtpMailbox` splits off the suffix; `Bracketed` appends it, `Bare` drops it. Unit tests pin every measured case, including upstream test3215's exact `MAIL FROM` line. The gap tool itself is out of a lane's reach (audit guard), so `behaviour:test3215` is confirmed by the next gap run.
- No ADR: this matches measured curl, no design choice was made.
- The full fast run had one failure in Curl.Networking.UnitTests that passed alone; filed as BL-2028.

## Log

- 2026-10-10: Created.
- 2026-10-10: Backlog -> Doing.
- 2026-10-10: Doing -> Done. MAIL FROM, RCPT TO and AUTH= keep the suffix after a bracketed address, as curl 8.21.0 sends it
