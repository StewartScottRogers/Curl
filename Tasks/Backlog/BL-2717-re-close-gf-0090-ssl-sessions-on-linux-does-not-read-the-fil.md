---
id: BL-2717
title: Re-close GF-0090: --ssl-sessions on Linux does not read the file and print the session-file warnings real curl prints (on linux)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
lane: no
requirement: none
created: 2026-10-17
completed:
---
# BL-2717 — Re-close GF-0090: --ssl-sessions on Linux does not read the file and print the session-file warnings real curl prints (on linux)

## Goal

Curl behaves as curl 8.22.0 does for every item of gap finding GF-0090 (--ssl-sessions on Linux does not read the file and print the session-file warnings real curl prints), so a later gap analysis measures each of `options:--ssl-sessions` as `match`.

## Context

This is a Re-close task: every earlier task for GF-0090 ([BL-2333]) is Done, but the latest gap analysis still measures a gap, so the fix did not hold.

- Finding: GF-0090, filed by the gap analysis office (ADR-0433).
- Area: options. Severity: Medium. Introduced in: 8.12.0.
- Platform: linux, measured against curl 8.22.0 built from the release tarball with OpenSSL by the gap-platforms workflow (.github/workflows/gap-platforms.yml, ADR-0505). This gap is linux's own: Windows does not show it.
- Lanes test only on Windows, so a lane cannot see this gap pass locally. The proof is the gap-platforms workflow (run it on the branch: gh workflow run gap-platforms.yml -f ref=<branch>) or the CI job on linux passing a test that pins each item; write those tests platform-neutral or mark them for linux.
- Items: `options:--ssl-sessions`.
- Targeted curl version: 8.22.0 (Gap/Baselines/target.json).
- Yardstick: `docs/cmdline-opts/*.md` in the curl 8.22.0 release tarball.
- The gap closes only when a later gap analysis re-measures every item as `match` (ADR-0433 decision 5), never because this task reaches Done.

Evidence, copied from the finding:

curl --ssl-sessions <tempfile> (placeholder file): expected 'exit 2: Warning: unrecognized line 1 in SSL session file | Warning: <file> | Warning: invalid shmax base64 encoding in line 2 | ... | curl: (2) no URL specified ...', actual 'exit 2: curl: (2) no URL specified | curl: try ...'. Reproduce on linux: curl --ssl-sessions <tempfile>. CommandLineOptionTable.cs line 268 stores SslSessionsFile with RefusedBySchannelBuild(), so on Linux the file is stored but never loaded or parsed.

Suggestion, copied from the finding:

In Curl.Cli.UnitLibrary, where SslSessionsFile (CommandLineGlobalState, CommandLineOptions) is applied on non-Schannel builds, load the file when the option is parsed, before the no-URL check, and print curl's warnings to standard error: 'Warning: unrecognized line N in SSL session file', 'Warning: <path>' and 'Warning: invalid shmax base64 encoding in line N', following the session-file format in docs/cmdline-opts/ssl-sessions.md. The TLS library that holds session tickets also needs the load and save. The cause is OpenSSL-build-specific (Schannel refuses the option). Measure real curl for the exact warning text and line rules. Keep `--ai-help` right for the options this changes (CLAUDE.md).

## Acceptance criteria

- [ ] `options:--ssl-sessions`: Curl answers what curl 8.22.0 answers on linux, `exit 2: Warning: unrecognized line 1 in SSL session file  | Warning: /tmp/gap-options-df8a0141de8e414e95913bfa3d95fbc5/placeholder.txt | Warning: invalid shmax base64 encoding in line 2 | Warning: invalid shmax base64 encoding in line 3 | Warning: unrecognized line 4 in SSL session file  | Warning: /tmp/gap-options-df8a0141de8e414e95913bfa3d95fbc5/placeholder.txt | Warning: unrecognized line 5 in SSL session file  | Warning: /tmp/gap-options-df8a0141de8e414e95913bfa3d95fbc5/placeholder.txt | Warning: unrecognized line 6 in SSL session file  | Warning: /tmp/gap-options-df8a0141de8e414e95913bfa3d95fbc5/placeholder.txt | Warning: unrecognized line 7 in SSL session file  | Warning: /tmp/gap-options-df8a0141de8e414e95913bfa3d95fbc5/placeholder.txt | Warning: unrecognized line 8 in SSL session file  | Warning: /tmp/gap-options-df8a0141de8e414e95913bfa3d95fbc5/placeholder.txt | Warning: unrecognized line 9 in SSL session file  | Warning: /tmp/gap-options-df8a0141de8e414e95913bfa3d95fbc5/placeholder.txt | Warning: unrecognized line 10 in SSL session file  | Warning: /tmp/gap-options-df8a0141de8e414e95913bfa3d95fbc5/placeholder.txt | Warning: unrecognized line 11 in SSL session file  | Warning: /tmp/gap-options-df8a0141de8e414e95913bfa3d95fbc5/placeholder.txt | Warning: unrecognized line 12 in SSL session file  | Warning: /tmp/gap-options-df8a0141de8e414e95913bfa3d95fbc5/placeholder.txt | Warning: unrecognized line 13 in SSL session file  | Warning: /tmp/gap-options-df8a0141de8e414e95913bfa3d95fbc5/placeholder.txt | Warning: unrecognized line 14 in SSL session file  | Warning: /tmp/gap-options-df8a0141de8e414e95913bfa3d95fbc5/placeholder.txt | curl: (2) no URL specified | curl: try 'curl --help' or 'curl --manual' for more information`, so the item measures `match`.
- [ ] The gap-platforms workflow run on the branch (or the CI job on linux) passes for the named items; lanes test only on Windows, so a green Windows run alone does not close this task.
- [ ] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [ ] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md).

## Notes

- 2026-10-17 (lane 3): Interactive only. No product change is needed; the gap is the
  measurement's order, and the fix is in the gap office's tools, which lanes may not read or
  change (ADR-0433, BL-1209). Evidence:
  - The gap-platforms run 39100480741 measured commit 16c54eb48, which already holds BL-2333's
    fix (401435779): Curl loads the file before the no-URL refusal and saves it after
    (`CurlCommandRunner.WriteRefusalAsync`, pinned on every platform by
    `TlsSessionFileLinesTests.RunAsync_NoUrlWithSslSessions_WarnsAboutTheFileBeforeTheRefusalAndSavesIt`).
    `TlsSessionCache.Import`'s line rules match curl 8.22.0's `my_get_line` (blank, blank-only
    and `#` lines skipped and not counted), and `WarningLineWrapper` already wraps
    `... in SSL session file ` / `Warning: <path>` as the expected text shows.
  - curl 8.22.0's `operate()` calls `tool_ssls_save` after `run_all_transfers` whatever it
    returned, and `tool_ssls_save` opens the file with `FOPEN_WRITETEXT` before exporting, so a
    no-URL run with no sessions leaves the file empty (src/tool_operate.c, src/tool_ssls.c).
  - The options measurement uses one placeholder for both binaries: the same
    `/tmp/gap-options-<id>/placeholder.txt` appears in curl's and Curl's `--config` lines. So
    curl runs first, prints the warnings and empties the file; Curl then reads an empty file and
    rightly prints nothing, exactly what curl itself prints on a second run.
- Remove `lane: no` once an interactive session has changed `Gap/Tools/Measure-OptionGap.ps1`
  to write the placeholder afresh before each binary's run (or give each binary its own copy),
  and a gap-platforms run re-measures `options:--ssl-sessions`. If it then measures `match`,
  this task needs no lane: tick criteria 1 and 2 on that run and finish it.

## Log

- 2026-10-17: Created.
- 2026-10-17: Backlog -> Doing. Criteria at claim: a577b127 8b83e917 1fd5de8c 28346828.
- 2026-10-17: Doing -> Backlog. Interactive only: Curl already matches curl; the gap office's option measurement shares one placeholder that curl's session save empties before Curl runs (see Notes)
