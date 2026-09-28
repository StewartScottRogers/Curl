---
id: BL-509
title: Run each -:/--next option group's URLs with that group's options
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-508]
touches: [Curl.Console, Curl.Console.UnitTests, Curl.Cookies.UnitLibrary, Curl.Cookies.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-28
completed: 2026-09-28
---
# BL-509 — Run each -:/--next option group's URLs with that group's options

## Goal

`CurlCommandRunner` runs the groups BL-508 parses in order, each URL with its own group's options (method, headers, data, output, `-w`), shares the connection pool, cookie engine and transfer numbering across groups as curl 8.21.0 does, and exits with the code curl gives for a run in which some groups fail.

## Context

- Conformance audit 2026-09-28, row 8 (Blocker). Parsing is BL-508.
- Code: `Curl.Console/CurlCommandRunner.cs` (the per-URL loop), `TransferContextFactory.cs`, `UrlTransfer.cs`, `CurlComposition.cs`. Connection reuse across URLs is ADR-0050; `%{xfer_id}`/`%{conn_id}`/`%{urlnum}` numbering is ADR-0060.
- `--fail-early` stops at the first failure; without it curl continues and the final exit code is the last failure's (measure).
- Parallel transfers (`-Z`, BL-517 onwards) build on the group list this task introduces into the runner.

## Acceptance criteria

- [x] Measured first with `Record-CurlExchange.ps1 -Connections 3`: `curl -d a URL1 --next URL2` (POST then GET), a failing first group with and without `--fail-early`, `-w '%{urlnum} %{xfer_id} %{conn_id}\n'` in each group, and `-c jar` in one group; request bytes, stdout, stderr and exit code copied into Notes.
- [x] `Curl.Console.UnitTests` tests with a fake handler pin each measured case: request per group, output per group, numbering, exit code.
- [x] A single-group command line behaves exactly as before (existing tests pass unchanged).
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Console` reports 100% line and branch coverage and no failing member.

## Notes

- From BL-508 (ADR-0125, BL-508 Notes): run `CommandLineParseResult.Groups` in order. The console
  already prints `RefusalAfterGroups` after the transfers. curl 8.21.0 runs no group after one
  that has an `-o` left over, and prints the "more output options than URLs" warning once for
  that group and once for each group after it (`WarningLinesAfterTransfers` already counts them).

Measured 2026-09-28 with curl 8.21.0 (Git for Windows mingw, Schannel) through
`Record-CurlExchange.ps1 -Port 45909 -Connections 1..3`, every command line starting `-q`; A, B, C =
`http://127.0.0.1:45909/a`, `/b`, `/c`; X = `http://127.0.0.1:1/x` (refused); the canned reply is
`200`, `Content-Length: 2`, body `hi` (plus `Set-Cookie: k1=v1` for the first and `k2=v2` for the
second connection in the cookie rows, and `X-N: 1`/`X-N: 2` in the `-D` rows). `(7)` is
`curl: (7) Failed to connect to 127.0.0.1:1 after ~2020 ms: Could not connect to server`.

| Command line | exit | stdout | stderr | requests |
| --- | --- | --- | --- | --- |
| `-s -d a A --next B` | 0 | `hihi` | | `POST /a` (`Content-Length: 1`, form type, body `a`), `GET /b` |
| `-s -S X --next B` | 0 | `hi` | `(7)` | GET /b |
| `-s -S --fail-early X --next B` | 7 | | `(7)` | none |
| `-s -S X --next --fail-early B` | 7 | | `(7)` | none |
| `-s -S A --next X` | 7 | `hi` | `(7)` | GET /a |
| `-s -S X --next nosuch://h/ --next A` | 0 | `hi` | `(7)`, `curl: (1) Protocol "nosuch" not supported` | GET /a |
| `-s -w '%{urlnum} %{xfer_id} %{conn_id}\n' A B --next -w (same) C` | 0 | `hi0 0 0\nhi1 1 1\nhi2 2 2\n` | | GET /a, /b, /c |
| `-s -c jar1 A --next B` | 0 | `hihi` | | B sends no Cookie; `jar1` holds k1 only |
| `-s A --next -c jar2 B` | 0 | `hihi` | | `jar2` holds k2 only |
| `-s -c jar4 A --next -c jar5 B` | 0 | `hihi` | | B sends `Cookie: k1=v1`; `jar4` k1; `jar5` k2 then k1 |
| `-s -c jar6 A --next -b x=y B` | 0 | `hihi` | | B sends `Cookie: x=y` only; `jar6` k1 |
| `-s -D h1 A --next -D h1 B` | 0 | `hihi` | | `h1` holds only B's head (`X-N: 2`) |
| `-s -D h2 A --next B` | 0 | `hihi` | | `h2` holds A's head |
| `A --next -I -d x B --next A` | 2 | `hi` | meter, then the two-line POST/HEAD warning | GET /a only |
| `-I -d x A --next B` | 2 | | the two-line warning | none |
| `-s -o NUL -w '%{exitcode}\n' A --next B` | 0 | `30 0A 68 69` (LF) | | GET /a, /b |
| `-s -o NUL -w '%{exitcode}\n' A --next -o NUL B` | 0 | `30 0D 0A` (CR LF) | | GET /a, /b |
| `-s -w '[%{conn_id} %{num_connects}]' A --next -w (same) B`, `-HoldOpenMilliseconds 1500` | 0 | `hi[0 1]hi[1 1]` | | GET /a; GET /b sent on A's held connection, then again on a new one when it closed |

Decisions (ADR-0126): each group gets its own `TransferDispatch` built from its own options, so
its TLS, proxy and `--resolve` settings are its own; `%{urlnum}`/`%{xfer_id}`/`%{conn_id}` and the
`-v`/trace output are run-wide; one `CookieStore` per run (`CurlComposition.SharingRunCookies`)
with each group's `-b`/`-c`/`-j` its own, which needed a `CookieStore.GetCookieHeader` overload
taking the group's `-b` strings. The last row shows curl reusing a connection across groups;
that is not modelled yet and is filed as BL-754 (the pool key needs the TLS settings first).

`touches` widened: `Curl.Cookies.UnitLibrary`/`.UnitTests` for that overload, and
`Documentation/Planning/Decisions` for ADR-0126. No task in `Doing` names them (this lane has only
BL-509; lanes 2 and 3 hold tasks touching `Curl.Cli.UnitLibrary`/`.UnitTests` only).
`CommandLineOptions.HasMoreOutputOptionsThanUrls` is internal to `Curl.Cli`, which other lanes
hold, so the runner tests the same thing through the public `UrlOutputs` (an entry is left
without a URL only by an output option).

Tests: `CurlCommandRunnerNextGroupTests` (19) pin every row except the last; `CookieStoreTests`
gains two; `UrlTransferTests` pins `UrlNumber`. Console 1155 passed, 3 skipped; every fast test
assembly green. `Measure-CodeQuality.ps1`: `Curl.Console` and `Curl.Cookies.UnitLibrary` at 100%
line and branch, 0 failing members.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. Each -:/--next group runs its URLs with its own options; urlnum, xfer_id, conn_id and cookies carry across groups; exit code is the last transfer's
