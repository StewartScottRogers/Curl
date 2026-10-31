# Curl - rules for a dark factory lane

A lane's headless run loads this file in place of the repository's root `CLAUDE.md`
(BL-3001): it holds every rule of that file a lane needs, and leaves out the operator
material (herdr, audio, releases, the audit and gap offices' operation, git exceptions
that only an interactive session uses). Where the lane prompt or a skill says "CLAUDE.md",
read it as these rules. A project folder's own `CLAUDE.md` still holds that project's
rules: read it before changing anything in that folder.

## What Curl is
Curl is a drop-in replacement for the `curl` command-line tool, written in C# on .NET 10:
the same options, exit codes and output bytes, so existing scripts cannot tell which
binary they invoked. Every protocol lives in its own class library behind injected
interfaces so it can be unit tested without a network.

## Toolchain and commands
- .NET SDK 10 (`global.json` pins it). Target framework `net10.0` unless a project says
  otherwise. Solution file `Curl.slnx` at the repository root. The shell is the host's: Windows
  PowerShell on Windows, PowerShell 7 (`pwsh`) on Linux and macOS; scripts build paths with
  `Join-Path` or forward slashes (BL-3934).
- Build: `dotnet build`
- Fast tests (default): `dotnet test --filter "TestCategory!=Integration"`
- Integration tests only: `dotnet test --filter "TestCategory=Integration"`
- Long-running tests: `dotnet test --filter "TestCategory=LongRunning"` with
  `CURL_RUN_LONG_RUNNING_TESTS=1`; without it they show as skipped.
- Format: `dotnet format`. Quality: `powershell -NoProfile -File Measure-CodeQuality.ps1` (`pwsh` instead of `powershell` on Linux and macOS).
- Always build and run the fast tests before declaring a task finished.

An Integration test touches something real outside the process (a socket, the disk, the
OS, a native API, a system agent), carries `[TestCategory("Integration")]` and lives only
in a `Curl.<Area>.IntegrationTests` project, where every test carries it. A test whose
only real resources are files in a temporary directory it creates and deletes, or a
loopback socket it opens and closes without sending a byte, is a unit test. A slow test
that is pure computation stays in its `*.UnitTests` project as `LongRunning`.
`Directory.Build.props` fails the build when either rule is broken (ADR-0421).

## Quality gates
Every `*.UnitLibrary`, `Curl.Console`, `Curl.Api`, `Curl.Mcp` and `Libcurl` is held to
100% line coverage, 100% branch coverage, cyclomatic complexity at most 10 per method and
a CRAP score at most 30. Complexity is enforced at build time (`CodeMetricsConfig.txt`,
warnings are errors); CI's quality job runs `Measure-CodeQuality.ps1` (on coverage three
parallel shard jobs collect, BL-3464) and fails on any
member outside a gate. Coverage is measured from the fast tests only, so a line only an
Integration test reaches counts as uncovered. Never change a threshold in
`CodeMetricsConfig.txt`: that is Stewart's, and a lane blocks to ask.

## Decisions
Design and behaviour decisions are delegated to Claude: decide, never block on one.
Match the platform's curl (Schannel build on Windows, OpenSSL build on Linux and macOS),
measure real curl before pinning output text, base class library only, simplest thing
that stays a drop-in replacement. Record the decision and why in an ADR marked "Decided
by Claude under Stewart's delegation". Only a new package or a threshold change needs
Stewart. When two lanes pick the same ADR number, the integrating lane renumbers its own
new ADR (file name, heading, README row and citations on the lines it added); never edit
an ADR number of another lane's file to avoid a clash (ADR-0818). Nothing is left out because it is hard or the BCL lacks a primitive: QUIC,
HTTP/3, SSH's Curve25519, Ed25519, ChaCha20-Poly1305, Kerberos and the like are built by
hand, each in its own `Curl.<Area>.UnitLibrary` with its own `.UnitTests` project, held
to the same gates. Never a package, never an ADR that decides a feature stays out.

## Repository layout and naming
Flat: every project is a directory immediately under the repository root. No `src/`,
no `tests/`. `Documentation/` and `Tasks/` are shared projects; `data/` is never read or
modified; never edit anything under `bin/`, `obj/` or `.vs/`.
- Production library `Curl.<Area>.UnitLibrary`, protocols `Curl.Protocol.<Name>.UnitLibrary`.
- Extensions (what Curl adds beyond upstream curl, ADR-0999): `Curl.Extension.<Name>.UnitLibrary`,
  in `/Extensions/<Name>/` in `Curl.slnx` (one single-line `<Folder>` holding both projects, added directly
  under the marker comment there; Composition references extensions by wildcard, ADR-1003), registered by one line in
  `Curl.Extension.Composition.UnitLibrary/ExtensionCatalogue.cs`. It references only
  `Curl.Protocol.Abstractions`, the shared layers and `Curl.Extension.Abstractions`; nothing but the
  hosts and the extensions references it (the build enforces both). It never changes curl's
  behaviour, and is tested against its specification, not against curl.
- Tests: the same name with `.UnitTests`; Integration tests with `.IntegrationTests`.
- Exceptions: `Curl.Console` (the executable), `Curl.Api` and `Libcurl` (programming
  interfaces), `Curl.Mcp` (MCP server host). `Curl.Api.UnitTests` tests `Curl.Api` and
  `Curl.Api.UnitLibrary`; `Curl.Mcp.UnitTests` tests `Curl.Mcp` and `Curl.Mcp.UnitLibrary`.
- Names sort so each `.UnitTests` lands directly after its library and each
  `.IntegrationTests` directly before it. `Curl.slnx` lists projects as one flat run with
  no solution folders around them; `Curl.Api*` first, `Libcurl*` after every `Curl.*`.
- New project: use the `new-project` skill.

## Solution-wide conventions
- **Say what it does, do what it says.** Every name (project, file, type, member,
  parameter, test, script, task) says exactly what the thing does, and the thing does
  nothing its name hides. No generic names (`Process`, `Handle`, `Manager`, `Helper`,
  `Utils`, `Data`) where a specific one exists; one concept has one name, the one in
  `Documentation/Wiki/Glossary.md`. Every statement in a document is true of the code as
  it is now. A misaligned name or document is a defect.
- **No Python**, committed or throwaway. Scripts and loopback servers are PowerShell or a
  C# file-based app (`dotnet run tool.cs`). Measure real curl with
  `Record-CurlExchange.ps1`, extending it when it falls short. Edit files with the Edit tool.
- **Base class library only.** Write against `System.*`. The only packages are the
  `MSTest` meta-package. Never add a package: block and ask Stewart. No mocking,
  fluent-assertion, parser or JSON library. Tests use MSTest.
- **Tests pass on Windows, Linux and macOS.** CI runs the fast tests on all three, but
  lanes test only on Windows, so every test is platform-neutral: no drive-letter URL
  (`file:///C:/...`) or other Windows-only path, error text, certificate or key outside a
  test marked `[OSCondition(OperatingSystems.Windows)]`. Use `file:///dir/x`. Where curl's
  answer differs by platform, pin each platform's answer in its own test
  (`[OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]` for the other).
- **`--ai-help` keeps pace with the options.** Every task that adds or changes a
  command-line option also keeps `curl --ai-help` (Markdown help for AI agents) right.
- **A fix found through an upstream case also gets a focused unit test** in the fixed
  library's own `.UnitTests` project (ADR-0482).
- Nullable reference types on, warnings are errors. File-scoped namespaces; namespace
  matches folder path.
- Central package management (`Directory.Packages.props`); never a `Version` on a
  `PackageReference`. Shared build settings go in `Directory.Build.props`.
- Async all the way; no `.Result` or `.Wait()`. Inject `TimeProvider`; never `Thread.Sleep`.
- Register services with dependency injection; no static service locators.
- Protocol libraries reference `Curl.Protocol.Abstractions.UnitLibrary` and never each
  other. Protocol handlers never construct a `Socket`, `SslStream` or `HttpClient`; they
  receive `IConnection`.
- Native AOT: no reflection-based DI scanning, no dynamic code paths. Every production
  project is AOT-compatible; never override that in a project file.
- Work in parallel where it is safe; new cases, stand-ins and handlers go in one file
  each that registers itself, not in a central switch or test class.

## Task board
Tasks are Markdown files in `Tasks/`; the folder is the status (`Backlog`, `Doing`,
`Blocked`, `Deferred`, `Done`). Read `.claude/skills/task-board/SKILL.md` before creating,
moving or editing a task, and move tasks only with its script
(`.claude/skills/task-board/task-board.ps1`). A task's `touches` names files or narrow
subfolders, never a whole project it only adds to. A registration table that tasks only
add lines to is one entry per line and listed in `Tasks/SharedListFiles.txt`.
A task is claimed at most twice (ADR-1155): a task split for size files every part of its
work, the rest included, as tasks of their own and goes to `Deferred`, never back to
`Backlog` to wait on its own subtasks; and the shift splits a task a run sends back to
`Backlog` a second time rather than offer it a third claim.

A CI fix (a task titled "Fix CI failure ...", "Fix CI build ..." or "Fix flaky CI test
...") goes before all other work, because a red CI blocks the shift-end merge and hides the
next failure (ADR-1158). The lane may have claimed it while another task in Doing holds a
file it needs: change that file anyway, widening `touches` if needed; never send a CI fix
back to Backlog to wait for a task in Doing to finish, since the other lane rebases over
the fix. Only a change you cannot make yourself (another task's code that must land first)
is a reason to wait, and then say which under Notes.

## Git
Commit by logical unit with Conventional Commits once the build is clean and the fast
tests are green. The lane prompt says what a lane may do beyond that (it never pushes,
pulls, rebases, merges or switches branches itself). Never force-push, tag, release,
delete a branch or merge to `master`.

## Audit and gap paths: stop
`Audit/`, `Gap/`, `.claude/agents/audit-*`, `.claude/agents/gap-*` and the guard files
(`.claude/hooks/guard-audit-paths.ps1`, `.claude/settings.json`,
`.github/workflows/ci.yml`, `.claude/skills/task-board/task-board.ps1`) belong to the
independent audit and gap analysis offices. A lane never reads or changes them; the
PreToolUse hook refuses it, the board refuses a lane filing or claiming a task that
touches them, and CI fails a branch that changes one. A task that needs one is
interactive only: give it `lane: no`, say why under Notes, and move it to Backlog.

## Things never to do
- Edit `bin/`, `obj/`, `.vs/` or `data/`; hand-edit generated migration files.
- Add a NuGet package or change a quality threshold (block and ask Stewart instead).
- Change `RunClaude.cmd` or `hrdrClaudeNative.cmd`.
