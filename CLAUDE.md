# Curl — Solution Instructions for Claude Code

## Overview
Curl is a C# solution maintained in Microsoft Visual Studio.
Repository: https://github.com/StewartScottRogers/Curl

Curl is a drop-in replacement for the `curl` command-line tool, written in C# on
.NET 10: the same options, exit codes and output bytes, so existing scripts cannot
tell which binary they invoked. Every protocol lives in its own class library behind
injected interfaces so it can be unit tested without a network. See
`Documentation/Product/Product-Overview.md`.

## Toolchain
- .NET Software Development Kit 10 (see `global.json` once added). Target framework: `net10.0` unless a project states otherwise.
- The solution file lives at the repository root (`Curl.slnx` preferred, `Curl.sln` acceptable).
- Shell is Windows. Use PowerShell or `cmd` syntax, backslash paths are fine.

## Build and test commands
- Build: `dotnet build`
- Test (fast, default): `dotnet test --filter "TestCategory!=Integration"`
- Test (everything): `dotnet test`
- Format: `dotnet format`
- Measure quality: `powershell -NoProfile -File Measure-CodeQuality.ps1`

Always build and run the fast tests before declaring a task finished.

## Quality gates
Every `*.UnitLibrary` (and `Curl.Console`) is held to 100% line coverage, 100% branch
coverage, cyclomatic complexity of at most 10 per method, and a CRAP score of at most 30.
All four are measured with tooling the solution already has - the coverage collector the
MSTest meta-package brings, and the SDK's own `CA1502` analyzer - so no package is needed
for any of it. Complexity is enforced at build time: the threshold lives in
`CodeMetricsConfig.txt` and warnings are errors, so a method at 11 breaks the build. The
`coverage-auditor` agent measures the rest and files the gaps as tasks. Thresholds in
`CodeMetricsConfig.txt` are Stewart's to change; never raise one to make code pass.

## Task board
Work is tracked as Markdown files in the `Tasks` shared project, one file per task, and
the folder a task sits in is its status: `Backlog`, `Doing`, `Blocked`, `Deferred`,
`Done`, with timestamped archive folders under `Done`. Read
`.claude/skills/task-board/SKILL.md` before creating, moving or editing a task, and move
tasks only with its script. `/task-plan` files tasks, `/task-run` works them, and
`/task-status` and `/task-archive` keep the board tidy.
## Repository layout
Flat and linear. Every project is a directory immediately under the repository root.
There is no `src/` and no `tests/`; do not create them.
```
Curl/
├── Curl.slnx
├── Curl.Core.UnitLibrary/        ← production library
├── Curl.Core.UnitTests/          ← its tests, immediately beside it
├── Curl.Protocol.Http.UnitLibrary/
├── Curl.Protocol.Http.UnitTests/
├── ...                           ← 48 projects, one flat alphabetical run
├── Documentation/                ← shared project (docs and planning)
├── Tasks/                        ← shared project (task board)
├── data/                         ← local runtime data (gitignored, never read or modify)
└── .claude/                      ← Claude Code configuration
```

### Project naming
- Production library: `Curl.<Area>.UnitLibrary`, protocols `Curl.Protocol.<Name>.UnitLibrary`.
- Tests: the same name with `.UnitTests` instead of `.UnitLibrary`.
- The executable is `Curl.Console` — no `.UnitLibrary` suffix, because it is not a
  library. It is the only exception.
- Names sort so each `.UnitTests` lands directly after the library it tests. Keep it
  that way.

In `Curl.slnx`, projects are listed as one flat run with no solution folders around
them. The `Solution Items` and `Scripts` solution folders hold loose files only.

Each project folder may contain its own `CLAUDE.md` with project-specific rules; follow it when working in that folder.

## Solution-wide conventions
- **Base class library only.** Write against `System.*`. Sockets, TLS, HTTP, DNS,
  compression, JSON and argument handling are all in the BCL already, and
  `Curl.Console` publishes native AOT, where every dependency is a trim risk. The
  one package in `Directory.Packages.props` — Microsoft's `MSTest` meta-package —
  is the test harness and is the only approved dependency in the solution. Adding a
  second needs Stewart's explicit approval, asked for *before* the reference is
  added — hand-roll the small piece needed, or stop and ask. Test projects use
  MSTest, the framework in the .NET SDK; no third-party test, mocking or assertion
  library is permitted.
- Nullable reference types enabled, warnings treated as errors.
- File-scoped namespaces; namespace matches folder path.
- Central package management through `Directory.Packages.props`; never put a `Version` attribute on a `PackageReference` in a project file.
- Shared build settings go in `Directory.Build.props`, not individual project files.
- Async all the way; no `.Result` or `.Wait()`.
- Register new services with dependency injection; no static service locators.
- Protocol libraries reference `Curl.Protocol.Abstractions.UnitLibrary` and never
  each other. A protocol referencing another protocol is a build break, not a smell.
- Protocol handlers never construct a `Socket`, `SslStream` or `HttpClient`; they
  receive `IConnection`. This is what keeps protocol tests off the network.
- Inject `TimeProvider` for anything time-dependent; never `Thread.Sleep`.
- Published native-AOT: no reflection-based DI scanning, no dynamic code paths.

## Things to never do
- Do not edit anything under `bin/`, `obj/`, `.vs/`, or `data/`.
- Do not hand-edit generated migration files.
- Do not add a NuGet package. Ask first; see the base-class-library-only rule above.
  No mocking library, no fluent-assertion library, no parser library, no JSON library.
- Do not change `RunClaude.cmd` or `hrdrClaudeNative.cmd` unless asked.
