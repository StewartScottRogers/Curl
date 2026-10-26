---
id: BL-3854
title: Tests that parse -u or --proxy-user without a password inject a fake IPasswordPrompt, never the process console
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Cli.UnitTests/CaCertificateEnvironmentTests.cs, Curl.Cli.UnitTests/Cgr020Tests.cs, Curl.Cli.UnitTests/CommandLineHttp3RefusalTests.cs, Curl.Cli.UnitTests/CommandLineNoFunctionOptionTests.cs, Curl.Cli.UnitTests/CommandLineSchannelBuildRefusalTests.cs, Curl.Cli.UnitTests/CommandLineTransferEncodingOptionTests.cs, Curl.Cli.UnitTests/Http2OnWindowsTests.cs, Curl.Cli.UnitTests/OpenSslBuildParser.cs, Curl.Cli.UnitTests/QuicBuildParser.cs, Curl.Console.UnitTests/CurlCompositionProxyTests.Http2Tunnel.cs, Curl.Console.UnitTests/OpenSslBuildParser.cs, Curl.Console.UnitTests/QuicBuildParser.cs, Curl.Engine.UnitTests/EngineCompositionProxyTests.cs]
requirement: none
created: 2026-10-26
completed:
---
# BL-3854 — Tests that parse -u or --proxy-user without a password inject a fake IPasswordPrompt, never the process console
## Goal

No fast test reaches `ConsolePasswordPrompt.ForProcessConsole` (or `CommandLineParser.Parse` overloads that default to it) with a `-u`/`--proxy-user` user that has no password, so the fast tests never wait on the console once BL-3573 makes the Windows prompt read `CONIN$`.

## Context

BL-3573 (CGR-044, ADR-0819) makes the Windows password prompt read the console input buffer through a new `WindowsConsolePasswordInput` (`CreateFileW("CONIN$")`, `SetConsoleMode`, `ReadConsoleW`), as curl 8.22.0's `_getch` does, instead of `Console.ReadKey`, which threw on a redirected standard input and made the prompt return an empty password at once. With that change applied (BL-3573's stash), `dotnet test Curl.Cli.UnitTests --filter "TestCategory!=Integration"` on a lane ran past 60 minutes: some tests parse a password-less `-u` through `ForProcessConsole` and now wait on the lane's console. On a host with no console `CreateFileW` fails and the prompt still returns empty, so CI may not hang, but lanes do.

Find the tests that prompt: apply BL-3573's change (or temporarily make `ForProcessConsole` throw) and run `dotnet test <project> --filter "TestCategory!=Integration" --blame-hang-timeout 60s` for Curl.Cli.UnitTests, Curl.Console.UnitTests and Curl.Engine.UnitTests. Files that pass `ForProcessConsole` today are in `touches`; tests calling `CommandLineParser.Parse(arguments)` (66 files in Curl.Cli.UnitTests, 10 in Curl.Console.UnitTests, 25 in Curl.Engine.UnitTests) also reach it, so add to `touches` each further file the hang run names. Give each prompting test a fake `IPasswordPrompt` (one returning a fixed or empty password) so its assertion is unchanged.

## Acceptance criteria

- [ ] With `ConsolePasswordPrompt.ForProcessConsole` reading the real console (BL-3573's change) the fast tests of Curl.Cli.UnitTests, Curl.Console.UnitTests and Curl.Engine.UnitTests finish without waiting for console input.
- [ ] Every changed test still asserts what it asserted before.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Filed by BL-3573's run, which found the hang; BL-3573 depends on this task.

## Log

- 2026-10-26: Created.
