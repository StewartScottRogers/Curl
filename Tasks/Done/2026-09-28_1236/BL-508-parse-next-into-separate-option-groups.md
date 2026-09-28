---
id: BL-508
title: Parse -:/--next into separate option groups
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-488, BL-489, BL-491, BL-492, BL-493, BL-494, BL-495]
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests, Curl.Console, Curl.Console.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-28
completed: 2026-09-28
---
# BL-508 — Parse -:/--next into separate option groups

## Goal

`-:`/`--next` ends one option group and starts a fresh one, so `CommandLineParser` returns an ordered list of groups, each with its own per-transfer options and URLs, while global options (curl's `-s`, `-v`, `--fail-early`, `--parallel` and the like) apply to all groups whichever group they appear in.

## Context

- Conformance audit 2026-09-28, row 8 (Blocker, M-L). Running the groups is BL-509.
- `Curl.Cli.UnitLibrary/CommandLineParser.cs` and `CommandLineParseResult.cs` currently produce one `CommandLineOptions`. `-K` config files may contain `next` lines too (`ConfigFileApplier.cs`).
- Which options are global is not guessable: curl marks them in its tool (`ARG_...` / the `global` flag). Measure the doubtful ones (for instance `-v` given only after `--next`, `-w` given only before it, `-o` counts per group, and a group with no URL: `curl URL --next`) with `Record-CurlExchange.ps1 -Connections 2`, and record the list of global options in Notes with how each was established.
- `-:` is accepted in a bundle position as curl allows; measure `-s:` too.
- Every option `CommandLineOptionTable` parses when this task runs is classified as global or per-group. It waits for BL-488, BL-489 and BL-491 to BL-495 so the options those add are classified here rather than left for someone to remember later; a test enumerating the option table and failing on an unclassified option keeps later additions honest.

## Acceptance criteria

- [x] Measured first with `Record-CurlExchange.ps1`: the cases above, stdout, stderr, request bytes and exit code copied into Notes.
- [x] `CommandLineParseResult` exposes the groups in order; `Curl.Cli.UnitTests` show per-group options reset after `--next`, global options shared, and `-K` `next` lines splitting groups.
- [x] A group with no URL is refused (or ignored) exactly as measured, with the text pinned.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

Measured 2026-09-28 with curl 8.21.0 (the script's reference build, Windows) through
`Record-CurlExchange.ps1 -Port 45808 -Connections 2` (1 where one request was expected), every
command line starting `-q`; A = `http://127.0.0.1:45808/a`, B = `/b`, C = `/c`; the canned reply is
`200` with body `hi`. "meter" means the progress meter only.

| Command line | exit | stdout | stderr | requests |
| --- | --- | --- | --- | --- |
| `A --next -v B` | 0 | `hihi` | `-v` lines for both A and B (with the meter) | GET /a, GET /b |
| `-w [w] A --next B` | 0 | `hi[w]hi` | meter, meter | GET /a, GET /b |
| `-o f508a A --next B` | 0 | `hi` (B only) | meter, meter | GET /a, GET /b |
| `-H "X-A: 1" A --next B` | 0 | `hihi` | meter, meter | X-A on /a only |
| `A --next` | 2 | `hi` | meter, `curl: (2) no URL specified`, try-help | GET /a |
| `-s A --next` | 2 | `hi` | `curl: (2) no URL specified`, try-help | GET /a |
| `--next A` | 2 | | `curl: missing URL before --next`, `curl: option --next: is badly used here`, try-help | none |
| `A --next --next B` | 2 | | the same three lines | none |
| `-: A` | 2 | | `curl: missing URL before --next`, `curl: option -:: is badly used here`, try-help | none |
| `-s: A` | 2 | | `curl: option -s:: is badly used here`, try-help | none |
| `-s --next A` | 2 | | `curl: option --next: is badly used here`, try-help | none |
| `-sS --next A` | 2 | | all three lines | none |
| `A -s:A B` | 0 | `hihi` | (silent) | GET /a, GET /b |
| `A -:s B` | 0 | `hihi` | meter, meter (`s` ignored) | GET /a, GET /b |
| `A --next=x B` | 0 | `hihi` | meter, meter | GET /a, GET /b |
| `A --no-next B` | 2 | | `curl: option --no-next: the given option cannot be reversed with a --no- prefix`, try-help | none |
| `-o nul A --next --bogusx` | 2 | | `curl: option --bogusx: is unknown`, try-help | none |
| `-s -F a=b -d z A --next B` | 2 | | | none |
| `-s A --next -F a=b -d z B` | 2 | `hi` | | GET /a |
| `-o nul -o nul2 A --next B` | 0 | | meter, `Warning: Got more output options than URLs` twice | GET /a only |
| `--no-progress-meter -o nul -o nul2 A --next B --next C` | 0 | | the warning three times | GET /a only |
| `--no-progress-meter A --next -o nul -o nul2 B --next C` | 0 | `hi` | the warning twice | GET /a, GET /b |
| `-s -K k1` (`url A`, `next`, `url B`) | 0 | `hihi` | | GET /a, GET /b |
| `-K k2` (`next`, `url B`) | 0 | `hi` | meter | GET /b (the `next` is ignored) |
| `-s A -K k3` (`-d x`, `-:`, `url B`) | 0 | `hihi` | | POST /a (body `x`), GET /b |
| `-s A -K k4 B` (`-d x`, `-:`, `-H "X-B: 2"`) | 0 | `hihi` | | POST /a, GET /b with `X-B: 2` |

Global options, and how each was established (`CommandLineOptionTable.GlobalOptionLongNames`):
`-v` measured above; `--fail-early`, `-#`, `--progress-meter`, `-S`, `--stderr`,
`--styled-output`, `--trace`, `--trace-ascii`, `--trace-time` from curl 8.21.0's manual ("This
option is global", read from `CurlManual.txt`); `-s`, `--variable`, `-V`, `-h`, `-M` held in curl's
`GlobalConfig` without the manual's mark; `config`, `next`, `disable` act on the command line as a
whole. Every other row is per-group (`-w`, `-o`, `-H` measured above); the list is in
`CommandLineNextGroupTests.PerGroupOptionLongNames`, and a row in neither list fails
`OptionTable_EveryRow_IsClassifiedAsGlobalOrPerGroupExactlyOnce`.

Decisions (ADR-0125): later-group setup refusals are `CommandLineParseResult.RefusalAfterGroups`,
printed by the console after the groups before them; per-group password prompts end
` on URL #<n>:` as curl's `checkpasswd` source builds them. That prompt cannot be measured,
because curl reads it from the console.

`touches` widened: upstream test686 (`htdhdhdtp://localhost --next`, exit 2) is on the
conformance ratchet and would have failed once a trailing empty group no longer refused the whole
command line. `Curl.Console` now prints `RefusalAfterGroups` after its transfers (three lines in
`CurlCommandRunner.RunParsedAsync`, one test). No task in `Doing` names `Curl.Console`, in this
lane or the others. The ADR is under `Documentation/Planning/Decisions`. The stray `f1` file left
by the cut-off run (its contents were `hi`, a measured body) was deleted.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. -:/--next parses into ordered option groups sharing global options; missing-URL and later-group refusals pinned as measured
