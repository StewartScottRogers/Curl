---
id: BL-3854
title: Tests that parse -u or --proxy-user without a password inject a fake IPasswordPrompt, never the process console
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Cli.UnitTests/CaCertificateEnvironmentTests.cs, Curl.Cli.UnitTests/Cgr015Tests.cs, Curl.Cli.UnitTests/Cgr050Tests.cs, Curl.Cli.UnitTests/Cgr020Tests.cs, Curl.Cli.UnitTests/CommandLineHttp3RefusalTests.cs, Curl.Cli.UnitTests/CommandLineNoFunctionOptionTests.cs, Curl.Cli.UnitTests/CommandLineSchannelBuildRefusalTests.cs, Curl.Cli.UnitTests/CommandLineTransferEncodingOptionTests.cs, Curl.Cli.UnitTests/Http2OnWindowsTests.cs, Curl.Cli.UnitTests/OpenSslBuildParser.cs, Curl.Cli.UnitTests/QuicBuildParser.cs, Curl.Console.UnitTests/CurlCompositionProxyTests.Http2Tunnel.cs, Curl.Console.UnitTests/OpenSslBuildParser.cs, Curl.Console.UnitTests/QuicBuildParser.cs, Curl.Engine.UnitTests/EngineCompositionProxyTests.cs]
requirement: none
created: 2026-10-26
completed: 2026-10-26
---
# BL-3854 — Tests that parse -u or --proxy-user without a password inject a fake IPasswordPrompt, never the process console
## Goal

No fast test reaches `ConsolePasswordPrompt.ForProcessConsole` (or `CommandLineParser.Parse` overloads that default to it) with a `-u`/`--proxy-user` user that has no password, so the fast tests never wait on the console once BL-3573 makes the Windows prompt read `CONIN$`.

## Context

BL-3573 (CGR-044, ADR-0819) makes the Windows password prompt read the console input buffer through a new `WindowsConsolePasswordInput` (`CreateFileW("CONIN$")`, `SetConsoleMode`, `ReadConsoleW`), as curl 8.22.0's `_getch` does, instead of `Console.ReadKey`, which threw on a redirected standard input and made the prompt return an empty password at once. With that change applied (BL-3573's stash), `dotnet test Curl.Cli.UnitTests --filter "TestCategory!=Integration"` on a lane ran past 60 minutes: some tests parse a password-less `-u` through `ForProcessConsole` and now wait on the lane's console. On a host with no console `CreateFileW` fails and the prompt still returns empty, so CI may not hang, but lanes do.

Find the tests that prompt: apply BL-3573's change (or temporarily make `ForProcessConsole` throw) and run `dotnet test <project> --filter "TestCategory!=Integration" --blame-hang-timeout 60s` for Curl.Cli.UnitTests, Curl.Console.UnitTests and Curl.Engine.UnitTests. Files that pass `ForProcessConsole` today are in `touches`; tests calling `CommandLineParser.Parse(arguments)` (66 files in Curl.Cli.UnitTests, 10 in Curl.Console.UnitTests, 25 in Curl.Engine.UnitTests) also reach it, so add to `touches` each further file the hang run names. Give each prompting test a fake `IPasswordPrompt` (one returning a fixed or empty password) so its assertion is unchanged.

## Acceptance criteria

- [x] With `ConsolePasswordPrompt.ForProcessConsole` reading the real console (BL-3573's change) the fast tests of Curl.Cli.UnitTests, Curl.Console.UnitTests and Curl.Engine.UnitTests finish without waiting for console input.
- [x] Every changed test still asserts what it asserted before.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Filed by BL-3573's run, which found the hang; BL-3573 depends on this task.
- Replaced ForProcessConsole with a per-project test fake EmptyPasswordPrompt (Curl.Cli.UnitTests/OpenSslBuildParser.cs, Curl.Console.UnitTests/OpenSslBuildParser.cs, Curl.Engine.UnitTests/EngineCompositionProxyTests.cs); added Cgr015Tests.cs and Cgr050Tests.cs to touches. Hang hunt: temporarily threw from ConsolePasswordPrompt.ReadPassword and ran the three projects; only ConsolePasswordPromptTests (the prompt's own tests, one of which, ForProcessConsole_InputRedirected_ReturnsAnEmptyPassword, calls the real prompt deliberately and belongs to BL-3573) failed, no Parse(arguments) caller reached it. Throw reverted.

## Log

- 2026-10-26: Created.
- 2026-10-26: Backlog -> Doing. Criteria at claim: ba4b4b5e 59f0897c 779790c0.
- 2026-10-26: Doing -> Done. Fast tests in Cli, Console and Engine no longer reach the process console prompt; fake IPasswordPrompt injected
