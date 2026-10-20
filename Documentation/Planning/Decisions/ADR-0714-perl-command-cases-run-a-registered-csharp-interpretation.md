# ADR-0714: A `<command type="perl">` case runs a registered C# interpretation of its script, with `%CURL` as an in-process token

- Status: Accepted
- Date: 2026-10-19
- Decided by Claude under Stewart's delegation (BL-2863)

## Context

Upstream cases 971, 1478, 1486, 1707, 1708 and 1710 of curl 8.22.0 run a Perl script
(`<command type="perl">%SRCDIR/test<n>.pl ...</command>`), some passing it `%CURL`, the curl under
test. The harness never runs Perl (the solution's no-Python, BCL-only rules), so these cases skipped
with `the harness does not run a perl command` or `the harness has no value for %CURL`.

## Decision

- `UpstreamCaseRunner` takes an optional `perlCommandScripts` map from a script's file name
  (`test1707.pl`, `UpstreamCaseScreening.PerlScriptName`: the command's first word after its last `/`)
  to a C# interpretation `Func<UpstreamCurlInvocation, Func<UpstreamCurlInvocation, Task<int>>, Task<int>>`.
  The interpretation runs in place of curl, under the same time limit and judged as a curl run is: its
  invocation's arguments are the command's words after the script, with the case's stdout and stderr,
  and it gets the runner's in-process curl delegate to call with an invocation of its own.
- `%CURL` is always `UpstreamCaseRunner.CurlProgram` (`curl`), a token with no program behind it, as
  `%LIBTESTS` is: only an interpretation gives it meaning. A case using `%CURL` anywhere else skips for
  its own reason (an uninterpreted check line, an unrun command type).
- `UpstreamCaseScreening.FindSkipReason` takes `interpretsPerlScript`; a perl command whose script
  has no interpretation still skips as `the harness does not run a perl command`.

## Why

It matches the existing dispatch of `UpstreamTest610Script`, `UpstreamTest613Script` and the libtest
ports: one seam, keyed by name, each interpretation hand-written and tested on its own (BL-2865,
BL-2866). A file-name key keeps the map independent of `%SRCDIR`'s value.
