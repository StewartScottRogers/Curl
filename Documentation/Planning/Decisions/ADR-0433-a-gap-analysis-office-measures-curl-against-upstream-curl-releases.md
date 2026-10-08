# ADR-0433 — A gap analysis office measures Curl against upstream curl's own release data and narrows the gap over time

- **Status:** Accepted
- **Date:** 2026-10-08

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"). Stewart
approved the gap analysis office on 2026-10-08: its yardstick, its areas, its mechanism, its
moving baseline, its dashboard and the rule that Claude files its tasks. This record states
that design and the details Claude decided to make it buildable. Tasks BL-1719 to BL-1748
build and first run it, and every one of them cites this ADR.

## Context

Curl is a drop-in replacement for the curl command line. The audit office (ADR-0267) checks
the factory's work for defects, but no part of the repository says how far Curl as a whole
is from real curl. The parts that come close measure against Curl's own tables (the passing
list, `Requirements.md`) or against generated command lines. Once the port enters maintenance
mode, upstream keeps releasing (curl 8.22.0 was already out on 2026-10-08, while Curl targets
8.21.0), so the distance has to be re-measured against each new release, not fixed once.

Stewart wants an office that works like the audit office. It should measure the distance
area by area against upstream's own release data, record every gap with evidence and a
suggestion for closing it, file the work, and close a gap only when a later measurement shows
it has gone.

## Decision

### 1. The yardstick is upstream's release tarball, never Curl's own tables

- The **targeted version** is the curl release Curl matches, recorded in
  `Gap/Baselines/target.json` (`8.21.0` today). The **newest version** is the latest release
  on `github.com/curl/curl/releases`.
- `Gap/Tools/Get-UpstreamRelease.ps1` downloads the release tarball
  `curl-<version>.tar.gz` from the GitHub release asset
  (`https://github.com/curl/curl/releases/download/curl-<v_with_underscores>/curl-<version>.tar.gz`),
  falling back to `https://curl.se/download/curl-<version>.tar.gz`. It extracts only
  `docs/`, `tests/data/`, `lib/strerror.c` and `COPYING` into a cache outside the
  repository: `%LOCALAPPDATA%\Curl\gap\upstream\<version>\` on Windows, and
  `$XDG_CACHE_HOME/curl-gap/upstream/<version>/` (default `~/.cache`) elsewhere.
- The tarball's SHA-256 is recorded in `Gap/Baselines/curl-<version>.json` the first time it
  is fetched, and every later fetch must match it. GPG verification of curl's `.asc`
  signature would need a package or a hand-built OpenPGP verifier, so pinning the hash is
  the simplest check that stays inside the BCL.
- Nothing from the tarball is built, linked or shipped. `lib/strerror.c` is read as text,
  because the `curl_easy_strerror` texts appear in no document. That is the one source file
  the office reads.
- Each area's **upstream inventory** (the list of upstream items, with their attributes and
  the version that introduced each one where upstream says) is committed as
  `Gap/Upstream/<version>/<area>.json`. The yardstick can then be reviewed in a diff, and the
  release watcher compares two versions without downloading the older one again.
- The **matched reference build** decides platform-specific answers, as everywhere else in
  Curl. On Windows that is the Schannel mingw curl from Git for Windows, found the way
  `Record-CurlExchange.ps1` finds it (ADR-0018). On Linux and macOS it is the OpenSSL curl on
  `PATH`. A probe uses the reference only when its `--version` first line names the targeted
  version. Otherwise the probe falls back to what the release's documents say, and the run
  records the fallback.

### 2. Areas, items and the score

Seven areas, each scored "X of Y match":

| Area | Upstream inventory from | Curl measured by |
| --- | --- | --- |
| `options` | `docs/cmdline-opts/*.md` (not `_*.md`): long name, short alias, argument, `--no-` form, protocols, `Added:` | Probing `Curl.Console` beside the reference curl |
| `protocols` | `docs/cmdline-opts/_PROTOCOLS.md` headings with their `(S)` variants, and the reference's `curl -V` `Protocols:` line | `curl -V` and a probe per scheme |
| `features` | `docs/cmdline-opts/version.md` feature headings, and the reference's `curl -V` `Features:` line | `curl -V` |
| `writeout` | `docs/cmdline-opts/write-out.md` variables | `-w '%{name}'` probes |
| `exitcodes` | `docs/libcurl/libcurl-errors.md` (`CURLE_*` names and numbers), `docs/cmdline-opts/_EXITCODES.md`, `lib/strerror.c` texts | `CurlExitCode` in `Curl.Protocol.Abstractions.UnitLibrary` and the texts in `Curl.Console/CurlEasyErrorText.cs`, read as source text |
| `environment` | `docs/cmdline-opts/_ENVIRONMENT.md`, `docs/libcurl/libcurl-env.md`, `docs/cmdline-opts/config.md` | Probes with a controlled environment and config file |
| `behaviour` | Every `tests/data/test*` case of the release | The existing in-process harness (`Curl.Conformance.UnitLibrary`, ADR-0013), plus an out-of-process cross-check through both binaries with `Record-CurlExchange.ps1` |

Every item has a stable key, `<area>:<item>[:<facet>]` (for example `options:--ech`,
`options:--ech:argument`, `behaviour:test1234`). Each run gives every item exactly one state:

- **match**: Curl answers as the reference build or the release's documents say it should;
- **gap**: it answers differently, with the evidence;
- **unmeasured**: the office cannot measure the item yet. Examples are a case that needs a
  server the harness cannot stand up, or an environment variable with no probe;
- **excluded**: the item cannot apply to a curl command-line replacement on this platform,
  with the reason. The excluded classes are libcurl API test cases (`<tool>lib…`), libcurl
  unit tests (`<tool>unit…`), cases that need a debug build of curl (`Debug`, `TrackMemory`,
  `unittest`), and features or protocols the matched reference build lacks on this platform,
  read from its `curl -V`.

The score is **X = match** and **Y = match + gap + unmeasured**. An unmeasured item counts
against the score. "X of Y match" therefore means matches shown, and measuring more items
raises the score only when the new items match. Each area also reports its unmeasured and
excluded counts with their reasons, so the size of what is not yet measured stays visible
and is never hidden. The **overall** score is the sum of X over the sum of Y across the
areas. Every run measures one platform, and the scorecard names it.

Curl's existing tooling is reused, not copied. That means the in-process harness and its
screening, `Record-CurlExchange.ps1` (extended where it falls short), and the reference-curl
lookup it already has. The audit office's `Invoke-DifferentialConformance.ps1` is not
reused. It lives under `Audit/`, which the factory's lanes may not read, and the lanes build
the gap office (decision 7).

### 3. The mechanism mirrors the audit office

- `Gap/RunGapAnalysis.cmd -NewTab` runs in a herdr tab, like `Audit\RunAudit.cmd`, with
  `-Areas`, `-DryRun`, `-Ref` (default `origin/work/dark-factory`), `-CurlVersion` (default
  the targeted version) and `-AlongsideShift`. It refuses inside a factory process, and
  refuses while a shift runs unless `-AlongsideShift` is given. Its run folder is
  `<repo>.gap\<stamp>\`.
- There is one analyst agent per area: `.claude/agents/gap-<area>.md`, following
  `Gap/Instructions/Analyst-Rules.md` and `Gap/Instructions/<Area>.md`. Models are chosen by
  cost. `gap-protocols`, `gap-features`, `gap-writeout` and `gap-exitcodes` run on Haiku,
  because their measurements are already itemised. `gap-options` and `gap-environment` run on
  Sonnet. `gap-behaviour` runs on Opus, because it groups hundreds of failing cases by cause
  and names where in Curl each cause lives. An analyst reads its area's measurement, groups
  gap items by cause, and writes one report block. Each group in that block has a title, a
  severity, the curl version that introduced it, a suggestion for closing it, and the Curl
  project folders the fix would touch.
- Gap findings are `Gap/Findings/GF-####-<slug>.md`. Each one lists its item keys, evidence,
  `introduced-in`, `scope` (`target` or `newest`), severity, suggestion and tasks. A
  scorecard per run is written as `Gap/Scorecards/<stamp>.md`, with the per-area and overall
  percentages and the change since the last run. The run is also appended to
  `Gap/Scorecards/history.json`. `Gap/Instructions/Gap-Format.md` defines every field.
- Severity: **Critical** when an exit code or the output bytes differ on a common path (a
  plain GET, a POST, `-o`, `-L`, `-u`). **High** when an option, scheme, feature, write-out
  variable or exit code is missing outright. **Medium** when an argument form, alias,
  message text or environment behaviour differs. **Low** for anything else.

### 4. Claude accepts the suggestions and files the tasks

Stewart approves nothing ahead of time. A finding is filed `open`, which means accepted.
`Gap/Tools/New-TasksFromGaps.ps1` files one lane-eligible Curl task for each open,
`scope: target` finding that has no open task. The task's title is
`Close GF-####: <title>`. Its priority follows severity: Critical and High give `High`,
Medium gives `Normal`, Low gives `Low`. Its pipeline is `protocol` for the protocols area
and `feature` for the rest. Its touches are the analyst's project folders and never a `Gap/`
path. The task body carries the finding's evidence and suggestion in full, because lanes
cannot read `Gap/` once it is guarded.

Stewart may reject any finding (`status: rejected`), and a rejected finding gets no new
tasks. When every task of a finding is Done and the latest run still measures it as a gap,
the script files `Re-close GF-####: <title>`. There is no "won't fix" status. Root
`CLAUDE.md` makes a complete reimplementation the default answer.

### 5. A gap closes only on re-measurement

A finding closes only when a later run measures every one of its items as `match`, or as
`excluded` with a reason the run states. A finding never closes because its task reached
Done. A closed finding whose item measures `gap` again reopens with `regression: true` and
the run that saw it. The scorecard and the dashboard list the regression.
`Write-GapFindings.ps1` applies this rule mechanically from the measurement files. An
analyst's opinion never closes a finding.

### 6. The baseline moves with upstream

- `.github/workflows/gap-release-watch.yml` runs weekly and on demand. It reads the latest
  curl release from the GitHub API. When a release is newer than the last one it saw, it runs
  `Gap/Tools/Compare-UpstreamReleases.ps1`, which builds the new version's upstream
  inventories and diffs them against the targeted version's. The diff covers new, removed and
  changed options and argument forms, write-out variables, exit codes and their texts,
  protocols, features, environment variables, and test cases (new, changed and removed
  `tests/data` files by hash). The workflow records the result as findings with
  `scope: newest` and `introduced-in: <new version>`, then commits them to the `gap` branch.
  The dashboard then says "curl X released: N new gaps".
- Findings with `scope: newest` are shown and counted in the score against the newest
  version, but no tasks are filed for them while Curl targets an older version. Curl matches
  the reference build it targets. Building the next release's behaviour early would break
  that match.
- Moving the targeted version is a decision recorded in its own ADR (Claude's, under the
  delegation). That ADR updates `Gap/Baselines/target.json`, the vendored test data
  (`Update-UpstreamTestData.ps1`), `UpstreamCaseRunner.CurlVersion` and the reference binary,
  and turns the newest-scope findings into target-scope findings so their tasks are filed.
  BL-1748 makes that decision for curl 8.22.0 after the first run.

### 7. Independence: the lanes build the office, then it is guarded

The factory's lanes may build the gap office now, because every task up to BL-1745 is
lane-eligible. Then BL-1746 makes `Gap/` and `.claude/agents/gap-*` audit paths. It is
interactive only and is done on the `audit` branch, because the guard files change only
there. It extends the PreToolUse hook, `task-board.ps1`'s audit-path list and CI's
`Test-AuditPathsUntouched.ps1`, so once the office is running, the factory cannot read or
move its own yardstick.

From the start, gap runs commit their findings, scorecards and inventories to a `gap`
branch cut from `master`, never to `work/dark-factory`. The branch reaches `master` through a
pull request, which an interactive session merges once CI is green on all three platforms.
This is the same route the audit branch takes. The tasks a run files go onto the factory's
board on `work/dark-factory`, as the audit's tasks do.

### 8. The dashboard is a page of the GitHub Pages site

`.github/gaps/site/index.html` is one self-contained file with no external script, font or
stylesheet. It reads `data.json` from beside itself, a file that
`Gap/Tools/Export-GapDashboardData.ps1` writes. It shows:

- the overall percentage and each area's percentage, against the targeted version and
  against the newest;
- the trend over runs;
- open gaps with their suggestions, filterable by area, severity and curl version;
- recently closed gaps and regressions;
- the release banner.

It supports light and dark themes and works at phone width. A `gaps-page` job in
`gource.yml` publishes it to the `gource` branch's `gaps/` folder. That job has its own
concurrency group and commits without force, as the `board-page` job does. It publishes the
page when the page's source changes on `work/dark-factory`, and the data when the `gap`
branch changes. The render job, the only one that force-pushes the `gource` branch, carries
`gaps/` over as it already carries `coverage/`, `board/` and `integration/`. The Gource
viewer, the board page and the coverage report link to the dashboard, and the dashboard
links back to them.

### 9. Base class library and PowerShell only

The tools are PowerShell scripts that run under Windows PowerShell 5.1 and PowerShell 7. The
parts the release watcher runs must also work on Linux. Heavy lifting goes in C# file-based
apps (`dotnet run --file`). The behaviour runner is one of them, and it references
`Curl.Conformance.UnitLibrary` and `Curl.Console` with `#:project`. No Python, no package.
Each tool has a `-SelfTest` against fixtures under `Gap/Tools/Fixtures/`. Like
`Audit/Tools/`, the office's tools are not product code and are not held to the coverage
gates. Anything that moves into a `*.UnitLibrary` is held to them.

## Consequences

- The repository gains a `Gap/` shared project, seven analyst agents, a weekly workflow and a
  fourth page on the Pages site.
- The first run will report a low behaviour score. 559 of about 2,000 upstream cases pass
  today, and the cases the harness cannot run count as unmeasured. The per-area breakdown
  shows how much of that is unmeasured rather than different. Building the missing test
  servers is how that number narrows.
- Since the 8.22.0 release, the dashboard's "against the newest version" figures will sit
  below the targeted ones until BL-1748 decides on retargeting.
- The `gap` branch adds one more pull request route to `master`. Root `CLAUDE.md` gains a
  section for the office (BL-1745), and Stewart's standing exception for audit-office pull
  requests is read as covering it.
