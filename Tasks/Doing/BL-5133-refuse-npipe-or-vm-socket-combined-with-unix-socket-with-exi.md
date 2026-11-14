---
id: BL-5133
title: Refuse --npipe or --vm-socket combined with --unix-socket with exit 2
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-5155]
touches: [Curl.Console/CurlComposition.cs, Curl.Console.UnitTests/ExtensionConnectionHookTests.cs, Curl.Extension.Composition.UnitLibrary/ExtensionConnectionHookCatalogue.cs, Curl.Extension.Composition.UnitTests/ConnectionOptionConflictTests.cs]
requirement: none
created: 2026-11-13
completed:
---
# BL-5133 — Refuse --npipe or --vm-socket combined with --unix-socket with exit 2

## Goal

`--npipe` or `--vm-socket` given together with curl's `--unix-socket` or `--abstract-unix-socket` exits 2 naming both options, and nothing is connected.

## Context

From the outside fitness review of 2026-11-13 (commit 56cb97ede, `preview` build), finding EXT-056 (Low, Confirmed). Part B: judged against the port's own docs and specs, not curl.

**Repro (PowerShell).**
1. Run a byte-mode named-pipe server on `\\.\pipe\b3pipe28` that answers `HTTP/1.1 200 OK` with body `via-pipe`.
2. `curl -sS -m 5 --npipe '\\.\pipe\b3pipe28' --unix-socket 'C:\nosuch.sock' http://example.invalid/plain`
3. `curl -sS -m 5 --vm-socket 3:1234 --unix-socket 'C:\nosuch.sock' http://127.0.0.1:28000/plain`

**Expected.** ADR-1021 and ADR-1109 present `--npipe` and `--vm-socket` as counterparts of `--unix-socket`, each supplying the transport. The port already refuses two extension connection options with exit 2 (ADR-1014: "the options --npipe and --vm-socket each supply the connection ... give only one of them"; the message is built in `Curl.Extension.Abstractions.UnitLibrary/Connection/ConnectionHooks.cs`). The same refusal should cover curl's own `--unix-socket` and `--abstract-unix-socket`: exit 2 with a message naming `--npipe` (or `--vm-socket`) and `--unix-socket`.

**Actual.** Step 2 prints `via-pipe`, exit 0 (`--unix-socket` ignored); step 3 tries the vsock and exits 7 with no mention of the unix socket.

**Where to start.** The ADR-1014 conflict check: `Curl.Extension.Composition.UnitLibrary/ExtensionConnectionHookCatalogue.cs`, which builds the hook list the abstractions' `ConnectionHooks` checks. The hook needs to see whether the option group carries curl's unix-socket setting; find where that setting is read (the Cli options or the transfer request) and pass it to the check without making Composition or an extension reference an Original protocol library. If the seam needs a change in `Curl.Extension.Abstractions.UnitLibrary/Connection/ConnectionHooks.cs`, add it to `touches` first.

## Acceptance criteria

- [ ] A new test file `Curl.Extension.Composition.UnitTests/ConnectionOptionConflictTests.cs` proves `--npipe` with `--unix-socket`, `--npipe` with `--abstract-unix-socket`, `--vm-socket` with `--unix-socket` and `--vm-socket` with `--abstract-unix-socket` each fail with an exception that surfaces as `CurlExitCode` 2 and a message naming both options (asserted exactly), before any connection opener is called.
- [ ] Regression tests prove each of `--npipe`, `--vm-socket` and `--unix-socket` alone still works, and `--npipe` with `--vm-socket` keeps its existing refusal text.
- [ ] Tests are platform-neutral (fake openers only; no real pipe, vsock or unix socket).
- [ ] `--extended-help` and `--ai-help` stay right (the npipe and vm-socket text says they cannot be combined with `--unix-socket`), and ADR-1014 or a new ADR marked "Decided by Claude under Stewart's delegation" records the rule.
- [ ] `dotnet build Curl.Extension.Composition.UnitLibrary -warnaserror` is clean, and `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Mock servers must be PowerShell, a C# file-based app (`dotnet run tool.cs`) or `Record-CurlExchange.ps1`, never Python. The unit tests need no server.
- Extension isolation holds: an extension never references another extension or an Original protocol library.
- Review acceptance check, as a hand check: steps 2 and 3 exit 2 with a message naming `--npipe` (or `--vm-socket`) and `--unix-socket`, and nothing is connected.

## Log

- 2026-11-13: Created.
- 2026-11-13: Backlog -> Doing. Criteria at claim: de7a2e61 f49c69db 45adc330 8d0ca22a 73f9114a.

## Notes

- Finding (turn 8, no code edited): the ADR-1014 conflict is raised in `Curl.Console/CurlComposition.cs` `ExtensionConnectionHooksOf`, the only place that sees `CommandLineOptions.UnixSocketPath` (covers both `--unix-socket` and `--abstract-unix-socket`; `UnixSocketIsAbstract` tells which). Plan: after `ConnectionHooks.ActivatedBy`, when the hook is non-null and `UnixSocketPath` is set, throw `ExtensionOptionConflictException("the options --npipe and --unix-socket each supply the connection ...")` naming the hook's option and the actual curl option; no seam change in Abstractions. Tests go in `Curl.Console.UnitTests` (Composition.UnitTests cannot see CommandLineOptions); also update the npipe/vm-socket help text and ADR-1014.
- Requeued: BL-5155 (Doing) holds Curl.Console and Curl.Console.UnitTests. Remove nothing; the board offers this again once BL-5155 is Done.
- 2026-11-13: Doing -> Backlog. Needs Curl.Console/CurlComposition.cs and its tests, held by BL-5155 (Doing); depends-on BL-5155. Plan in Notes.
- 2026-11-13: Backlog -> Doing. Criteria at claim: de7a2e61 f49c69db 45adc330 8d0ca22a 73f9114a.
