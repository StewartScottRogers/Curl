---
id: BL-1730
title: Cross-check runnable single-exchange upstream cases through the reference curl and Curl.Console with Gap/Tools/Measure-ReferenceCrossCheck.ps1
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1729, BL-1722]
touches: [Curl.Conformance.UnitLibrary/Curl.Conformance.UnitLibrary.csproj, Gap/Tools/Measure-ReferenceCrossCheck.ps1, Gap/Tools/Expand-UpstreamCase.cs, Gap/Tools/Fixtures/crosscheck, Record-CurlExchange.ps1]
requirement: none
created: 2026-10-08
completed: 2026-10-08
---
# BL-1730 — Cross-check runnable single-exchange upstream cases through the reference curl and Curl.Console with Gap/Tools/Measure-ReferenceCrossCheck.ps1

## Goal

`Gap/Tools/Measure-ReferenceCrossCheck.ps1` runs the upstream cases that need a single
plain-HTTP exchange through both real binaries, the matched reference curl and the built
`Curl.Console`, against a loopback server, using `Record-CurlExchange.ps1`. It then amends
the `behaviour` measurement. Where both binaries agree but the case's `<verify>` does not,
the case is `reference-diverges`, and Curl is judged against the reference instead. Where
the binaries disagree on a case the harness passed, it records a gap.

## Context

This is ADR-0433 decision 2. Stewart asked that the behaviour suite be run "through both
binaries where runnable". The in-process harness (BL-1728) cannot run the reference curl.
This cross-check covers the cases where both binaries can see the same server.

`Record-CurlExchange.ps1` (repository root) binds a loopback port, answers each connection
with a canned response, and writes `request.bin`, `stdout.bin`, `stderr.txt`,
`exitcode.txt` and `timing.json`. `-Curl` picks the binary, and by default it finds the
reference as `Invoke-GapProbe.ps1` (BL-1722) does. It also has FTP, SMTP, IMAP, POP3, TFTP
and TLS modes. Read its header help. Extend it, rather than writing a second server, when a
case needs something it lacks (CLAUDE.md, "No Python"). Any extension keeps its existing
behaviour, and its header help documents the new parameter.

**Which cases.** From the behaviour measurement (BL-1729), take the cases whose state is
`match` or `gap`, whose `<server>` is `http` alone, and whose `<reply>` is one `<data>`
part answering one connection (no `<dataN>`, no `<servercmd>`). Cases whose command needs
client files are included: create them as the harness does. Cases the selection leaves out
keep their in-process verdict. The measurement records how many were cross-checked and how
many were not, with each rule's count.

**Per case.**

1. Expand the case's variables as the harness does: `%HOSTIP` is `127.0.0.1`, `%HTTPPORT`
   is a free loopback port (the same one for both binaries), and `%LOGDIR` and
   `%TESTNUMBER` follow the same rules. Get the expanded file and the command line from the
   harness's public `UpstreamTestFileExpander` and `UpstreamTestCaseParser`, called from a
   small C# file-based app, `Gap/Tools/Expand-UpstreamCase.cs`, which sits in BL-1728's
   isolated `Gap/Tools/` build settings. Do not
   re-implement them.
2. Run the reference curl and `Curl.Console` through `Record-CurlExchange.ps1`, each with
   the `<data>` part as `-Response`, and compare the two binaries' request bytes, stdout,
   stderr and exit code with each other. Do not re-implement the harness's `<strip>`
   rules: the reference's own output is the yardstick here.
3. Combine that with the in-process verdict:
   - binaries agree and the verdict is `match`: unchanged;
   - binaries agree and the verdict is `gap`: the case's verify block is not what this
     platform's matched build produces, so the item becomes `match` with reason
     `reference-diverges` and the harness's first difference kept in `evidence`;
   - binaries differ and the verdict is `match`: the item becomes `gap`, with evidence
     `in-process and out-of-process differ`;
   - binaries differ and the verdict is `gap`: it stays `gap`, with the reference's output
     added as `expected`.

Output: an amended copy of the behaviour measurement (`-OutFile`), with every changed item's
previous state in `evidence`. A `-Limit` parameter caps the case count for quick runs.

**Self-test.** Fixtures under `Gap/Tools/Fixtures/crosscheck/`: two fake cases, plus canned
`Record-CurlExchange.ps1` outputs through a `-RecordedResults` folder, so no binary or socket
is needed. Cover the four combinations in step 3.

## Acceptance criteria

- [x] `Gap/Tools/Measure-ReferenceCrossCheck.ps1 -SelfTest` prints `PASS` lines and no `FAIL` under Windows PowerShell 5.1 and PowerShell 7, covering the four combinations in step 3 and the selection rule's counts.
- [x] A real run on Windows against the 8.21.0 behaviour measurement finishes. The numbers cross-checked, left out (by rule), `reference-diverges`, and in-process versus out-of-process disagreements are recorded in this task's Notes.
- [x] If `Record-CurlExchange.ps1` was extended, its existing header-documented modes still work, shown by re-running one recorded exchange from an existing test fixture with byte-identical output. Its header help documents the new parameter.
- [x] The header help documents every parameter and the selection rules. The script is ASCII only.

## Notes

- Touches widened (no task in Doing on origin/work/dark-factory names it):
  `Curl.Conformance.UnitLibrary/Curl.Conformance.UnitLibrary.csproj` gains
  `InternalsVisibleTo Include="Expand-UpstreamCase"`. The harness's command splitter
  (`UpstreamCommandLineSplitter`) and part decoding (`UpstreamTestPartBodies`) are internal;
  granting the file-based app access reuses them instead of re-implementing them, and adds no
  public API.
- `Expand-UpstreamCase.cs` takes a batch of case numbers in one `dotnet run`, so the run builds
  once. It applies the three expansion-side selection rules (not-parsed, server-not-http-alone,
  reply-not-one-data) and writes arguments, reply, stdin and client files as JSON.
- Decisions (sensible defaults): stderr is compared after dropping progress meter lines (their
  times differ run to run); each binary's command line starts `--max-time 20`
  (`-CaseTimeLimitSeconds`) because test25 (a redirect loop) hung the first real run for 40
  minutes; the server answers up to 20 connections with the same `<data>`, as sws does for a
  followed redirect. Record-CurlExchange.ps1 needed no extension (its -Response escapes carry
  any bytes), so it is unchanged and the third criterion holds trivially.
- Real run, Windows, reference `curl 8.21.0 (x86_64-w64-mingw32) ... Schannel`, against an
  in-process measurement of 8.21.0 cases 1-80 (match 58, gap 14, unmeasured 8), 246 s:
  cross-checked 47; left out by not-match-or-gap 8, limit 0, not-parsed 0,
  server-not-http-alone 2, reply-not-one-data 23; reference-diverges 3 (test17, test56,
  test73); disagreements 6 (test9, test39, test44 in-process match now gap; test60, test62,
  test71 stay gap with the reference's output as expected). test9/39/44 differ only in the
  random multipart boundary: filed BL-1791 to normalise it.
- Self-test: 10 PASS, 0 FAIL under Windows PowerShell 5.1 and PowerShell 7.


## Log

- 2026-10-08: Created.
- 2026-10-08: Backlog -> Doing.
- 2026-10-08: Doing -> Done. Measure-ReferenceCrossCheck.ps1 runs single-exchange HTTP cases through the reference curl and Curl.Console and amends the behaviour measurement
