---
id: BL-2968
title: Report Unicode on Windows once upstream cases 3203, 1703 and 472 pass under it
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2975]
touches: [Curl.Conformance.UnitLibrary/UpstreamWindowsFeatures.cs, Curl.Conformance.UnitTests/PassingUpstreamCases.txt, Curl.Protocol.File.UnitLibrary, Curl.Protocol.File.UnitTests, Curl.Authentication.UnitLibrary, Curl.Authentication.UnitTests]
requirement: none
created: 2026-10-21
completed:
---
# BL-2968 — Report Unicode on Windows once upstream cases 3203, 1703 and 472 pass under it

## Goal

The conformance harness reports `Unicode` on Windows, as Windows' `curl.exe` does, and upstream cases 3203, 1703 and 472 pass there under it.

## Context

ADR-0737 (BL-2858) keeps `Unicode` off. Adding `"Unicode"` to `UpstreamWindowsFeatures.Names` on 2026-10-21 and running each case with `$env:CURL_UPSTREAM_CASE` gave:

- test3203 (listed; `%if Unicode` names its file `漢字-ä.txt`): the `file://` directory listing came back as `\xC3\xA6\xC2\xBC...` - the name's UTF-8 bytes encoded as UTF-8 a second time. Find whether the harness writes the `<file name>` from Latin-1 or the `file://` listing encodes it twice.
- test1703 (a UTF-8 file name percent-encoded in a `file://` URL): `--output` file empty ("expected `foo\n`, got the end"): the decoded path's bytes are probably read as Latin-1, not UTF-8.
- test472 (`--aws-sigv4` with query `a=%e3%81%82`, `Debug` stand-in): `Signature=ea8d5ce0...` where the case expects `b8783c83...`; compare the canonical query string with curl 8.22.0's `http_aws_sigv4.c`. This may fail without `Unicode` too.

Where the fix lands outside `touches`, add the folder in the same commit. Each product fix also gets a focused unit test in its library's `.UnitTests` (ADR-0482). Update ADR-0737's decision 2 or supersede it.

## Acceptance criteria

- [ ] `UpstreamWindowsFeatures.Names` contains `Unicode`, and test3203 still passes on Windows.
- [ ] test1703 and test472 are on `PassingUpstreamCases.txt` and pass on Windows; test472 also on Linux and macOS in CI.
- [ ] Each product fix has a focused unit test in its library's `.UnitTests` project that fails without it.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and each changed library keeps 100% line and branch coverage and complexity at most 10.

## Notes

- 2026-10-21 (lane 3, BL-2975): ADR-0741 measured the Windows reference (mingw curl 8.22.0): it is
  not a `Unicode` build and sends `あ` as `?`, as Curl does. Curl keeps the ANSI argument model and
  its `-V` does not list `Unicode`, so ADR-0737's decision 2 stands and test472 stays off the
  ratchet. On resume, land the harness's UTF-8 case-word fix for test3203 and test1703 (list
  test1703) without adding `Unicode`, drop the `Unicode` and test472 criteria, and record why.

- 2026-10-21 (lane 5): Cause of 3203 and 1703 is the harness, not Curl. It reads every case one
  character per byte (Latin-1), so the `<file name>` `%hex[%E6%BC%A2...]hex%` was created on disk as
  the characters U+00E6 U+00BC ... and Curl listed that name correctly as UTF-8. The fix, left in this
  lane's stash: a new `Curl.Conformance.UnitLibrary/UpstreamUtf8Words.cs` (`AsSystemText`: the word's
  bytes read as strict UTF-8, else the word unchanged), applied to the client `<file name>` in
  `UpstreamCaseRunner.WriteClientFiles`, to each command argument in `UpstreamCaseRunner.Arguments`,
  and to the `<verify><file name>` in `UpstreamCaseVerification.CompareFile`, plus `"Unicode"` in
  `UpstreamWindowsFeatures.Names`. With it, test3203 and test1703 pass on Windows
  (`CURL_UPSTREAM_CASE` run). Those three Conformance files lie outside `touches`. No Doing task on
  origin/work/dark-factory names them, so add them to `touches` when resuming. Still to do on resume:
  unit tests for `UpstreamUtf8Words` (valid UTF-8, invalid bytes, ASCII), the full fast-test run to
  catch any listed case whose command holds high bytes that happen to be valid UTF-8, ADR-0737's
  decision 2 superseded, test1703 and test472 on the ratchet.
- test472 then sends `GET /472/a=? HTTP/1.1` on Windows: Curl's Windows build turns `あ` into `?`
  because it models curl receiving argv in the ANSI code page (ADR-0022). A Unicode `curl.exe`
  converts wide argv to UTF-8. That is a measured, cross-library change outside this task, filed as
  BL-2975. The task is requeued behind it.

## Log

- 2026-10-21: Created.
- 2026-10-21: Backlog -> Doing. Criteria at claim: de350832 ba7c7a94 6c1653fd dd1cc079.
- 2026-10-21: Doing -> Backlog. Waits on BL-2975: Curl on Windows turns the non-ASCII argument of test472 into '?' (ANSI argv model, ADR-0022); harness UTF-8 fix for 3203 and 1703 is in the stash
- 2026-10-21: Backlog -> Doing. Criteria at claim: de350832 ba7c7a94 6c1653fd dd1cc079.
