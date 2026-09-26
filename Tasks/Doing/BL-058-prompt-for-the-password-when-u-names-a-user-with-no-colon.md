---
id: BL-058
title: Prompt for the password when -u names a user with no colon
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-038]
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-058 — Prompt for the password when -u names a user with no colon

## Goal

`-u user` with no colon prompts for the password through an injected prompt seam, using
curl's exact prompt text, and records the answer as the `CommandLineOptions.Credentials`
password instead of an empty one.

## Context

BL-038 made `CommandLineOptions.SetCredentials` (`Curl.Cli.UnitLibrary/CommandLineOptions.cs`)
split `-u`/`--user` at the first colon; a value with no colon records the user with an
empty password. Upstream curl 8.21.0 (<https://curl.se/docs/manpage.html#-u>): if only the
user name is given, curl prompts for a password.

Measured with the local curl 8.21.0 (`/mingw64/bin/curl` in Git Bash) on 2026-09-26:

- `curl -u bob bogus://x` writes exactly `Enter host password for user 'bob':` to standard
  error (last bytes `27 3A`: no trailing space, no newline), nothing to standard output,
  and waits for input from the console, not standard input (redirecting standard input
  from `/dev/null` still waits). The prompt comes before the unsupported scheme is
  reported, so it happens while the command line is handled, not at transfer time.
- `curl -u '' bogus://x` prompts `Enter host password for user '':`.

Not yet measured, and to be measured before any test states it: whether curl writes a
newline to standard error once the password is entered, whether `-u bob:` (colon, empty
password) prompts (the manual implies it does not), and what curl does when the console
cannot be read. Exact output must be measured against the local curl 8.21.0 at
`/mingw64/bin/curl` in Git Bash; record each measurement in `Notes`.

Design constraints:

- An interface defined in `Curl.Cli.UnitLibrary` (for example `IPasswordPrompt`, one member
  taking the prompt text and returning the typed password) is injected into the parser
  entry point; nothing in the parser calls `Console`. The BCL-backed implementation writes
  the prompt to standard error and reads without echo (`Console.ReadKey(intercept: true)`);
  keep it a thin pass-through so the logic under test lives in the parser, and if it
  cannot reach 100% coverage without a console, say so in `Notes`.
- Keep `Parse(IReadOnlyList<string>)` working. BL-080 adds a file and stdin reader seam to
  the same entry point; whichever of BL-080 and BL-058 lands second extends the first's
  entry point rather than adding another overload.
- The split at the first colon is unchanged for values that contain one.

## Acceptance criteria

- [ ] A test in `Curl.Cli.UnitTests` shows `-u bob`, with a fake prompt answering
      `secret`, gives user `bob` and password `secret`, and that the fake received the
      prompt text `Enter host password for user 'bob':` exactly once.
- [ ] A test shows `--user ""` prompts with `Enter host password for user '':`.
- [ ] A test shows `-u bob:secret` and `-u bob:se:cret` never call the prompt and keep the
      BL-038 split (passwords `secret` and `se:cret`).
- [ ] A test states the behaviour for `-u bob:` that local curl 8.21.0 was measured to
      show, and the measurement is recorded in `Notes`.
- [ ] A test shows a command line with no `-u` never calls the prompt.
- [ ] No code in `Curl.Cli.UnitLibrary` other than the BCL-backed prompt calls `Console`.
- [ ] `dotnet build Curl.Cli.UnitLibrary -warnaserror` is clean and
      `dotnet test Curl.Cli.UnitTests --filter "TestCategory!=Integration"` is green.

## Notes

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
