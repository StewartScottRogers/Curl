# ADR-0458 — The upstream harness rewrites %PWD/%LOGDIR and %SRCDIR/libtest before expansion

- Status: Accepted
- Date: 2026-10-09
- Decided by Claude under Stewart's delegation (BL-1944)

## Context

`runtests.pl` runs in the `tests` folder: `%PWD` is that folder, `%LOGDIR` is the relative
`log`, and `%SRCDIR` is `$srcdir`, `.` by default. So `%PWD/%LOGDIR/x` names `tests/log/x`.
The case runner in `Curl.Conformance.UnitLibrary` gives `%LOGDIR` the case's own absolute log
directory, so cases run in parallel. With an absolute `%LOGDIR`, no value of `%PWD` makes
`%PWD/%LOGDIR/x` a real path on every platform (`/tests//abs/log` off Windows,
`Z:/tests/Z:/log` on Windows); 31 vendored cases write it. test1445's precheck and postcheck
(`%PERL %SRCDIR/libtest/test613.pl ... %PWD/%LOGDIR/test%TESTNUMBER.dir`) were skipped for it,
and its postcheck reads curl's output as `%LOGDIR/curl%TESTNUMBER.out`, where the runner wrote
`%LOGDIR/curl.out`.

## Decision

1. Before expansion, `UpstreamTestDirectoryComposition.Rewrite` replaces the text
   `%PWD/%LOGDIR` with `%LOGDIR`, which names the same file under the case's log directory on
   Windows, Linux and macOS. `%PWD` anywhere else is left as written, so it still has a value
   only when the caller names a tests directory (the 8 cases using `%PWD` alone are unchanged).
2. `%SRCDIR/libtest/test610.pl` and `%SRCDIR/libtest/test613.pl` become `./libtest/...`, as
   `runtests.pl`'s default `$srcdir` names them: these are the two scripts the harness emulates.
   `%SRCDIR` is given no value, so every other use (`%SRCDIR/data/...`, `%SRCDIR/../docs/...`,
   the libtest scripts not emulated) still skips the case with "the harness has no value for
   %SRCDIR" rather than failing on a file that is not vendored.
3. curl's output file is `%LOGDIR/curl%TESTNUMBER.out`, as `runtests.pl` names it.

A textual rewrite was chosen over a `%PWD` value with a sentinel because it needs no change to
variable substitution and is exact: the composition is spelled the same way in every case.

## Consequences

- test1445 now skips only on its `test613.pl` check line, which BL-1894 routes to
  `UpstreamTest613Script`; BL-1894 measures it.
- `Gap/Tools/Expand-UpstreamCase.cs` still names `%LOGDIR/curl.out`; it is an audit path,
  changed only on the `gap` branch.
