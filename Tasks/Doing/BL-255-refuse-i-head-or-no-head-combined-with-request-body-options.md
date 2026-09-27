---
id: BL-255
title: Refuse -I/--head (or --no-head) combined with request-body options at transfer setup, as curl 8.21.0 does
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-190]
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-255 — Refuse -I/--head (or --no-head) combined with request-body options at transfer setup, as curl 8.21.0 does

## Goal

A command line that asks for HEAD (`-I`) or GET (`--no-head`) and also carries a request body (`-d`/`--data*`, `--json`) without `-G` ends before any transfer with curl 8.21.0's two warning lines and exit 2 (`CurlExitCode.FailedInit`), whatever order the options were given in.

## Context

Measured on curl 8.21.0 (mingw, Schannel build) on 2026-09-26 with `curl <args> http://127.0.0.1:1/ -o /dev/null`.

curl does not check this while parsing. It checks it in `tool_operate` (`SetHTTPrequest` with `TOOL_HTTPREQ_SIMPLEPOST` when post fields are set and `-G` is not), so option order does not matter, and the refusal prints only the two warning lines: no `curl: option ...: is badly used here` line and no `curl: try 'curl --help'` line. Exit is 2.

| Command line | Standard error (each line exactly; note the trailing space on line 1) | Exit |
| --- | --- | --- |
| `-I -d x` | `Warning: You can only select one HTTP request method! You asked for both POST ` / `Warning: (-d, --data) and HEAD (-I, --head).` | 2 |
| `-d x -I` | same as `-I -d x` | 2 |
| `-I --json x` | same as `-I -d x` (it still names `-d, --data`) | 2 |
| `--no-head -d x` | `Warning: You can only select one HTTP request method! You asked for both POST ` / `Warning: (-d, --data) and GET (-G, --get).` | 2 |
| `-d x --no-head` | same as `--no-head -d x` | 2 |
| `-s -I -d x` | nothing | 2 |
| `-I -G -d x` | accepted: the data goes into the query, the transfer runs | per transfer |

Where the code is:

- BL-190 recorded the parse-time state in `Curl.Cli.UnitLibrary`: `CommandLineOptions.NoBody` (set by `-I`) and the internal `CommandLineOptions.HttpMethodSelected` (`SelectedHttpMethod` `None`/`Get`/`Head`, in `SelectedHttpMethod.cs`). The transfer-setup check needs to tell `--no-head` from "nothing chosen", so expose what it needs (for example make `HttpMethodSelected` and `SelectedHttpMethod` public, with XML docs) rather than re-deriving it.
- `CommandLineOptions.PostData` and `CommandLineOptions.DataInQuery` are the `-d`/`--json` and `-G` state (BL-188).
- The warning texts belong in `Curl.Cli.UnitLibrary/CommandLineWarning.cs` beside `HeadRequestedAfterGet` and `GetRequestedAfterHead`, as two `IReadOnlyList<string>` properties pre-wrapped at 79 columns the same way (suggested names: `PostRequestedWithHead`, `PostRequestedWithGet`), each with a doc comment naming the measured command.
- The check runs in `Curl.Console/CurlCommandRunner.cs` after the parse is accepted and before `TransferAllAsync` builds the dispatcher (around the `parsed.IsAccepted` branch in the run method). Write the lines through the runner's existing warning path so `WarningLineWrapper` applies. `-s -I -d x` was measured to print nothing; `-s -S -I -d x` was not measured, so measure it against real curl 8.21.0 before choosing, and pin what curl does in a test.
- `-T` (PUT) and `-F` (multipart) conflict with `-I` in the same way, but neither option is parsed yet; they are left to the tasks that add those options. Do not add them here.

Upstream: https://curl.se/docs/manpage.html (`-I`, `-d`, `-G`, `--json`), https://curl.se/libcurl/c/libcurl-errors.html (`CURLE_FAILED_INIT` = 2), curl 8.21.0.

## Acceptance criteria

- [ ] `CommandLineWarning` in `Curl.Cli.UnitLibrary` has the two two-line warnings above, and tests in `Curl.Cli.UnitTests` pin each line byte for byte, including the trailing space on the first line.
- [ ] Tests in `Curl.Console.UnitTests` run `CurlCommandRunner` with `-I -d x`, `-d x -I` and `-I --json x` against `http://127.0.0.1:1/` and assert standard error is exactly the POST/HEAD two lines, standard output is empty, no transfer is dispatched, and the exit code is 2 (`CurlExitCode.FailedInit`).
- [ ] Tests in `Curl.Console.UnitTests` do the same for `--no-head -d x` and `-d x --no-head` with the POST/GET two lines and exit 2.
- [ ] A test in `Curl.Console.UnitTests` runs `-s -I -d x` and asserts standard error is empty and the exit code is 2.
- [ ] A test in `Curl.Console.UnitTests` runs `-I -G -d x` and asserts the refusal lines are not written and the transfer is dispatched.
- [ ] No test needs `TestCategory=Integration`, and `dotnet test --filter "TestCategory!=Integration"` is green.
- [ ] `dotnet build Curl.Cli.UnitLibrary -warnaserror` and `dotnet build Curl.Console -warnaserror` are clean.
- [ ] `powershell -NoProfile -File Measure-CodeQuality.ps1` reports 100% line and 100% branch coverage for `Curl.Cli.UnitLibrary` and `Curl.Console`, with no method above cyclomatic complexity 10 or CRAP 30.
- [ ] `Curl.Console/CLAUDE.md` and `Curl.Cli.UnitLibrary/README.md` state that this refusal happens at transfer setup, not while parsing.

## Notes

## Log

- 2026-09-26: Created.
- 2026-09-27: Backlog -> Doing.
