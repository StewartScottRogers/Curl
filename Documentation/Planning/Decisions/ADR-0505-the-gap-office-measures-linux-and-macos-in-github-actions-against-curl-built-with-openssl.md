# ADR-0505: The gap office measures Linux and macOS in GitHub Actions against curl built with OpenSSL

- Status: Accepted
- Date: 2026-10-15
- Decided by Claude under Stewart's delegation (Stewart asked for it on 2026-10-15: "set up gap
  office on Linux and macOS")

## Context

The gap analysis office (ADR-0433) measured Curl on one platform only: the Windows machine a gap
run starts on, against the Schannel curl of Git for Windows. Curl ships for Linux and macOS too,
where the drop-in promise is against the OpenSSL build of curl (CLAUDE.md), and CI runs the fast
tests there, but nothing measured the seven gap areas off Windows. A gap that only Linux or macOS
shows (a Unix socket path, an OpenSSL error text, a config file location, a test case that runs
only off Windows) was invisible to the office, and its single headline score said nothing about
the other two platforms.

The office's measurement tools were already written for PowerShell 7 on Linux and macOS; what was
missing was a reference curl of the targeted version on those platforms, somewhere to run, and a
way for findings, tasks and scores to keep platforms apart.

## Decision

1. **Reference curl on Linux and macOS: curl `<target>` built from the release tarball with
   OpenSSL.** `gap-platforms.yml` takes the tarball from `Get-UpstreamRelease.ps1 -SaveArchiveTo`,
   which checks it against the pinned SHA-256 in `Gap/Baselines/curl-<version>.json` first, so the
   reference is built from exactly the source the inventories come from. It is configured with
   OpenSSL, nghttp2, libssh2, zlib, brotli, zstd, libidn2, libpsl, GSS-API and LDAP from apt or
   Homebrew, static libcurl, NTLM (and with it SMB) and TLS session export switched on (configure
   leaves both off unless asked, and Curl implements both; the first build without them showed
   `--ntlm`, `--ssl-sessions`, `smb` and `smbs` as gaps that were only the reference's
   configuration), and no HTTP/3 (building ngtcp2 and nghttp3 as well is not cheap on Ubuntu's
   OpenSSL 3.0). The converter's existing `reference-lacks:HTTP3` rule excludes the HTTP/3
   behaviour cases honestly; the `HTTP3` feature and the `--http3` options measure as gaps,
   because Curl lists what this reference lacks. The built curl
   is cached per OS, version and workflow hash, and rebuilt when the cached one no longer runs.
   `CURL_GAP_REFERENCE_CURL` names it to every tool (`Get-GapReferenceCurlPath`).
2. **Measure in GitHub Actions, through one code path.** `Gap/Tools/Measure-GapAreas.ps1` is the
   whole measurement phase of one platform: build `Curl.Console` Release, fetch the release, find
   the reference, run each area's `Measure-*` tool (behaviour through `Measure-UpstreamCases.cs`,
   `ConvertTo-BehaviourMeasurement.ps1` and `Measure-ReferenceCrossCheck.ps1`), and write
   `measurements/<area>.json` and a status file. `RunGapAnalysis.ps1` runs it for Windows and
   `.github/workflows/gap-platforms.yml` runs it on `ubuntu-latest` and `macos-latest` (the
   repository is public, so the minutes are free), uploading `gap-measurements-<platform>`. The
   workflow runs on `workflow_dispatch` (input `ref`, default `work/dark-factory`, and `request`,
   the gap run's stamp, which names the run), weekly, and on pull requests that change `Gap/` or
   the workflow, so a change to the office proves itself before it reaches `master`. It uses no AI
   and no secret beyond `GITHUB_TOKEN`.
3. **Analyse locally.** `RunGapAnalysis.ps1` gains `-Platforms` (default `windows,linux,macos`) and
   `-PlatformTimeoutMinutes` (default 180). It dispatches `gap-platforms.yml` on `master` for the
   measured commit, measures Windows while the workflow runs, then waits for the workflow and
   downloads its artifacts into `<run>/measurements/<platform>/`. A platform whose measurement does
   not arrive (dispatch failed, run timed out, job left no measurement) is recorded as missing in
   `<run>/platforms.json` with the reason, and the run goes on with the others; nothing stands in
   for it.
4. **Keep platforms apart.** Every finding has a `platform` field; a finding without one is a
   `windows` finding, as every finding filed before this ADR is. A finding is measured, closed and
   reopened only by its own platform's measurement. A Linux or macOS gap that an earlier platform in
   the order windows, linux, macos shows too (in this run, or in an open finding when that platform
   was not measured) is left to that platform's finding, so one cause seen everywhere is one finding
   and one task, not three; a gap that is a platform's own gets that platform's finding. A Linux or
   macOS analyst runs only for its platform's own gaps, which its prompt lists. A Linux or macOS
   task says in its title, Context and acceptance criteria that lanes test only on Windows, so its
   proof is the gap-platforms workflow or CI on that platform passing the named items. Item keys are
   unchanged.
5. **Scores per platform, headline from the lowest.** The scorecard has one column per platform
   per area, a per-platform detail section, and a headline that is the lowest platform's overall
   score, so no platform hides behind another; a missing platform is named with its reason.
   `history.json` gains one entry per measured platform; `data.json` gains `platforms` (each
   platform's newest entry) and `lowest`; the dashboard shows the lowest platform as its headline,
   a column per platform, a per-platform trend and a platform filter on the open gaps.
6. **Documents stay true.** `Gap/README.md`, `Gap/Instructions/Gap-Format.md`, `Analyst-Rules.md`
   (rule 9), the area methods, `Gap/Triage.md`, the findings and scorecards READMEs, the analyst
   agents and CLAUDE.md say all of the above.

## Consequences

- Every gap run takes as long as the slower of the Windows measurement and the gap-platforms
  workflow (about the same today), and costs a little more analyst time only for gaps that are a
  platform's own.
- The overall score can only fall when Linux and macOS join: a gap there now counts. That is the
  point; the drop-in promise is per platform.
- Findings of Linux or macOS gaps that Windows shares are not filed until Windows closes its own
  finding; Linux's and macOS's scores show those gaps meanwhile.
- `gap-platforms.yml` runs `master`'s copy of the office's tools when dispatched, while a local run
  uses the gap worktree's; the two differ only between a `gap` pull request and its merge.
- HTTP/3 behaviour cases stay excluded on Linux and macOS until the reference curl is built with
  ngtcp2 and nghttp3; doing so is a later, separate decision.
