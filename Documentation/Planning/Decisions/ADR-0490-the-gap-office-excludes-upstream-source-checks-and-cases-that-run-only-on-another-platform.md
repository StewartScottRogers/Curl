# ADR-0490: The gap office excludes upstream source checks and cases that run only on another platform

- Status: Accepted
- Date: 2026-10-14
- Task: BL-2160 (gap finding GF-0038)
- Decided by Claude under Stewart's delegation

## Context

GF-0038 lists the 154 `tests/data` cases that curl 8.22.0 added or changed. The lane that
split BL-2160 left the cases Curl's in-process harness cannot run with a release curl to an
interactive session, because only the gap office (`Gap/`, interactive only) can record them
as `excluded`. The 2026-10-13 run already excluded the libtest (`libcurl-api`), debug-build
and `unittest` (`debug-build-only`) cases. Two kinds were still `unmeasured`, counting
against the behaviour score though no change to Curl could ever measure them:

- `<command type="perl">` cases that run a perl script over upstream's own source tree,
  documents or test scripts and never start curl (test1177 checks `CURL_VERSION_*` against
  `curl.h` and `version.c`; test1119, 1135, 1139, 1140, 1165, 1167, 1173, 1175, 1185, 1222,
  1275, 1276, 1279, 1477, 1478, 1486, 1488, 1544, 745 and 971 are the same kind; 1169 and
  1182 check `runtests.pl`; 1705 and 1706 check `managen`). Cases 1707, 1708 and 1710 also
  use a perl command but pass it `%CURL`: they run curl, so they are curl behaviour.
- Cases that run only off Windows, measured on Windows: `!win32` in `<features>` (test469,
  1056, 1714, 3016, 969, and 955 to 968, which were excluded as `reference-lacks:codeset-utf8`
  only by accident of rule order) and test2072, whose `<precheck>` refuses `MSWin32` ("Test
  requires a Unix system").

## Decision

1. A skipped case whose `<command type="perl">` never names `%CURL` is `excluded` with the new
   reason `upstream-source-check` (rule 2a of `Gap/Tools/ConvertTo-BehaviourMeasurement.ps1`,
   after `libcurl-unit-test`). It tests upstream's source and scripts, not curl's behaviour.
2. On Windows, a skipped case whose `<features>` lists `!win32`, or whose `<precheck>` refuses
   `$^O eq 'MSWin32'`, is `excluded` as `platform:unix`; elsewhere a skipped case that needs
   `win32` is `excluded` as `platform:windows` (rule 3a, after `debug-build-only` and before
   `reference-lacks`). The same case is measured on the platform it belongs to.
3. Test321 (`--etag-save -` with the shell's `>>`) stays `unmeasured` as
   `harness-unsupported`: it is curl behaviour a release curl shows, so the harness should run
   it rather than the office exclude it.

## Consequences

About 30 items leave the behaviour area's Y, so its percentage rises without any change to
Curl; the scorecard's reason table shows them under the two reasons. GF-0038's test1177 and
test2072 can close on the next run. A future upstream source check is excluded without a new
decision; a perl command that starts curl is still measured.
