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
completed: 2026-09-26
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

- [x] A test in `Curl.Cli.UnitTests` shows `-u bob`, with a fake prompt answering
      `secret`, gives user `bob` and password `secret`, and that the fake received the
      prompt text `Enter host password for user 'bob':` exactly once.
- [x] A test shows `--user ""` prompts with `Enter host password for user '':`.
- [x] A test shows `-u bob:secret` and `-u bob:se:cret` never call the prompt and keep the
      BL-038 split (passwords `secret` and `se:cret`).
- [x] A test states the behaviour for `-u bob:` that local curl 8.21.0 was measured to
      show, and the measurement is recorded in `Notes`.
- [x] A test shows a command line with no `-u` never calls the prompt.
- [x] No code in `Curl.Cli.UnitLibrary` other than the BCL-backed prompt calls `Console`.
- [x] `dotnet build Curl.Cli.UnitLibrary -warnaserror` is clean and
      `dotnet test Curl.Cli.UnitTests --filter "TestCategory!=Integration"` is green.

## Notes

Measured with the local curl 8.21.0 (`timeout 3 /mingw64/bin/curl <arguments> </dev/null`, reading
standard error) on 2026-09-26:

- `-u bob bogus://x` writes `Enter host password for user 'bob':` (no space, no newline) and waits.
- `-u bob: bogus://x` never prompts: exits 1 with `curl: (1) Protocol "bogus" not supported`.
  So `-u bob:` is user `bob`, empty password, no prompt; `Parse_UserThatCurlDoesNotPromptFor_NeverPrompts` pins it.
- `-u 'bob;opt'` prompts for `'bob'` (login options not shown); `-u ';opt'` never prompts.
- `-u bob -u alice` prompts once, for `'alice'`; two URLs still prompt once with the plain prompt.
- `-u bob` with no URL prompts (then reports no URL); `-u bob --bogus` is refused without prompting.
  So the prompt runs after the whole argument loop, only when nothing was refused, before the no-URL check.

Not measured (needs a person typing at a console, impossible in an unattended lane): whether a newline
follows the entered password, and what curl does when the console cannot be read.

Choices made (unattended defaults):

- `IPasswordPrompt.ReadPassword(prompt)` is the seam; `CommandLineParser.Parse(arguments, pathExists,
  passwordPrompt)` is the new entry point, and BL-080 should extend it rather than add another overload.
  `Parse(arguments)` and `Parse(arguments, pathExists)` use `ConsolePasswordPrompt.ForProcessConsole`, so
  `Curl.Console` prompts for real with no change to that project.
- `ConsolePasswordPrompt` follows curl's Windows `getpass_r` (src/tool_getpass.c): keys read without echo
  until CR or LF, backspace erases, then a newline to standard error. The newline is taken from curl's
  source, not measured.
- An unreadable console (`Console.ReadKey` throws `InvalidOperationException` when input is redirected)
  ends the password with what was typed so far, usually empty. curl on Windows reads the console with
  `_getch` even when standard input is redirected; reading `CONIN$` directly is left for later if needed.
- Coverage: every line and branch of the new code is covered except the one-line
  `() => Console.ReadKey(intercept: true)` lambda in `ConsolePasswordPrompt.ForProcessConsole`, which cannot
  run without a real console. The key loop takes an injected `Func<ConsoleKeyInfo>` and `TextWriter` so it
  is fully tested.
- The `;` login-options rule (no prompt for a leading `;`, prompt shows the user up to `;`) was included
  because it was measured and costs two lines.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. -u user with no colon prompts for the password through IPasswordPrompt with curl's exact prompt text
