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

## Git and GitHub
Reversible git and gh work is delegated to github-operator: status, commits, rebases,
explaining conflicts, pull request bodies, Actions triage, branch cleanup.

Committing and pushing to a feature branch is automatic and needs no confirmation. Once
`dotnet build` is clean and the fast tests are green, commit by logical unit and push;
report it afterwards rather than asking first.

One standing exception: the `gource` branch holds only the latest showcase render (the Gource video and the coverage report) and is
force-pushed on every render by `.github/workflows/gource.yml` (owned by the
`showcase-publisher` agent). That force push, to that branch only, needs no confirmation.

A second standing exception (Stewart, 2026-09-27): at the end of every dark factory
shift, `RunDarkFactory.ps1` merges its branch into `master` through a pull request, but
only when the `CI` workflow passed on Windows, Linux and macOS for the exact commit being
merged. That merge needs no confirmation; a red or unfinished CI run means no merge.

Ask first for: a force push or any rewrite of already-pushed history, any other merge to `master`,
a tag or a release, creating a repository or changing its visibility, and deleting a
branch. Irreversible GitHub actions are run directly and not through the subagent, which
by design refuses authorization relayed to it in a prompt.

## Task board
Work is tracked as Markdown files in the `Tasks` shared project, one file per task, and
the folder a task sits in is its status: `Backlog`, `Doing`, `Blocked`, `Deferred`,
`Done`, with timestamped archive folders under `Done`. Read
`.claude/skills/task-board/SKILL.md` before creating, moving or editing a task, and move
tasks only with its script. `/task-plan` files tasks, `/task-run` works them, and
`/task-status` and `/task-archive` keep the board tidy.

## Decisions
Stewart has delegated design and behaviour decisions to Claude (2026-09-26): which curl
build to match, option limits, how an unsupported option behaves, test approach, URL
model, licence-level project choices and the like. Do not ask him and do not block a task
for one. Decide by the standing rules - match the platform's curl (the Schannel build on
Windows, the OpenSSL build on Linux and macOS), measure real curl before pinning output
text, base class library only, simplest thing that stays a drop-in replacement - record
the decision and why in an ADR marked "Decided by Claude under Stewart's delegation", and
tell him afterwards. Still his, and still asked first: adding a package, changing a
threshold in `CodeMetricsConfig.txt`, and the irreversible git and GitHub actions listed
above.

The default answer to every question is the same (Stewart, 2026-09-28): do what a complete
reimplementation of curl in C# needs. Nothing is left out, deferred or refused because it
is hard or because the BCL has no primitive for it - QUIC and HTTP/3, SSH's Curve25519,
Ed25519 and ChaCha20-Poly1305, Kerberos and the like are built by hand. Isolate each such
hand-built piece in its own `Curl.<Area>.UnitLibrary` with its own `.UnitTests` project,
held to the same quality gates. Hand-writing it is the answer, never a package, and never
an ADR that decides a feature stays out.

## Dark factory
`RunDarkFactory.cmd` works the board unattended: each ready task goes to a headless
`/task-run`, and anything that needs Stewart ends in `Blocked` with an alarm at the end
of the shift. `-Lanes N` runs N tasks at once, each in its own git worktree beside the
checkout (`<repo>.lanes\lane-<n>`); the board never gives two lanes tasks whose
`touches` overlap, and each lane rebases, rebuilds, tests and pushes its own work, one
lane at a time. Running out of tokens is not a stall: the shift announces it with the
reset time, waits (the wait does not count against `-Hours`), warns a minute before the
new session and reruns the cut-off task. See the script's header for the details.

When Claude starts a shift it always passes `-NewTab`, e.g.
`RunDarkFactory.cmd -NewTab -Lanes 3 -Continuous`; `-Continuous` makes a shift that
ends with work still ready start the next one itself. A shift ends before the tokens run
out: once 85% of the 5-hour window (`-StopAtUsage`) or 97% of the weekly window
(`-StopAtWeeklyUsage`) is used, lanes claim
nothing new, finish what they hold and push; the next shift waits for a fresh 5-hour
window, and a used-up weekly window raises the alarm. Inside herdr (`HERDR_ENV=1`) that opens the shift
and each of its lanes as herdr tabs in the current workspace; outside herdr, as console
windows. Never start one with `Start-Process` or a bare background command: Stewart
watches shifts in herdr. Stop a shift by closing its tabs (or killing its process tree).
Leave its tasks in `Doing` and its lane worktrees as they are: the next shift adopts each
stopped lane and resumes its task from the work in place. While a shift runs, its
coordinator restarts any lane whose process dies, and lanes wait out the usage limit and
carry on when tokens return - nobody needs to restart them.

Every session, lanes included, whispers milestones to Stewart through the PostToolUse hook
`.claude/hooks/whisper-milestone.ps1`: a task moved to Done, a commit made, a branch
deleted - quietly, in Windows' Zira voice, one phrase at a time.
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
- **Say what it does, do what it says.** Every name - project, file, type, member,
  parameter, test, script, task - says exactly what the thing does, and the thing does
  nothing its name hides. No generic names (`Process`, `Handle`, `Manager`, `Helper`,
  `Utils`, `Data`) where a specific one exists; one concept has one name, the one in
  `Documentation/Wiki/Glossary.md` (not yet written; `align-and-document` starts it on
  its first run). Documents obey the same rule: every statement is true
  of the code as it is now, and intent is written as intent. A misaligned name or document
  is a defect, because it is how an agent reading this repository comes to believe
  something false. The `align-and-document` agent owns this.
- **No Python, committed or throwaway.** Scripts, one-liners, file edits and loopback
  test servers are PowerShell (or a C# file-based app, `dotnet run tool.cs`). Measure real
  curl with `Record-CurlExchange.ps1` - it runs the loopback server, records the request
  bytes, stdout, stderr and exit code - and extend it when it falls short, rather than
  writing a server of your own. Edit files with the Edit tool, not generated scripts.
- **Base class library only.** Write against `System.*`. Sockets, TLS, HTTP, DNS,
  compression, JSON and argument handling are all in the BCL already, and
  `Curl.Console` publishes native AOT, where every dependency is a trim risk. The
  one package in `Directory.Packages.props` — Microsoft's `MSTest` meta-package —
  is the test harness and is the only approved dependency in the solution. Adding a
  second needs Stewart's explicit approval, asked for *before* the reference is
  added — hand-roll the small piece needed, or stop and ask. Test projects use
  MSTest, the framework in the .NET SDK; no third-party test, mocking or assertion
  library is permitted.
- **Tests pass on Windows, Linux and macOS.** CI runs the fast tests on all three, and
  a red Linux or macOS job blocks the dark factory's merge to `master`, but lanes only
  test on Windows - so write every test to be platform-neutral. No drive-letter URL
  (`file:///C:/...`) or other Windows-only path, error text, certificate or key outside
  a test marked `[OSCondition(OperatingSystems.Windows)]`; off Windows curl rejects a
  drive letter in a `file://` URL, so such a test fails there with exit 3. Use a
  drive-less URL such as `file:///dir/x` unless the drive letter is what the test is
  about. Where curl's answer differs by platform, pin each platform's answer in its own
  test (`[OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]` for the other).
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
- Published native-AOT: no reflection-based DI scanning, no dynamic code paths. Every
  production project is AOT-compatible, `Directory.Build.props` sets it, and its
  `VerifyAotCompatibility` target fails the build if a project overrides it - so a new
  project is covered without touching its csproj. Test projects are exempt on purpose:
  MSTest discovers tests by reflection. `dotnet publish Curl.Console` produces a native
  binary by default and needs
  `C:\Program Files (x86)\Microsoft Visual Studio\Installer` on PATH for vswhere.

## Things to never do
- Do not edit anything under `bin/`, `obj/`, `.vs/`, or `data/`.
- Do not hand-edit generated migration files.
- Do not add a NuGet package. Ask first; see the base-class-library-only rule above.
  No mocking library, no fluent-assertion library, no parser library, no JSON library.
- Do not change `RunClaude.cmd` or `hrdrClaudeNative.cmd` unless asked.
