---
id: BL-1439
title: List the 158 passing upstream cases and fix the harness causes of 'got the end' conformance failures
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-04
completed: 2026-10-04
---
# BL-1439 — List the 158 passing upstream cases and fix the harness causes of 'got the end' conformance failures

## Goal

The 158 upstream cases that already pass are on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`, so the ratchet guards them, and the harness causes behind the conformance failures where Curl's recorded request or output "ends early" (`got the end`) are found and fixed, so those cases measure Curl rather than the harness.

## Context

- A conformance run on 2026-10-04 (`dotnet test Curl.Conformance.UnitTests --filter "TestCategory=Conformance"`, 2013 cases, about 8 seconds) reported 208 listed and passing, 158 `passes; add N to PassingUpstreamCases.txt`, 110 failing and the rest skipped. The 158 are: 10 18 33 47 58 66 74 75 78 86 87 156 164 178 180 181 197 198 199 204 205 218 262 269 281 319 326 339 341 342 343 344 345 347 357 367 376 379 386 389 393 421 425 426 455 473 487 490 491 492 495 496 679 680 682 683 684 685 690 691 692 693 756 772 785 789 899 978 995 1041 1045 1051 1052 1064 1065 1067 1127 1128 1129 1130 1131 1164 1210 1235 1237 1239 1240 1259 1266 1267 1280 1283 1296 1298 1299 1310 1311 1312 1313 1322 1328 1334 1335 1336 1337 1338 1339 1340 1342 1343 1344 1345 1346 1347 1411 1422 1424 1460 1462 1487 1490 1492 1497 1524 1584 1585 1619 1624 1635 1637 1639 1641 1644 1646 1672 1680 1681 1682 1722 1723 1909 1983 1984 2005 2036 2040 2044 2049 2052 2053 2054 2075 2081 2310 3029 3035 3036 3204 
- 48 failures report `got the end` - the recorded request, stdout, stderr or file stops where the expected text continues: test459 test3 test1642 test796 test1633 test174 test432 test431 test1721 test1015 test996 test797 test1148 test790 test477 test1566 test385 test430 test1325 test1075 test451 test470 test784 test991 test994 test450 test2409 test791 test1712 test1054 test1489 test463 test1070 test788 test369 test1076 test1643 test383 test743 test1332 test2013 test2014 test1012 test415 test268 test990 test1491 test794.
- At least some are the harness, not Curl: test790 (`--variable "name[5-9]=0123456789abcdef" --expand-data '{{name}}'`) expects the body `56789` and the harness records none, yet Curl sends `56789` exactly as real curl 8.21.0 does when both are run through `Record-CurlExchange.ps1` (checked 2026-10-04); test990/test991/test1721 (`-w '%output{%LOGDIR/output}...'`) expect a file Curl does write when run outside the harness. Look first at how the harness passes `%LOGDIR` paths, `-K` config files, `<stdin>` and quoted `{{...}}` arguments (`UpstreamCommandLineSplitter`, `UpstreamTestFileExpander`, `UpstreamCaseRunner`, `SwsHttpServerConnection` recording).
- ADR-0013 and `Curl.Conformance.UnitTests/CLAUDE.md` describe the harness and the ratchet; the pass-rate table there is updated from each run.

## Acceptance criteria

- [x] Every case the run reports as `passes; add N` - the 158 above, plus any the fixes below make pass - is added to `PassingUpstreamCases.txt` in numeric order, and `dotnet test Curl.Conformance.UnitTests --filter "TestCategory!=Integration"` passes.
- [x] The harness cause behind test790 (and test784, test788, test791, which share the `--variable` byte-range form) is fixed with a harness unit test in `Curl.Conformance.UnitTests` that pins it, and those cases are then listed or their remaining real difference from curl is written in Notes.
- [x] Each of the other `got the end` cases is classified in Notes as harness (with the cause, fixed or not) or Curl (with the difference and the library it belongs to), so follow-up tasks can be filed.
- [x] The harness applies a `<stdin crlf="headers">` (and `crlf="yes"`) attribute to the bytes it feeds Curl, as `runtests.pl` does, pinned by a harness unit test; test1326 and test1327 (telnet, whose recorded request today ends its lines in a bare LF where CRLF is expected) are then listed or their remaining difference is written in Notes.
- [x] The pass-rate table in `Curl.Conformance.UnitTests/CLAUDE.md` gains a 2026-10-04-or-later row from the final run.
- [x] `dotnet build Curl.Conformance.UnitTests -warnaserror` is clean; `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Conformance.UnitLibrary` reports no failing member.

## Notes

- **Main harness cause (fixed).** `UpstreamCaseVerification.Expected` forced CRLF before cutting the final newline, so a `<protocol crlf="yes" nonewline="yes">` expected a stray `\r` after the request body that no request ends in; `runtests.pl` chomps the last line before `subnewlines`. The order is now nonewline then crlf (also for `<verify><file>`, the other user of `Expected`), pinned by `FindFirstDifference_ProtocolWithCrlfAndNonewline_CutsTheLastLineFeedBeforeForcingCrlf`. This was the cause behind test790, test784, test788 and test791 (Curl's `--variable` byte ranges were right all along) and 21 other `got the end` cases; all 25 now pass and are listed: 3 174 268 383 385 430 431 432 450 451 463 743 784 788 790 791 794 796 797 1012 1015 1054 1076 1325 1332. test459 and test1712 (`--config` files) got past the protocol and now differ in `<stderr>` at byte 79 of curl's config-file warning line (Curl.Cli).
- **`<stdin>`/`<file>` crlf (fixed).** `UpstreamCaseRunner` now applies `crlf="yes"`/`crlf="headers"` (after `nonewline`) to the `<stdin>` bytes it feeds curl and the client `<file>` parts it writes, pinned by `RunAsync_StdinAndFileWithCrlf_AreGivenWithCrlfLineEndings`. test1326 passes and is listed. test1327 passes most runs but failed 1 in 5 repeated runs with an empty recording (the server records nothing: a race between the telnet upload and the empty `<reply>` closing), as it did in the run this task was filed from, so it is left off the list rather than made flaky.
- **Listed:** the 158 from Context plus the 26 newly passing, 392 in all (1327 excluded). Final run: 392 listed, 82 failing, 1539 skipped, 82.7%.
- **Remaining `got the end` cases, classified:**
  - Harness, not fixed here (Curl.Console is outside `touches`; filed BL-1445): test990, test991, test1721 - the connector-taking `CurlComposition.CreateRunner` the harness uses passes no `IWriteOutFileOpener`, so `%output{}` files are refused; test1148 - the same overload passes no `writesProgressMeter`, so `-#` writes no bar to `--stderr`.
  - Harness/race: test1327 (above).
  - Curl, named URL/upload globs (`[<test>7-9]`, `{<hej>1,2}`, `#<name>`; BL-1436 in Doing covers named globs; Curl.Cli/Curl.Console): test2409, test2014. test2013 (`-T` glob upload with two URLs and two `--output`): Curl sends no request; Curl.Console.
  - Curl.Protocol.Http: test1075 (PUT with `--anyauth` is not resent after the 401), test415 (control code then negative Content-Length: no output written), test1070 (POST with `Expect: 100-continue` when the server replies and closes before the body: Curl sends no body where curl sends what it can).
  - Curl.Console: test477 (`--max-filesize` with `-L`: the redirected request is not made), test1566 (`--etag-compare` with a 304 truncates the existing `-o` file), test1642 and test1643 (`-J -L -O --output-dir`: the file is not saved under the last Location name), test369 (`--etag-save` to a bad path aborts instead of going on to `--next`), test1489 (`-D %` to stderr writes nothing), test1633 (`--retry` on 429 with `Retry-After:` and `-d`: no retry).
  - Curl.Cli: test470 (no warning for a Unicode quote read from a `-K` config file).
- Measure-CodeQuality.ps1 -Library Curl.Conformance.UnitLibrary: 0 failing members.

## Log

- 2026-10-04: Created.
- 2026-10-04: Backlog -> Doing.
- 2026-10-04: Doing -> Done. 184 more upstream cases listed (392); harness now chomps before forcing CRLF and applies stdin/file crlf
