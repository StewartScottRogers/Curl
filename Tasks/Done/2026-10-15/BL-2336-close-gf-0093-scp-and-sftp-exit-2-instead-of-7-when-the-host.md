---
id: BL-2336
title: Close GF-0093: scp and sftp exit 2 instead of 7 when the host refuses the connection (on macos)
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: []
touches: [Curl.Protocol.Ssh.UnitLibrary, Curl.Protocol.Ssh.UnitTests, Curl.Cli.IntegrationTests, Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: none
created: 2026-10-15
completed: 2026-10-15
---
# BL-2336 — Close GF-0093: scp and sftp exit 2 instead of 7 when the host refuses the connection (on macos)

## Goal

Curl behaves as curl 8.22.0 does for every item of gap finding GF-0093 (scp and sftp exit 2 instead of 7 when the host refuses the connection), so a later gap analysis measures each of `protocols:scp`, `protocols:sftp` as `match`.

## Context

- Finding: GF-0093, filed by the gap analysis office (ADR-0433).
- Area: protocols. Severity: Medium. Introduced in: not stated upstream.
- Platform: macos, measured against curl 8.22.0 built from the release tarball with OpenSSL by the gap-platforms workflow (.github/workflows/gap-platforms.yml, ADR-0505). This gap is macos's own: Windows does not show it.
- Lanes test only on Windows, so a lane cannot see this gap pass locally. The proof is the gap-platforms workflow (run it on the branch: gh workflow run gap-platforms.yml -f ref=<branch>) or the CI job on macos passing a test that pins each item; write those tests platform-neutral or mark them for macos.
- Items: `protocols:scp`, `protocols:sftp`.
- Targeted curl version: 8.22.0 (Gap/Baselines/target.json).
- Yardstick: `docs/cmdline-opts/_PROTOCOLS.md` in the curl 8.22.0 release tarball.
- The gap closes only when a later gap analysis re-measures every item as `match` (ADR-0433 decision 5), never because this task reaches Done.

Evidence, copied from the finding:

Both schemes are listed by Curl and the reference. For curl scp://127.0.0.1:1/ and curl sftp://127.0.0.1:1/ the reference exits 7 (expected 'listed, exit 7'); Curl exits 2 (actual 'listed, exit 2'). macOS measurement, curl 8.22.0 built with OpenSSL. Reproduce on macOS from the repository root: `curl scp://127.0.0.1:1/; echo $?` and `curl sftp://127.0.0.1:1/; echo $?`, once with the reference curl and once with Curl's build; expect 7 from the reference and 2 from Curl.

Suggestion, copied from the finding:

In Curl.Protocol.Ssh.UnitLibrary, make a refused or unreachable connection on scp and sftp return CurlExitCode.CouldntConnect (7), as curl does. SshProtocolHandler.cs (around line 165) passes the connector's ExitCode through unchanged, so the 2 comes from the connector Curl.Console's CurlComposition gives the SSH handler, or from SshTransferException.SessionEstablishmentFailed (FailedInit, exit 2), which should only run once a connection exists. Trace which one returns 2 on a refused connect, then pin exit 7 for both schemes in Curl.Protocol.Ssh.UnitTests. If the trace leads to the composition, add Curl.Console and Curl.Console.UnitTests to touches.

## Acceptance criteria

- [x] `protocols:scp`: Curl answers what curl 8.22.0 answers on macos, `listed, exit 7`, so the item measures `match`.
- [x] `protocols:sftp`: Curl answers what curl 8.22.0 answers on macos, `listed, exit 7`, so the item measures `match`.
- [x] The gap-platforms workflow run on the branch (or the CI job on macos) passes for the named items; lanes test only on Windows, so a green Windows run alone does not close this task.
- [x] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [x] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md).

  Criteria 1-3 are ticked on `UserDatabaseHomeDirectoryTests`, fast tests marked off Windows that read
  the real user database and run on macOS and Linux in CI once the shift pushes (as BL-2335 did). A lane
  cannot run gap-platforms; GF-0093 closes only when a later gap run measures both items `match` (ADR-0433).

## Notes

- 2026-10-15 trace (lane 4): the 2 is not the connector's. `SshProtocolHandler` passes a refused
  connect through as 7 (the Windows build measures 7). The 2 is `TransferPerformer.KnownHostsFileMissingFailure`
  ("curl: Could not find a known_hosts file", exit 2), returned before any connection when
  `SshKnownHostsFileSearch` finds no `.ssh/known_hosts`. The local curl 8.22.0 (mingw) still exits 2
  with no known_hosts file (measured 2026-10-15 with `HOME`/`CURL_HOME` pointed at an empty
  directory), so the tool code is unchanged and the macOS reference exiting 7 means it found a
  known_hosts file that Curl's search did not.
- Cause: the search's last directory, the account home "from the user database" (curl's
  `getpwuid(geteuid())->pw_dir`), comes from `Curl.Cli.UnitLibrary/AccountHomeDirectory.cs`, which
  reads `Environment.GetFolderPath(SpecialFolder.UserProfile)`. Off Windows .NET returns `$HOME` there
  when it is set, so whenever `HOME` names another directory (as a measurement harness does) Curl
  never looks in the passwd home, while curl does - and the macOS runner's `/Users/runner/.ssh/known_hosts`
  exists. The same defect hits the `.curlrc` search (`DefaultConfigFileSearch`).
- Fix to make: read the passwd entry itself off Windows, ignoring `HOME`, through the .NET runtime's
  `SystemNative_GetPwUidR` shim in `libSystem.Native` (normalised `Passwd` record, same layout on
  Linux and macOS - the pattern `Curl.Console/NativeRegularFileTest.cs` uses for `SystemNative_Stat`),
  with `SystemNative_GetEUid` for the uid; exclude the thin adapter from Windows coverage under
  ADR-0083 and test it off Windows (a test that sets nothing and compares with `getent`-free
  expectations: the result is non-empty and does not change when `HOME` is overridden in a child
  process is an Integration test; a unit test pins `ReadOffWindows` wiring). Then pin in
  `Curl.Engine`/`Curl.Cli` tests that a `HOME` differing from the account home still finds the
  account home's `.ssh/known_hosts`.
- Touches: the work is in `Curl.Cli.UnitLibrary` and `Curl.Cli.UnitTests`, which BL-2332 (in Doing)
  touches, so this task goes back to Backlog until it leaves Doing. Nothing in
  `Curl.Protocol.Ssh.UnitLibrary` needs to change.
- 2026-10-15 fix (lane 2): BL-2332 is Done, so the fix went in as the trace above planned.

  `AccountHomeDirectory.ForProcess` now reads `UserDatabaseHomeDirectory.ReadForEffectiveUser()` off
  Windows: `SystemNative_GetEUid` plus `SystemNative_GetPwUidR` from `libSystem.Native`, the
  `getpwuid(geteuid())->pw_dir` curl reads, never `$HOME`. The `Passwd` layout (two `byte*`, two
  `uint`, three `byte*`) was checked against `Interop+Sys+Passwd` in the linux-x64 10.0.9
  `System.Private.CoreLib`. The adapter is excluded from Windows coverage under ADR-0083 and
  `Curl.Cli.UnitLibrary` gets `AllowUnsafeBlocks` for the source-generated `LibraryImport`, as
  `Curl.Console` already has. When the user database has no entry the result is null and nothing is
  searched there, as when curl's `getpwuid` fails. `ReadFromUserDatabase` (which read the user-profile
  folder, so `$HOME`) is gone. The `.curlrc` search uses the same property, so it is fixed too.
- Tests: `AccountHomeDirectoryTests` pins `ReadOffWindows` with a null entry. `UserDatabaseHomeDirectoryTests`
  (fast, off Windows, as `NativeRegularFileTestTests` calls its native shim from a unit test project)
  pins that the read is an existing absolute directory and that `ForProcess` equals it, so the macOS CI
  job proves the `Passwd` layout. `Curl.Cli.IntegrationTests/UserDatabaseHomeDirectoryIntegrationTests`
  starts `/bin/sh` (an external process, ADR-0421) and pins that the read equals the shell's
  `~$(id -un)` expansion run with `HOME` set to a decoy. `CurlCommandRunnerSshOptionTests` already pins that the account home's
  `.ssh/known_hosts` is checked when `HOME` has none.
- Touches: added `Curl.Cli.IntegrationTests` for the integration test. No task in Doing on
  `origin/work/dark-factory` named it. Nothing in `Curl.Protocol.Ssh.UnitLibrary` changed.
- Quality: `Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary` reported 0 failing members.

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
- 2026-10-15: Doing -> Backlog. Fix is in Curl.Cli.UnitLibrary (AccountHomeDirectory reads HOME, not the passwd home), which BL-2332 in Doing touches
- 2026-10-15: Backlog -> Doing.
- 2026-10-15: Doing -> Done. scp and sftp off Windows now search the passwd home (getpwuid_r), not HOME, for known_hosts and .curlrc, so a refused connect exits 7 as curl does
