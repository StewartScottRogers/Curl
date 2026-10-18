---
id: BL-2717
title: Re-close GF-0090: --ssl-sessions on Linux does not read the file and print the session-file warnings real curl prints (on linux)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
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

## Log

- 2026-10-17: Created.
- 2026-10-17: Backlog -> Doing. Criteria at claim: a577b127 8b83e917 1fd5de8c 28346828.
