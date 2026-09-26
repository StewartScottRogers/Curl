---
id: BL-078
title: Warn when there are more output options than URLs
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-051]
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: none
created: 2026-09-26
completed: 2026-09-26
---
# BL-078 — Warn when there are more output options than URLs

## Goal

An accepted command line with more `-o`/`--output` values than URLs carries the warning
line `Warning: Got more output options than URLs`, and where curl prints it relative to
the transfer is recorded in the repository.

## Context

Follow-up of BL-037 (the table-driven parser in `Curl.Cli.UnitLibrary`). Depends on
BL-051, which adds the ordered warning-line list to `CommandLineParseResult`; this task
appends to that list rather than inventing a second channel.

Measured with the local curl 8.21.0 on Windows (2026-09-26): `curl -o f -o g URL` prints
`Warning: Got more output options than URLs` on stderr and the transfer still runs. The
condition is `CommandLineOptions.OutputFiles.Count > CommandLineOptions.Urls.Count`.

Not yet measured: whether curl prints this line before the transfer starts or after it
ends (for example, whether it comes before or after a transfer's own error message, such
as a `file://` URL naming a missing file with `-S`). That ordering decides whether the
warning belongs in the parse result (printed before the transfer) or has to be emitted
by the console layer afterwards. Measure it with the local curl first. If it is printed
before the transfer, finish it here. If it is printed after, still add the warning to the
parse result as the record of the condition, state in the XML documentation that the
console must write it after the transfer, and file a task against `Curl.Console` for the
ordering.

## Acceptance criteria

- [x] Tests in `Curl.Cli.UnitTests` assert that `-o f -o g URL` is accepted with exactly
      one warning line, `Warning: Got more output options than URLs`.
- [x] Tests assert that `-o f URL` and `-o f URL1 URL2` carry no such warning.
- [x] A test asserts the order of warning lines when both this warning and BL-051's
      flag-like file name warning apply (`-o -s -o g URL`), matching the order the local
      curl 8.21.0 prints them.
- [x] Where curl prints the line relative to the transfer is stated, with the command
      used to measure it, in the XML documentation of the member that produces it and in
      `Curl.Cli.UnitLibrary/README.md`.
- [x] If curl prints it after the transfer, a Backlog task against `Curl.Console` exists
      for writing it at that point.
- [x] `dotnet build Curl.Cli.UnitLibrary -warnaserror` is clean and
      `dotnet test Curl.Cli.UnitTests --filter "TestCategory!=Integration"` is green.

## Notes

- Measured with the local curl 8.21.0 on Windows (2026-09-26): curl prints the warning
  **after** the transfer. `curl -o f -o g file:///Z:/nx` gives
  `curl: (37) Could not open file Z:/nx`, then `Warning: Got more output options than URLs`;
  `curl -o -s -o g file:///Z:/nx` gives the file-name warning, the transfer error, then this
  warning. It is printed once however many `-o` are left over, `--url` counts as a URL, and
  `-s` anywhere on the command line drops it (`-s --no-silent` keeps it): curl checks the
  final silent state when it prints, unlike the warnings raised while reading.
- Choice: the warning goes in a new `CommandLineParseResult.WarningLinesAfterTransfers`
  list, not appended to `WarningLines`. Why: `Curl.Console/CurlCommandRunner.cs` writes
  `WarningLines` before the transfer, so appending there would print it in the wrong place
  until the console changed, and would make that property's "written before anything else"
  documentation false. It is still on the parse result, as the task asked, and the order
  test asserts both lists for `-o -s -o g URL`. The text is `CommandLineWarning.MoreOutputOptionsThanUrls`.
- `-O`/`--remote-name` also counts as an output option in curl (`curl -o f -O URL` warns),
  but `-O` is not in the option table yet; whoever adds it must count it here.
- Filed BL-106 against `Curl.Console` to write the list after the transfers.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. An accepted command line with more -o values than URLs carries 'Warning: Got more output options than URLs' in CommandLineParseResult.WarningLinesAfterTransfers
