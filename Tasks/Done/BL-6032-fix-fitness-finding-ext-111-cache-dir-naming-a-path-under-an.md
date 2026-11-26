---
id: BL-6032
title: Fix fitness finding EXT-111: --cache-dir naming a path under an existing file silently caches nothing, exit 0 (PARTLY FIXED)
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.HttpCache.UnitLibrary/CacheDirectory.cs, Curl.Extension.HttpCache.UnitTests/CacheDirectoryTests.cs, Curl.Extension.HttpCache.UnitTests/HttpCacheTransferHookTests.cs]
requirement: none
created: 2026-11-26
completed: 2026-11-26
---
# BL-6032 — Fix fitness finding EXT-111: --cache-dir naming a path under an existing file silently caches nothing, exit 0 (PARTLY FIXED)

## Goal

EXT-111 from the outside fitness review of 2026-11-26 07:04 no longer reproduces: --cache-dir naming a path under an existing file silently caches nothing, exit 0 (PARTLY FIXED). Curl behaves as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-26T0704` (commit `23b62c2`, the `preview` release binaries).

This is an extension (Part B: extended protocols and extension options): it is judged against Curl's own documentation (`curl --extended-help`, `Documentation/Protocols.md`, `Documentation/Conformance/Extended/*.md`, the ADRs), never against C curl.

The reviewer used Python mocks and real servers in Docker (pinned images listed in the review workspace's TestServers.md); rebuild any mock you need in PowerShell or C# (never Python). Mock servers bind 127.0.0.1 and port numbers are examples. Check that it still reproduces on the current branch first.

The finding, verbatim:

- **Severity:** Low · **Confidence:** Confirmed (Windows 2 runs, Linux 1) · **Area:** `--cache-dir`, both platforms.
- **What is fixed:** `--cache-dir <existing file>` now exits 2 with `curl: --cache-dir cannot use '<file>'`.
- **Repro:** `echo x > recfile`, then run `curl -sS --cache-dir recfile/sub http://127.0.0.1:27020/plain -o /dev/null` twice against a server whose `/plain` sends `Cache-Control: max-age=60`, and count the requests it receives.
- **Expected:** the same refusal as the fixed case (a message naming `--cache-dir` and `recfile/sub`, exit 2), or a `Warning:` line; with a real directory, two runs make one request.
- **Actual (.NET):** both runs exit 0 with no message, the server sees 2 requests, and `recfile/sub` does not exist.
- **Likely location:** `Curl.Extension.HttpCache.UnitLibrary/CacheDirectory.cs`: the check only asks whether the path itself is a file, not whether the directory can be created.
- **Acceptance check:** the repro prints a message naming `--cache-dir` and `recfile/sub` and exits 2 (or warns).

## Acceptance criteria

- [x] the repro prints a message naming `--cache-dir` and `recfile/sub` and exits 2 (or warns).
- [x] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Refuse rather than warn, the same answer as a path that is itself a file (ADR-1427).
- Added the two test files to `touches`; no other task in Doing names this library's tests.
- `ExecuteAsync_ACacheDirectoryThatCannotBeWritten_LeavesTheTransferAsItWas` now makes the blocking
  file after the hook has accepted the path, since the path under a file is refused at activation.
- Repro run with the built `curl.exe`: `curl: --cache-dir cannot use 'recfile/sub'`, the try-help
  line, exit 2 (no server needed: the refusal comes before the transfer).
- Fast tests: the full run failed only on the test above; after its change its project passed
  (106 of 106). Quality for the library: 100% line and branch, 0 failing members.
- The tests join paths with `/` and name no OS text, so they hold on Linux and macOS too.

## Log

- 2026-11-26: Created from fitness review 2026-11-26T0704.
- 2026-11-26: Backlog -> Doing. Criteria at claim: 93878311 69116afe 3d836d5e.
- 2026-11-26: Doing -> Done. path under an existing file is refused, exit 2
