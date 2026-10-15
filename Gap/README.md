# Gap analysis office

The gap analysis office measures how far Curl is from upstream curl in seven areas -
command-line options, URL schemes (protocols), `curl -V` features, `--write-out` variables,
exit codes, environment variables and config files, and the behaviour of upstream's own
`tests/data` cases - and narrows that distance over time
([ADR-0433](../Documentation/Planning/Decisions/ADR-0433-a-gap-analysis-office-measures-curl-against-upstream-curl-releases.md)).
The yardstick is upstream's release data, never Curl's own tables: the release tarball of the
targeted version (`Baselines/target.json`, 8.22.0 today, ADR-0479), its documents and test cases, and
the matched reference curl build for that version. Every run measures three platforms
([ADR-0505](../Documentation/Planning/Decisions/ADR-0505-the-gap-office-measures-linux-and-macos-in-github-actions-against-curl-built-with-openssl.md)):
Windows on this machine, against the Schannel curl of Git for Windows, and Linux and macOS in
the `gap-platforms` workflow on GitHub's runners, against curl built from the same release
tarball with OpenSSL. The file formats are in
[`Instructions/Gap-Format.md`](Instructions/Gap-Format.md).

| Path | What it holds | Built by |
| --- | --- | --- |
| `Instructions/` | The file formats (`Gap-Format.md`), the analysts' rules (`Analyst-Rules.md`), and one method per area | BL-1720, BL-1737 to BL-1739 |
| `Baselines/` | The targeted upstream version (`target.json`), the options intended to differ, each with its ADR (`option-exclusions.json`), the newest one once the watcher has run (`newest.json`), and each release's pinned tarball hash (`curl-<version>.json`) | BL-1721, BL-1736 |
| `Upstream/<version>/` | Upstream curl's inventories, one per area, for that version | BL-1723 to BL-1728 |
| `Findings/` | Gap findings, `GF-####-*.md`, with the folder's `README.md` and `FINDING-TEMPLATE.md` | BL-1720, BL-1731 |
| `Scorecards/` | One scorecard per run, plus `history.json` | BL-1720, BL-1732 |
| `Tools/` | The office's scripts: release download, probe, one `Measure-*` tool per area, `Measure-GapAreas.ps1` (one platform's whole measurement phase), findings, scorecard, dashboard data, tasks and the release diff | BL-1721 to BL-1735, ADR-0505 |
| `Triage.md` | How findings become tasks | BL-1734 |
| `RunGapAnalysis.cmd`, `RunGapAnalysis.ps1` | The entry point that runs a gap analysis | BL-1740, BL-1741 |
| `../.claude/agents/gap-*.md` | The seven gap analyst agents | BL-1737 to BL-1739 |
| `../.github/workflows/gap-release-watch.yml` | The weekly upstream release watcher | BL-1736 |
| `../.github/workflows/gap-platforms.yml` | The Linux and macOS measurement: builds the reference curl, runs `Measure-GapAreas.ps1`, uploads `gap-measurements-<platform>` | ADR-0505 |
| `../.github/gaps/site/index.html` | The dashboard page | BL-1742, published by BL-1743 |

## Running it

```powershell
Gap\RunGapAnalysis.cmd -NewTab
```

Run it in herdr, like the dark factory and the audit office; never with `Start-Process`.
`-NewTab` opens the run in a new herdr tab (a console window outside herdr) and returns.
It refuses inside a dark factory process and while a shift runs.

| Parameter | Effect |
| --- | --- |
| `-Ref` | The commit measured. Default `origin/work/dark-factory`. |
| `-Areas` | Any of `options`, `protocols`, `features`, `writeout`, `exitcodes`, `environment`, `behaviour`. Default all seven. |
| `-Platforms` | Any of `windows`, `linux`, `macos`. Default all three. |
| `-PlatformTimeoutMinutes` | How long the run waits for the `gap-platforms` workflow after dispatching it. Default 180. |
| `-CurlVersion` | The upstream release measured against. Default `Baselines/target.json`'s version. |
| `-AlongsideShift` | Runs even while a shift runs, sharing the machine. |
| `-DryRun` | Does the refusals, then prints every step's command and changes nothing. |
| `-NoTasks` | Files no tasks on `work/dark-factory`. |
| `-NoPullRequest` | Opens no pull request. |
| `-NoCommit` | Writes everything into the gap worktree but commits, pushes and files nothing. |
| `-SelfTest` | Checks the script's own logic against faked `git`, `gh` and `claude`, and the task renumbering against two real clones of a throwaway board. |

A run checks the measured commit out in a detached worktree, dispatches the `gap-platforms`
workflow for that commit, measures Windows here with `Tools/Measure-GapAreas.ps1` (the same
script the workflow runs on Linux and macOS), then waits for the workflow and downloads its
`gap-measurements-linux` and `gap-measurements-macos` artifacts. A platform whose
measurement does not arrive (the workflow failed or did not finish in time) is reported
missing, with the reason, and the run goes on with the others. Each area's analyst then
groups the gaps by cause: every Windows area, and a Linux or macOS area only for the gaps
that are that platform's own (a gap Windows also shows stays with the Windows finding). The
run writes findings and a scorecard on the `gap` branch, opens its pull request to `master` (never merging it), and files the findings'
tasks on `work/dark-factory`. Tasks are numbered from the board as fetched just before filing,
and before each push the run rebases them and runs `task-board.ps1 dedupe` against the commit
it rebased onto, rewriting any renumbered ID in the findings, so tasks the audit office filed
meanwhile keep their IDs (BL-2013). Its run folder, kept for reading, is
`<repo>.gap\<yyyy-MM-dd_HHmm>\` beside the checkout: `gap.log`, `run.json`,
`platforms.json`, `measurements\<platform>\<area>.json`, `reports\<platform>\gap-<area>.md`
and `data.json`.

The workflow also runs on its own every Monday and on every pull request that changes `Gap/`
or the workflow, and by hand (`gh workflow run gap-platforms.yml -f ref=<branch>`); each run
puts its scores in the job summary and its measurements in the artifacts. That is how a
Linux or macOS gap task is proved, since lanes test only on Windows. The script's help
(`Get-Help Gap\RunGapAnalysis.ps1 -Full`) lists every step.

## Scores

Each area's measurement gives every item of the targeted version's upstream inventory
exactly one state:

- **match** - Curl behaves as upstream does;
- **gap** - it does not;
- **unmeasured** - the office cannot measure the item yet, with a reason (for example
  `needs-server:<protocol>` or `no-probe`);
- **excluded** - the item does not apply, with a reason (for example
  `reference-lacks:<feature>`, `platform:<os>` or `debug-build-only`).

The score is **X of Y**, where X is `match` and Y is `match + gap + unmeasured`; an excluded
item counts in neither. Unmeasured counts against the score so that "X of Y match" means
matches shown: measuring more items raises the score only when the new items match, and
what is not yet measured stays visible. Each scorecard also lists the unmeasured and
excluded items by reason.

Every area is scored on each platform separately, one column per platform. The headline,
on the scorecard and the dashboard, is the lowest platform's overall score, so no platform
hides behind another; a platform the run could not measure is named, with the reason, never
filled in.

## From gap to task

[`Triage.md`](Triage.md) has the rules. In short: `Tools/Write-GapFindings.ps1` files each
cause of a gap as a finding, `status: open`, which means accepted.
`Tools/New-TasksFromGaps.ps1` files one lane-eligible `Close GF-####: <title>` task for each
open `scope: target` finding with no open task, without asking Stewart, its priority from
the finding's severity. Stewart may reject any finding (`status: rejected`); a rejected
finding gets no new task.

## Closing and regressions

A finding closes only on re-measurement on its own platform (its `platform` field; a finding
without one is Windows'): a later run must measure every one of its items as `match`, or
`excluded` with a reason, on that platform. A task reaching Done never closes it; when every task
is Done and the latest measurement still shows gaps, a `Re-close GF-####: <title>` task is
filed. A closed finding whose item measures `gap` again reopens with `regression: true`,
and the scorecard and dashboard list it. There is no won't-fix status.

## The moving baseline

`.github/workflows/gap-release-watch.yml` checks curl's latest release every Monday at
06:41 UTC. When it is newer than the one recorded, `Tools/Compare-UpstreamReleases.ps1`
diffs it against the targeted release, `Tools/Write-GapFindings.ps1 -ReleaseDiff` files the
new items as `scope: newest` findings, `Baselines/newest.json` records it, and the result is
pushed to the `gap` branch. `scope: newest` findings get no task while Curl targets an older
release. Moving the target is a decision recorded in an ADR, which changes
`Baselines/target.json` and turns the newest findings into target findings.

## The dashboard

<https://stewartscottrogers.github.io/Curl/gaps/> shows the lowest platform's score as its
headline, the scores per area and platform, the trend, the open gaps with their platforms and
suggestions, the regressions, and the newest release's gaps. The page is `.github/gaps/site/index.html`; its
data is `Tools/Export-GapDashboardData.ps1`'s output, published to the `gource` branch's
`gaps/` folder by `gource.yml`'s `gaps-page` job and by the release watcher.

## Independence

The office is meant to stand outside the dark factory's reach, like the audit office.
BL-1746 is to make `Gap/` and `.claude/agents/gap-*` audit paths, so that lanes can neither
read nor change them; until it is Done they are ordinary paths, and the factory is kept out
only by `Tools/New-TasksFromGaps.ps1` refusing to run inside a lane and by the tasks it files
carrying everything a lane needs without reading `Gap/`.
