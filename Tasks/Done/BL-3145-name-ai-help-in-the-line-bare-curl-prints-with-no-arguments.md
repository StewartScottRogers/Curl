---
id: BL-3145
title: Name --ai-help in the line bare curl prints with no arguments
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Cli.UnitLibrary/CommandLineRefusal.cs, Curl.Cli.UnitTests, Curl.Console.UnitTests/CurlCommandRunnerTests.cs, Curl.Console.UnitTests/CurlCommandRunnerConfigFileTests.cs, Documentation/Planning/Decisions]
requirement: none
created: 2026-10-22
completed: 2026-10-22
---
# BL-3145 — Name --ai-help in the line bare curl prints with no arguments

## Goal

Running `curl` with no arguments prints `curl: try 'curl --help', 'curl --manual' or 'curl --ai-help' for more information` (exit code unchanged), so an AI agent that runs bare `curl` learns that `--ai-help` exists.

## Context

Stewart asked for this on 2026-10-22: "add the '--ai-help' to the default curl.exe -> curl: try 'curl --help' or 'curl --manual' for more information". `--ai-help` is Curl's Markdown help for AI agents (BL-911, ADR-0224).

The line is `CommandLineRefusal.TryHelpLine` (`Curl.Cli.UnitLibrary/CommandLineRefusal.cs:25`). Curl prints it after every command-line error, as real curl does, and upstream cases 322, 411, 422, 481, 482, 484, 485, 746 and 1179 pin those error outputs byte for byte. Only the no-arguments case changes. Every error path keeps curl's exact line, so the scripts and upstream cases that parse curl's errors still cannot tell Curl from curl. This is a deliberate, Stewart-requested difference from curl 8.22.0 in one output only. Record it in an ADR (Documentation/Planning/Decisions, next free number, "Decided by Stewart; scope decided by Claude under Stewart's delegation").

Measure first what Curl prints today for bare `curl` (stderr bytes and exit code), and keep everything else in that output byte-identical. If a test pins the no-argument output, update it there and nowhere else.

## Acceptance criteria

- [x] `curl` with no arguments writes `curl: try 'curl --help', 'curl --manual' or 'curl --ai-help' for more information` on that line of stderr, with the same exit code and the same other bytes as before. A unit test in `Curl.Cli.UnitTests` pins it.
- [x] Every other output that ends in the try-help line, such as an unknown option or a missing option argument, still prints `curl: try 'curl --help' or 'curl --manual' for more information` exactly. A unit test pins one such case beside the new one, and the upstream cases on `PassingUpstreamCases.txt` still pass.
- [x] `--ai-help`'s own index mentions that bare `curl` points to it, if the index lists entry points; otherwise it is unchanged.
- [x] An ADR records the deliberate difference from curl 8.22.0 and why it is limited to the no-argument case.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Bare curl is every empty command line, so the two Console tests and the Cli default-config tests that pin an empty command line now pin the new line; added Curl.Console.UnitTests files to touches (no other Doing task held them).
- The --ai-help index lists no entry points, so it is unchanged. ADR-0762 records the difference. CI on Linux and macOS: change is platform-neutral, fast tests green here.

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: 63b98b66 e2aa702f cd7b69ab 3e377a02 9742157d.
- 2026-10-22: Doing -> Done. bare curl names --ai-help
