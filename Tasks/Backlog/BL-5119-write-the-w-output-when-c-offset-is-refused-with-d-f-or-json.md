---
id: BL-5119
title: Write the -w output when -C offset is refused with -d, -F or --json
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console/CurlCommandRunner.cs, Curl.Cli.UnitLibrary/CommandLineRefusal.cs, Curl.Console.UnitTests/ContinueAtWithBodyWriteOutTests.cs]
requirement: none
created: 2026-11-13
completed:
---
# BL-5119 — Write the -w output when -C offset is refused with -d, -F or --json

## Goal

When `-C <offset>` is combined with `-d`, `-F` or `--json` and curl refuses with exit 2, the `-w` write-out is still printed (for example `[W 000 2]`), as upstream curl does.

## Context

From the outside fitness review of 2026-11-13 (commit 56cb97ede, `preview` build), finding STD-069 (Low, Confirmed against upstream curl on Windows and Linux and the port, repeated). Area: HTTP, `-w` on the "cannot mix --continue-at" refusal.

**Repro.** `curl -sS -C 100 -d x -w "[W %{http_code} %{exitcode}]" http://127.0.0.1:1/` (also with `-F a=b`, `-X POST --json '{}'`, and `%{onerror}`).

**Expected (upstream curl).** stderr `curl: cannot mix --continue-at with --data` and `curl: (2) Failed initialization`, stdout `[W 000 2]`, exit 2.

**Actual.** Same stderr and exit 2, nothing on stdout. Other setup failures already write `-w` (for example `-T nofile` gives `[W 000 26]`).

**Where to start.** `Curl.Cli.UnitLibrary/CommandLineRefusal.cs`, `ContinueAtWithBody` (`foundAtTransferSetup: true`), handled in `Curl.Console/CurlCommandRunner.cs` around lines 1056 and 6167; that path skips the write-out. Route it through the write-out the way the `-T nofile` setup failure is.

## Acceptance criteria

- [ ] New tests in `Curl.Console.UnitTests/ContinueAtWithBodyWriteOutTests.cs` prove that `-C 100 -d x -w "[W %{http_code} %{exitcode}]"` writes `[W 000 2]` to stdout, `curl: cannot mix --continue-at with --data` and `curl: (2) Failed initialization` to stderr, and exits 2; the same for `-F a=b` and `--json '{}'` (message names the respective option as today).
- [ ] A test proves `%{onerror}` output is written for the same refusal.
- [ ] A regression test proves the refusal still writes nothing to stdout when no `-w` is given, and that no network connection is opened.
- [ ] Tests are platform-neutral.
- [ ] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Mock servers must be PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md).
- Review acceptance check, as a hand check: the repro prints `[W 000 2]` on stdout and exits 2.

## Log

- 2026-11-13: Created.
