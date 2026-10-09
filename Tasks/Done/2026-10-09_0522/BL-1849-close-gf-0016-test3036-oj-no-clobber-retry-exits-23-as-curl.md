---
id: BL-1849
title: Close GF-0016 test3036: -OJ --no-clobber --retry exits 23 as curl 8.21.0 does
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console, Curl.Console.UnitTests]
lane: no
requirement: none
created: 2026-10-08
completed: 2026-10-09
---
# BL-1849 — Close GF-0016 test3036: -OJ --no-clobber --retry exits 23 as curl 8.21.0 does

## Goal

Curl behaves as curl 8.21.0 does for upstream test3036 (`--no-clobber --output-dir ... -OJ --retry 1 --retry-all-errors`), so a later gap analysis measures `behaviour:test3036` of GF-0016 as `match`.

## Context

- Split from BL-1809, which closed test1642 and test1643 (`-J -L` names the file after the last `Location`).
- Interactive only (`lane: no`): the upstream test case sits in the gap analysis office's upstream cache, which the audit guard refuses to dark factory lanes, so a lane cannot read what the test sends. The reproduce command is in GF-0016.
- Finding evidence: the reference curl exits 23 with 0 bytes on stdout; Curl's stderr differs. Suggestion: with `--no-clobber` and `--retry`, fail on an existing file with exit 23 and the reference's message.
- Start at `RemoteHeaderNameStream.TryOpenAsync` (an output already open when a `Content-Disposition` arrives fails with exit 23 and no warning) and `DeferredOutputFileStream.TryOpenUnderNameAsync` (`--no-clobber` numbering); check what a `--retry` attempt does to the `-J` file the attempt before it opened.

## Acceptance criteria

- [x] `behaviour:test3036`: Curl answers what curl 8.21.0 answers (exit 23, stdout 0 bytes, the same stderr), pinned by a `CurlCommandRunner` unit test.
- [x] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.

## Notes

- 2026-10-09 (dark factory lane 2): the Context already says this task is interactive only, but the front matter had no `lane: no`, so `next` offered it to a lane. The server exchange test3036 needs (what the server sends, the expected stderr) is only in the gap office's upstream cache, which lanes must not read, and guessing it would pin unmeasured output. Added `lane: no` and returned it to Backlog untouched; no code was changed. An interactive session should read the test case, reproduce it with `Record-CurlExchange.ps1` against the local curl 8.21.0 (Schannel), and pin it.

- 2026-10-09 (interactive): measured with Record-CurlExchange.ps1 against curl 8.21.0 (Schannel), `--output-dir` naming a file. curl reports each attempt for itself: progress zero line, `Warning: Failed to open the file <dir>/MMM3036MMM: No such file or directory`, `curl: (23) client returned ERROR on write of 52 bytes` (the `Content-Disposition` line), the retry warning, then the second attempt's warning and `write of 6 bytes` (the body: curl's `honor_cd_filename` is already off, so the retry takes no name from its headers and opens the named file at its first body write). Curl printed the handler's `Failure writing output to destination, passed 134 returned 0` with no warning for the first attempt, failed the second at the header (52 bytes), and drew an extra `00:01` meter line because a retry's progress started before the wait. Fixed in `DeferredOutputFileStream` (`SettleRetriedAttempt`, `NamedByContentDisposition`), `RemoteHeaderNameStream` (reads the name once per transfer) and `CurlCommandRunner` (settles each retried attempt's file, starts the next attempt's progress after the wait). Stderr is now byte-identical to curl's (also under `-sS` and `--no-progress-meter`); pinned by `CurlCommandRunnerRetryTests.RunAsync_RetriedRemoteHeaderNameUnderAFile_ReportsEachAttemptsOpenFailure`, `..._Silent_PrintsOnlyEachAttemptsErrorLine` and `RunAsync_RetriedAttemptsProgressMeter_StartsAfterTheRetryWait`. Gap harness: test3036 Passed (it passed before too: runtests checks only the exit code and requests, not stderr).
## Log

- 2026-10-08: Created.
- 2026-10-09: Backlog -> Doing.
- 2026-10-09: Doing -> Backlog. Interactive only: test3036's exchange is in the gap office's upstream cache, which lanes may not read; added lane: no
- 2026-10-09: Backlog -> Doing. Interactive session claims it
- 2026-10-09: Doing -> Done. -OJ --no-clobber --retry under a file reports each attempt's open failure and exits 23 byte for byte as curl 8.21.0 (test3036)
