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
- .NET Software Development Kit 10 (`global.json` at the repository root pins SDK 10.0.401 with `rollForward: latestFeature`). Target framework: `net10.0` unless a project states otherwise.
- The solution file lives at the repository root (`Curl.slnx` preferred, `Curl.sln` acceptable).
- Shell is Windows. Use PowerShell or `cmd` syntax, backslash paths are fine.

## Build and test commands
- Build: `dotnet build`
- Test (fast, default): `dotnet test --filter "TestCategory!=Integration"`
- Test (Integration only): `dotnet test --filter "TestCategory=Integration"`
- Test (long-running only): `dotnet test --filter "TestCategory=LongRunning"` with
  `CURL_RUN_LONG_RUNNING_TESTS=1` set; without it they show as skipped
- Test (everything): `dotnet test`

An Integration test touches something real outside the process - a socket, the disk, the
OS, a native API, a system agent - and carries `[TestCategory("Integration")]`. It lives
only in a `Curl.<Area>.IntegrationTests` project, never in a `*.UnitTests` project, and
every test in an `*.IntegrationTests` project carries the category, so the fast command
skips them all. `Directory.Build.props` enforces this at build time: its
`VerifyIntegrationTestPlacement` target fails the build, naming the file, when either half
is broken. A test whose only real resources are files in a temporary directory it
creates and deletes, or a loopback socket it opens and closes without sending a byte, is a
unit test. A slow test that is pure computation is not an Integration test: it stays in its
`*.UnitTests` project as a `LongRunning` test. See ADR-0421.
- Format: `dotnet format`
- Measure quality: `powershell -NoProfile -File Measure-CodeQuality.ps1`

Always build and run the fast tests before declaring a task finished.

## Quality gates
Every `*.UnitLibrary` (and `Curl.Console`, `Curl.Api` and `Libcurl`) is held to 100% line coverage, 100% branch
coverage, cyclomatic complexity of at most 10 per method, and a CRAP score of at most 30.
All four are measured with tooling the solution already has - the coverage collector the
MSTest meta-package brings, and the SDK's own `CA1502` analyzer - so no package is needed
for any of it. Complexity is enforced at build time: the threshold lives in
`CodeMetricsConfig.txt` and warnings are errors, so a method at 11 breaks the build. CI
enforces all four: its `quality` job (Windows) runs `Measure-CodeQuality.ps1` on the
coverage three parallel `quality-shard` jobs collect (`-Shard`, `-FromShards`, BL-3464), which
measures complexity from the coverage report as the coverage page does - stricter than
`CA1502` for async methods and lambdas - and fails when any member is outside a gate, so a
red quality job blocks the shift-end merge to `master` (BL-2068). The
`coverage-auditor` agent measures the rest and files the gaps as tasks. Thresholds in
`CodeMetricsConfig.txt` are Stewart's to change; never raise one to make code pass.
Coverage is measured from the fast tests only (`TestCategory!=Integration`), so moving a
test into an `*.IntegrationTests` project never changes a library's measured coverage, and
a line only an Integration test reaches counts as uncovered (ADR-0421).

## Git and GitHub
Reversible git and gh work is delegated to github-operator: status, commits, rebases,
explaining conflicts, pull request bodies, Actions triage, branch cleanup.

Committing and pushing to a feature branch is automatic and needs no confirmation. Once
`dotnet build` is clean and the fast tests are green, commit by logical unit and push;
report it afterwards rather than asking first.

One standing exception: the `gource` branch holds only the latest showcase render (the Gource video and the coverage report) and is
force-pushed on every render by `.github/workflows/gource.yml` (owned by the
`showcase-publisher` agent). That force push, to that branch only, needs no confirmation.

Another standing exception (Stewart, 2026-09-28): the `board` branch holds only the dark
factory's latest `status.json` and is force-pushed every few minutes by
`RunDarkFactory.ps1`'s coordinator (ADR-0129). That force push, to that branch only,
needs no confirmation.

Another standing exception (Stewart, 2026-10-19: "make sure to keep these downloads updated with
the latest builds"): on every push to `master`, `.github/workflows/release.yml` deletes the rolling
pre-release `preview` and its tag, and makes them again at the new commit with that build's
packages. `DOWNLOAD.md` links there. Moving that one tag needs no confirmation; numbered `v*` tags
and releases still do.

A second standing exception (Stewart, 2026-09-27): at the end of every dark factory
shift, `RunDarkFactory.ps1` merges its branch into `master` through a pull request, but
only when the `CI` workflow passed on Windows, Linux and macOS for the exact commit being
merged. That merge needs no confirmation; a red or unfinished CI run means no merge.

A third standing exception (Stewart, 2026-09-30: "stop asking redundant questions and just
do the job without me"): an interactive session merges the audit office's pull requests
(the `audit` branch, and any branch built from `master` for guarded audit or guard work)
into `master` itself, then merges `master` into `work/dark-factory`, and goes on to the
next audit task without asking - but only when the `CI` workflow passed on Windows, Linux
and macOS for the pull request's head commit. It reports what it merged afterwards.
The gap analysis office's pull requests (the `gap` branch) are merged under the same
standing exception and the same CI condition.

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
On Linux and macOS a shift runs with `RunDarkFactory.sh` (PowerShell 7, `pwsh`, required), and
`.github/workflows/dark-factory-unix.yml` runs the factory's `-Test*` self-tests under `pwsh` on both (BL-3923).
`RunDarkFactory.cmd` works the board unattended: each ready task goes to a headless
`/task-run`, and anything that needs Stewart ends in `Blocked` with an alarm at the end
of the shift. Its code is `RunDarkFactory.ps1` (the help, the parameters and a loader) and
one file per area under `DarkFactory/` (claims, integration, CI watch, lane pacing and so
on), which the loader dot-sources in a fixed order (BL-2988); a factory task names the area
files it changes in `touches`. `-Lanes N` runs N tasks at once, each in its own git worktree beside the
checkout (`<repo>.lanes\lane-<n>`); the board never gives two lanes tasks whose
`touches` overlap, and each lane rebases, rebuilds, tests and pushes its own work, one
lane at a time. Under that integrate lock a lane builds the whole solution but runs only the
fast tests of the test projects its commits can affect (all of them when a shared build
file or a `.csproj` changes), and it resolves a rebase conflict with the lock released;
CI still runs every test on Windows, Linux and macOS (BL-2967, ADR-0743). `-Lanes Auto` sizes the shift itself to the measured burn rate, the board's parallel
capacity and the machine's cap - straight up to its unpaced lanes, then one lane at a
time above them, and straight down when a limit falls - so
it suits whichever Claude plan is in use. Running out of tokens is not a stall: the shift announces it with the
reset time, waits (the wait does not count against `-Hours`), warns a minute before the
new session and reruns the cut-off task. See the script's header for the details.

Work runs as parallel as it can (Stewart, 2026-10-21: "always function in a highly
parallel fashion"). `-Lanes Auto` adds its first `-UnpacedLanes` (3) lanes without
reading the burn rate; a task's `touches` names files or narrow subfolders, never a whole
project it only adds to; new cases, stand-ins and handlers go in one file each that
registers itself, not in a central switch or test class; and a file many tasks touch is
split as its own High task rather than waited on (`task-board.ps1 hotspots`). A
registration table that tasks only add lines to is kept one entry per line, marked
`merge=union` in `.gitattributes` and listed in `Tasks/SharedListFiles.txt`, so tasks
that each add to it do not count as overlapping. An
interactive session sends independent steps as parallel tool calls or subagents.

When Claude starts a shift it always passes `-NewTab`, e.g.
`RunDarkFactory.cmd -NewTab -Lanes Auto -Continuous`; `-Continuous` makes a shift that
ends with work still ready start the next one itself. A shift ends before the tokens run
out: once 85% of the 5-hour window (`-StopAtUsage`) or 97% of the weekly window
(`-StopAtWeeklyUsage`) is used, lanes claim
nothing new, finish what they hold and push; the next shift waits for a fresh 5-hour
window, or for the weekly reset when the week is used up, with a notice rather than the
alarm. `-Lanes Auto` paces lanes to the 5-hour window only; `-WeeklyPace` also spreads
the weekly budget evenly to its reset (ADR-0130, BL-806). Lane runs load
`DarkFactory/LaneRules.md` in place of this file, with no user-level settings, plugins or
MCP servers and a `-MaxRunTurns` turn cap, so a rule a lane needs goes in both files
(BL-3001). A task is claimed at most twice (ADR-1155): a split for size defers the task
and files every part of it, the rest included, as new tasks, and a task that reaches the
turn cap or is sent back to `Backlog` a second time gets the bounded split run instead of
a third claim. The factory picks the Claude
model per task - a task's front-matter `model:` wins, else opus for security, crypto,
feature, protocol, retried and CI-fix work and sonnet for the rest, a routine feature
task (an upstream unit program port, a harness stand-in and the like, naming no hard
work) included - and `-Model` forces
one model for every task (BL-1705). Inside herdr (`HERDR_ENV=1`) that opens the shift
and each of its lanes as herdr tabs in the current workspace; outside herdr, as console
windows. Never start one with `Start-Process` or a bare background command: Stewart
watches shifts in herdr. Stop a shift by closing its tabs (or killing its process tree).
Leave its tasks in `Doing` and its lane worktrees as they are: the next shift adopts each
stopped lane and resumes its task from the work in place. To restart a running shift,
run `RunDarkFactory.cmd -Restart` (in the background: it waits for each lane to finish
any claim or integration). It stops that shift lane by lane and starts the next one with
the same arguments; never hand-kill the processes for a restart. While a shift runs, its
coordinator restarts any lane whose process dies, and lanes wait out the usage limit and
carry on when tokens return - nobody needs to restart them. The lanes' heartbeats are
published as `status.json` on the `board` branch every `-HeartbeatMinutes` (default 3)
for the live board page.

Lanes test only on Windows, so the coordinator of a lane shift watches CI for them
(BL-987): on each heartbeat it reads the finished `CI` runs on the shift's branch (and the
failed jobs of the one still running, ADR-0811)
(cancelled ones ignored) and, for every new failing test or build error in
`gh run view <id> --log-failed`, files and pushes a High task - "Fix CI failure <test> on
Linux and macOS", or "Fix flaky CI test <test> ..." for one that failed once between
passing runs - with the run, platforms, first failing commit, error message and the test's
project and library as `touches`. A test a live task already names is not filed again.

Every session, lanes included, whispers milestones to Stewart through the PostToolUse hook
`.claude/hooks/whisper-milestone.ps1`: a task moved to Done, a CI failure filed, a commit
made, a branch deleted - quietly, in Windows' Zira voice, one phrase at a time. The same
script, run by `task-board.ps1`, whispers the Backlog's depth ("Backlog depth, 63.") a minute
after a move takes a task out of `Backlog`, once per burst of moves (BL-1182). All of it is silent while `%LOCALAPPDATA%\Curl\audio-off` exists (BL-1317), and so are the dark factory's alarm, chimes and spoken notices: `RunDarkFactory.ps1` checks the file at each sound, so it reaches a shift already running, shows the alarm and notices on screen only and never touches the volume (BL-1456). `-QuietAlarm` is then not needed; it silences a shift the same way whether or not the file exists.
## Audit office
An independent team of AI auditors audits the dark factory and the code it produced, from
outside the factory's reach (ADR-0267; `Audit/README.md` maps the folder). Five read-only
auditors, each reading `Audit/Instructions/Auditor-Rules.md` and its own method there:

- `audit-quality` (Opus): tests whose names lie, weak assertions, mutants no test kills.
- `audit-security` (Opus): fuzzing, constant-time secret comparisons, secrets in output.
- `audit-conformance` (Opus): generated command lines through real curl and Curl, and heavy
  transfers through both for hangs, quadratic blow-up and failures under load.
- `audit-truthfulness` (Opus): names, doc comments, documents, ADRs and script help.
- `audit-process` (Opus): the factory's logs, git history and CI runs.

No audit compares Curl's speed, memory or binary size with curl's (Stewart, 2026-10-13): C curl
will always be faster and smaller, so Curl never tries to match or beat it there; only behaviour
must match, and performance counts only when it breaks behaviour. The `audit-performance`
auditor was retired that day (BL-2132).

`audit-seeder` (Sonnet) plants known defects from the encoded catalogue in
`Audit/PlantedDefects/` before each audit, at least one per auditor, so each auditor's
catch rate is measured. An auditor that misses a defect planted for it, returns no report
block, or changes the audited tree is **unreliable** on that audit's scorecard: its numbers
are marked, and its re-audits close nothing on its word alone (ADR-0422).

Audit paths are `Audit/` and `.claude/agents/audit-*` (ADR-0267), and the gap analysis
office's `Gap/` and `.claude/agents/gap-*` (ADR-0433); the guard files that protect them -
`.claude/hooks/guard-audit-paths.ps1`, `.claude/settings.json`, `.github/workflows/ci.yml`
and `.claude/skills/task-board/task-board.ps1` - change only through the `audit` branch
too. Four guards keep the factory out: tasks that touch an audit path or a guard file are
interactive only, and `task-board.ps1` refuses a lane filing or claiming one (BL-1209); every factory process carries `CURL_DARK_FACTORY_LANE`, and the PreToolUse
hook `.claude/hooks/guard-audit-paths.ps1` refuses it any read or change of an audit path;
the CI job `audit-guard` (`Audit/Guard/Test-AuditPathsUntouched.ps1`, required on `master`)
fails when `work/dark-factory` changes one; and audit work is done on the `audit` branch,
whose pull requests an interactive session merges once CI is green (the standing exception
in "Git and GitHub"). A lane that meets an audit path stops and leaves it to an interactive
session.

Run an audit with `Audit\RunAudit.cmd -NewTab` (in herdr, like the factory; never with
`Start-Process`); on Linux and macOS, with `Audit/RunAudit.sh -NewTab` (PowerShell 7, `pwsh`,
required; same arguments, BL-3927). `-Auditors` picks some of the five (and warns about any left out that have
accepted findings, which go un-re-audited), `-Planted` the defect count (default
8), `-Ref` the commit (default `origin/work/dark-factory`), `-DryRun` prints the plan. It
refuses inside a factory process and while a shift runs; `-AlongsideShift` runs it anyway,
sharing the Claude budget, since `-Continuous` shifts leave no gap. It writes findings and a
scorecard on the `audit` branch and opens its pull request, which it never merges.

Cadence: on demand, before each roadmap-milestone merge to `master`, and after changes to
`RunDarkFactory.ps1`. `Audit\Tools\Test-AuditDue.ps1` says when one is due - also when
accepted findings wait on a re-audit (`reaudit-pending:<n>`), on a task no lane will take
(`interactive-stuck:<n>`) or on the audit that closes them (`close-pending:<n>`); a shift's end announces it, and holds a milestone's merge until the
audit has run (BL-1022).

Findings are `Audit/Findings/AF-####-*.md`, arriving `proposed`. Only Stewart sets
`accepted` or `rejected`; `Audit/Triage.md` and `Audit\Tools\New-TasksFromAcceptedFindings.ps1`
turn accepted ones into lane-eligible Curl tasks, and a `Re-fix` task when a re-audit finds
one still reproducing after its tasks are Done. A finding closes only on evidence, never
because its task reached Done (ADR-0422): its own auditor's re-audit says it no longer
reproduces and the audit run's own rerun of its mechanical reproduction on the clean commit
agrees (whatever the auditor's reliability), or, with no mechanical answer, that auditor is
reliable or says so on two audits running. The one exception is a finding about one past
run's cost (key ending `:cost-outlier`): history cannot change, so it is never re-audited and
no `Re-fix` task is filed for it; the first audit after all its tasks are Done closes it as
`task-done-historical` (Stewart, 2026-10-14). Duplicates close as `duplicate`, and Stewart alone
may close an accepted finding himself (`closed-how: stewart`); the scorecard lists findings
stuck open after more than 3 audits. Re-audit verdicts that overlap a planted defect are set
aside.

## Gap analysis office
The gap analysis office measures Curl against upstream curl's release data - the targeted
version's tarball (`Gap/Baselines/target.json`) and the matched reference curl - in seven
areas: options, protocols, features, write-out variables, exit codes, environment, and the
behaviour of upstream's `tests/data` cases (ADR-0433; `Gap/README.md` maps the folder).
Every run measures Windows locally and Linux and macOS in `gap-platforms.yml` (curl built
from the tarball with OpenSSL); each platform scores and files findings on its own, a finding
closes only on its own platform, and the headline is the lowest platform's score (ADR-0505).
Seven read-only analysts group each area's gaps by cause: `gap-behaviour` (Opus),
`gap-options` and `gap-environment` (Sonnet), `gap-protocols`, `gap-features`,
`gap-writeout` and `gap-exitcodes` (Haiku).

Run it with `Gap\RunGapAnalysis.cmd -NewTab` (in herdr; never with `Start-Process`).
Findings are `Gap/Findings/GF-####-*.md` on the `gap` branch, and close only when a later
run re-measures every item as `match` (or `excluded` with a reason), never because a task reached Done. Claude files
their tasks without asking (`Gap/Triage.md`); Stewart may reject any finding. The weekly
`gap-release-watch.yml` records each new curl release's gaps as `scope: newest` findings;
moving the targeted version is an ADR. Dashboard:
<https://stewartscottrogers.github.io/Curl/gaps/>.

## Repository layout
Flat and linear. Every project is a directory immediately under the repository root.
There is no `src/` and no `tests/`; do not create them.
```
Curl/
├── Curl.slnx
├── Curl.Core.UnitLibrary/        ← production library
├── Curl.Core.UnitTests/          ← its tests, immediately beside it
├── Curl.Networking.IntegrationTests/ ← its Integration tests, just before the library
├── Curl.Networking.UnitLibrary/
├── Curl.Networking.UnitTests/
├── ...                           ← every other project, one flat alphabetical run
├── Documentation/                ← shared project (docs and planning)
├── Tasks/                        ← shared project (task board)
├── Audit/                        ← shared project (the audit office; never written by the factory)
├── data/                         ← local runtime data (gitignored, never read or modify)
└── .claude/                      ← Claude Code configuration
```

### Project naming
- Production library: `Curl.<Area>.UnitLibrary`, protocols `Curl.Protocol.<Name>.UnitLibrary`,
  extensions `Curl.Extension.<Name>.UnitLibrary` (see "Extensions" below).
- Tests: the same name with `.UnitTests` instead of `.UnitLibrary`.
- Integration tests, once an area has any: the same name with `.IntegrationTests`
  instead of `.UnitLibrary` (ADR-0421).
- Five projects are exceptions to the `Curl.<Area>.UnitLibrary` rule: the executables
  `Curl.Console` and `Curl.Config` (the `curl-config` executable, ADR-0712; no `.UnitLibrary`
  suffix, because they are not libraries), the two
  programming interfaces `Curl.Api` and `Libcurl` (ADR-0506), and the MCP server host
  `Curl.Mcp` (ADR-0548). `Libcurl` also has no `Curl.` prefix, matching upstream's library
  name. `Curl.Api`, `Libcurl` and `Curl.Mcp` are held to the four quality gates like a
  `*.UnitLibrary`. `Curl.Api.UnitTests` tests both `Curl.Api` and `Curl.Api.UnitLibrary`,
  `Curl.Mcp.UnitTests` tests both `Curl.Mcp` and `Curl.Mcp.UnitLibrary`, and
  `Libcurl.UnitTests` tests `Libcurl`.
- Names sort so each `.UnitTests` lands directly after the library it tests, and each
  `.IntegrationTests` directly before it (for `Curl.Console`, directly before
  `Curl.Console.UnitTests`). Keep it that way. In `Curl.slnx` the order of projects is set
  by their solution folders (ADR-0731), not by name: a project sits with its tests in the
  folder for its area, and the folders follow one another in the file.

In `Curl.slnx`, every project sits with its tests in a solution folder for its area,
under one folder for its group (ADR-0731):

- `/Hosts/`: Api, Curl (the `Curl.Console` projects, which build `curl.exe`), Libcurl,
  Mcp - the ways Curl is used.
- `/CommandLine/`: Cli, Config, Output.
- `/Transfer/`: Cookies, Core, Engine, Networking.
- `/Wire/`: Http2, Http3, Quic, Zstandard - framing and encoding below the protocols.
- `/Security/`: Authentication, Cryptography, HttpMessageSignatures, Kerberos, Ntlm, Tls.
- `/Protocols/<Name>/`: each `Curl.Protocol.<Name>.*`, and `Curl.Protocol.Abstractions.*`
  in `/Protocols/Abstractions/`.
- `/Conformance/<Part>/`: each `Curl.Conformance.<Part>.*`, and the harness itself
  (`Curl.Conformance.UnitLibrary` and `.UnitTests`) in `/Conformance/Harness/`.
- `/Extensions/<Name>/`: each `Curl.Extension.<Name>.*`, with the registry in
  `/Extensions/Composition/`.
- `/Shared/`: the shared projects (Audit, Claude, Documentation, Gap, Tasks).

A new area goes in the group it belongs to, in a folder named for the area. Solution
folders change only the solution file; the
directories on disk stay flat. The `Solution Items` and `Scripts` solution folders hold
loose files only.

Each project folder may contain its own `CLAUDE.md` with project-specific rules; follow it when working in that folder.

### Extensions
What Curl adds beyond upstream curl - a scheme or feature curl does not have - is an
*extension*, kept apart from curl's own protocols (ADR-0999):

- Each is `Curl.Extension.<Name>.UnitLibrary` with its `.UnitTests`, registered by one line in
  `Curl.Extension.Composition.UnitLibrary/ExtensionCatalogue.cs` (a shared list file).
- It joins the solution with exactly one line in `Curl.slnx`, directly under the marker comment in the
  Extensions section: a single-line `<Folder Name="/Extensions/<Name>/">` holding both `<Project/>`
  elements (`merge=union`, ADR-1003). Composition references every extension by wildcard, so no csproj
  is edited and `Curl.slnx` and that csproj need not be in the task's `touches`.
- Only the hosts (`Curl.Console`, `Curl.Api`, `Curl.Mcp`) and the extensions reference an
  extension. An extension references `Curl.Protocol.Abstractions` and the shared layers, never a
  `Curl.Protocol.*` library and never another extension but `Curl.Extension.Abstractions`.
  `VerifyExtensionIsolation` in `Directory.Build.props` fails the build otherwise.
- An extension never changes what curl does: it adds a scheme or option curl refuses, is put after
  curl's handlers (a clash fails composition), leaves `curl -V` and `Libcurl` alone, and
  `CURL_EXTENSIONS=0` turns every extension off.
- It is tested against its specification, not against curl; the gap office and conformance audits
  leave it out. The four quality gates apply.

## Solution-wide conventions
- **Say what it does, do what it says.** Every name - project, file, type, member,
  parameter, test, script, task - says exactly what the thing does, and the thing does
  nothing its name hides. No generic names (`Process`, `Handle`, `Manager`, `Helper`,
  `Utils`, `Data`) where a specific one exists; one concept has one name, the one in
  `Documentation/Wiki/Glossary.md`, which `align-and-document` keeps current. Documents obey the same rule: every statement is true
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
  only approved package is Microsoft's `MSTest` meta-package, the test harness; no
  production project references a package (the MCP server is hand-written, ADR-0799).
  Adding any other package needs Stewart's explicit approval, asked for *before* the
  reference is added — hand-roll the small piece needed, or stop and ask. Test projects use
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
- **`--ai-help` keeps pace with the options.** `curl --ai-help` is Markdown help for AI
  agents beside the human `--help` (BL-911): a short index by default,
  `--ai-help <category>` or `all` for the full reference. Every task that adds or
  changes a command-line option also keeps `--ai-help` right, and says so in its
  acceptance criteria.
- **A fix found through an upstream case also gets a focused unit test.** Any product fix
  found through an upstream case (the ratchet, a gap finding or an audit conformance
  finding) also adds a focused unit test in the fixed library's own `.UnitTests` project,
  so each library's tests stand alone without the upstream cases (ADR-0482).
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
