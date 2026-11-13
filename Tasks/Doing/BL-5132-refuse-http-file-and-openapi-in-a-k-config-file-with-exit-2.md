---
id: BL-5132
title: Refuse --http-file and --openapi in a -K config file with exit 2
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary/CommandLineParser.cs, Curl.Cli.UnitTests/ConfigFileArgumentExpanderOptionTests.cs]
requirement: none
created: 2026-11-13
completed:
---
# BL-5132 — Refuse --http-file and --openapi in a -K config file with exit 2

## Goal

`--http-file`, `--http-env`, `--openapi` and `--operation` in a `-K` config file either work or are refused with exit 2 naming the option; they no longer do nothing silently.

## Context

From the outside fitness review of 2026-11-13 (commit 56cb97ede, `preview` build), finding EXT-055 (Low, Confirmed; a "before drop-in" item). Part B: judged against the port's own docs and specs, not curl. Silent wrong operation.

**Repro.**
1. `printf -- '--http-file hf/a.http\n--http-env dev\n' > k3.cfg`, where `hf/a.http` holds three requests (GET, POST, DELETE) to the server.
2. `curl -sS -m 5 -K k3.cfg http://127.0.0.1:28000/plain -o NUL -w "%{method} %{url_effective}\n"`
3. `printf 'openapi = "oa.json"\noperation = "delPet"\n' > k5.cfg; curl -sS -m 5 -K k5.cfg http://127.0.0.1:28000/plain -o NUL -w "%{method} %{url_effective}\n"`

**Expected.** ADR-1040 (`Documentation/Planning/Decisions/ADR-1040-an-extension-option-expands-into-curl-command-line-arguments.md`) says the expanders run only on top-level arguments, "not lines of a -K config file". Other extension options are honoured from `-K` (`checksum = ...`, `--structured-fields`). An option that cannot work from `-K` must be refused there (exit 2 naming it), not accepted silently.

**Actual.** Steps 2 and 3 print `GET http://127.0.0.1:28000/plain`, exit 0; the server receives one request and none of the file's requests run. Without a URL, `curl -sS -K k3.cfg` prints `curl: (2) no URL specified`.

**Where to start.** The `-K` path in `Curl.Cli.UnitLibrary/CommandLineParser.cs` accepts the `ExtensionOptionCatalogue` entries for the argument-expander options (see `Curl.Extension.Composition.UnitLibrary/ExtensionArgumentExpanderCatalogue.cs`); `Curl.Console` `CurlCommandRunner.RunAsync` expands only top-level arguments. The run chooses between making the expanders run on `-K` lines and refusing the options there; refusing is the minimum, and the choice is recorded in an ADR note or an update to ADR-1040. If the Cli cannot know which options are expanders without a reference to Composition, pass the set in through the existing option-catalogue seam, never a Cli reference to an extension.

## Acceptance criteria

- [ ] A new test file `Curl.Cli.UnitTests/ConfigFileArgumentExpanderOptionTests.cs` proves that `--http-file`, `--http-env`, `--openapi` and `--operation` read from a `-K` file (in `--name value` and `name = "value"` forms) are either expanded as on the command line or refused with `CurlExitCode` 2 and a message naming the option and saying it cannot be used in a config file; the message is asserted exactly.
- [ ] A regression test proves other extension options (`checksum`, `--structured-fields`) are still honoured from `-K`, and that the same expander options on the command line still expand.
- [ ] If the options are refused, `--extended-help` for the http-file and openapi extensions states they are command-line only; `--ai-help` is kept right where it mentions them.
- [ ] ADR-1040 (or a new ADR marked "Decided by Claude under Stewart's delegation") states what happens to expander options in `-K`; the outdated line about extension options not being read from `-K` is corrected if it is in the same ADR.
- [ ] Tests are platform-neutral (no Windows-only paths).
- [ ] `dotnet build Curl.Cli.UnitLibrary -warnaserror` is clean, and `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Mock servers must be PowerShell, a C# file-based app (`dotnet run tool.cs`) or `Record-CurlExchange.ps1`, never Python. The unit tests need no server.
- Extension isolation holds: no extension references another extension or an Original protocol library.
- The touches list is minimal; add any extra file the run really needs (for example `Documentation/Planning/Decisions/ADR-1040-*.md` or the extension description files) to `touches` before editing it.
- Review acceptance check, as a hand check: step 2 either runs the three requests from `a.http` or exits 2 with a message that `--http-file` cannot be used in a config file; step 3 behaves the same for `--openapi`.

## Log

- 2026-11-13: Created.
- 2026-11-13: Backlog -> Doing. Criteria at claim: df1ee63f 403425be 15e4eb64 a0218085 89b4d29a a1aaf16f.
