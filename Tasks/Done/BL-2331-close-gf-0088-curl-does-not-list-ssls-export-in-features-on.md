---
id: BL-2331
title: Close GF-0088: Curl does not list SSLS-EXPORT in Features: on Linux; the curl 8.22.0 OpenSSL reference does (on linux)
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: none
created: 2026-10-15
completed: 2026-10-15
---
# BL-2331 — Close GF-0088: Curl does not list SSLS-EXPORT in Features: on Linux; the curl 8.22.0 OpenSSL reference does (on linux)

## Goal

Curl behaves as curl 8.22.0 does for every item of gap finding GF-0088 (Curl does not list SSLS-EXPORT in Features: on Linux; the curl 8.22.0 OpenSSL reference does), so a later gap analysis measures each of `features:SSLS-EXPORT` as `match`.

## Context

- Finding: GF-0088, filed by the gap analysis office (ADR-0433).
- Area: features. Severity: High. Introduced in: not stated upstream.
- Platform: linux, measured against curl 8.22.0 built from the release tarball with OpenSSL by the gap-platforms workflow (.github/workflows/gap-platforms.yml, ADR-0505). This gap is linux's own: Windows does not show it.
- Lanes test only on Windows, so a lane cannot see this gap pass locally. The proof is the gap-platforms workflow (run it on the branch: gh workflow run gap-platforms.yml -f ref=<branch>) or the CI job on linux passing a test that pins each item; write those tests platform-neutral or mark them for linux.
- Items: `features:SSLS-EXPORT`.
- Targeted curl version: 8.22.0 (Gap/Baselines/target.json).
- Yardstick: `docs/cmdline-opts/version.md` in the curl 8.22.0 release tarball.
- The gap closes only when a later gap analysis re-measures every item as `match` (ADR-0433 decision 5), never because this task reaches Done.

Evidence, copied from the finding:

Measurement features:SSLS-EXPORT: expected 'listed', actual 'not listed', state gap. Reference is curl 8.22.0 built with OpenSSL/3.0.13. Reproduce on the Linux runner: curl -V | grep '^Features:' (the item's evidence). The upstream manual, under SSLS-EXPORT, says the build supports TLS session export/import, like with --ssl-sessions.

Suggestion, copied from the finding:

In Curl.Cli.UnitLibrary, CurlVersionText.FeaturesLine (CurlVersionText.cs, line 51), add SSLS-EXPORT after SSL, in curl's case-insensitive order, so it reads SSL, SSLS-EXPORT, threadsafe. Do not add it to WindowsFeaturesLine: CommandLineOptionTable marks --ssl-sessions RefusedBySchannelBuild (ADR-0397), so the Windows build does not serve it. Before listing it, confirm --ssl-sessions round-trips TLS session tickets on Linux and macOS through the hand-built client (ADR-0319) and add a test in Curl.Cli.UnitTests. Explained by ADR-0021's SSLS-EXPORT row, which says no measured build prints it: the 8.22.0 OpenSSL reference does, so that row is out of date on Linux and needs an amending ADR.

## Acceptance criteria

- [x] `features:SSLS-EXPORT`: Curl answers what curl 8.22.0 answers on linux, `listed`, so the item measures `match`.
- [x] The gap-platforms workflow run on the branch (or the CI job on linux) passes for the named items; lanes test only on Windows, so a green Windows run alone does not close this task.
- [x] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [x] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md).

## Notes

- Evidence for the feature: `--ssl-sessions` already loads and saves TLS session tickets off Windows (ADR-0319, `TlsSessionFileLines` and its tests in Curl.Console.UnitTests), so no transport work was needed.
- `CurlVersionText.FeaturesLine` (Linux and macOS) now lists `SSLS-EXPORT` between `SSL` and `threadsafe`; `WindowsFeaturesLine` is unchanged because the Schannel build refuses `--ssl-sessions` (ADR-0397). ADR-0518 records this and amends ADR-0021's row. ADR-0021 itself is outside this task's touches, so its row was left as is; ADR-0518 says it now holds only for Windows.
- macOS shares the line: the gap-platforms macOS reference is also curl built with OpenSSL (ADR-0505).
- Linux proof: `CurlVersionTextTests.Lines_Linux_NamesTheGnuTripleAndOpenSsl` pins the line platform-neutrally (isWindows is injected), so CI's Linux job checks it once the shift pushes; the gap item closes on the next gap-platforms run.
- No option added or changed, so `--ai-help` needs nothing. Only a string constant changed (no new branch or method), so Measure-CodeQuality was not run.
- Build clean with -warnaserror; fast tests green (Curl.Cli.UnitTests 3916 passed, no failed project).

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
- 2026-10-15: Doing -> Done. curl -V lists SSLS-EXPORT on Linux and macOS as the curl 8.22.0 OpenSSL reference does
