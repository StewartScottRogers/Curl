# ADR-0656: A lib tool case runs its ported ILibTest program in place of curl

- Status: Accepted
- Date: 2026-10-16
- Decided by Claude under Stewart's delegation
- Task: BL-2556

## Context

Upstream's `runtests.pl` runs a case whose `<client><tool>` names a program in place of curl:
`lib%TESTNUMBER` or `libNNN` is a libtest program, and the case's `<command>` words are its
arguments, with none of the `--output`, `--include` or trace options it puts before a curl
command. Its stdout and stderr are captured and judged against `<verify>` as for curl. Of the
vendored 8.22.0 cases, 371 have a `<tool>`: all but two name `libNNN` or `lib%TESTNUMBER`; one
names `unitNNNN` and one `tool%TESTNUMBER`. Until now screening skipped every one of them with
"the harness does not act on <client><tool>". Phase 5 of `Curl-Api-And-libcurl.md` ports the
programs as `ILibTest` classes in `Curl.Conformance.LibTest.UnitLibrary`, which needs the runner
to run them.

## Decision

1. `LibTestPrograms` in `Curl.Conformance.LibTest.UnitLibrary` is the table of ported programs,
   keyed by `ProgramName` (`lib500`), built from an explicit list - never a reflection scan, since
   production projects publish native AOT. `LibTestPrograms.Ported` is the one table each port task
   appends to; it starts empty. Keyed by name rather than test number because `libNNN` names a
   program shared by several cases, and the name is what the `<tool>` line says.
2. `Curl.Conformance.UnitLibrary` references the LibTest library (never the other way round), and
   `UpstreamCaseRunner` takes an optional `UpstreamLibTests(Programs, CreateEngine)`. Screening
   accepts `<tool>`: a `libNNN` program in the table runs; a `libNNN` program not in it skips with
   "libtest program not ported"; any other tool (`unitNNNN`, `toolNNNN`) keeps "the harness does
   not act on <client><tool>". The tool check sits where the old `<tool>` part check sat, so an
   unported lib case skips for the same reason on every platform.
3. A ported program runs on the same `UpstreamCurlInvocation` a curl run gets - the case's
   connectors, environment and standard streams - with the command's words alone as its arguments,
   on its own thread under the same time limit, and is judged by the same `Judge`: stdout,
   protocol, `<stripfile>` parts and exit code. `LibTestContext` carries the `Engine`
   (`CreateEngine(invocation)`; the program's `curl_global_cleanup` disposes it), `StandardOutput`
   and `StandardError` (a line-feed-only UTF-8 writer, so `<stderr>` compares the same on every
   platform, as curl's own stderr does in the harness).
4. Making a real `Libcurl` engine over the in-memory servers for `UpstreamConformanceTests` is its
   own task (filed with this one): until a program is ported, the ratchet gives the runner no
   `UpstreamLibTests`, and every lib case skips as not ported.

## Consequences

- `PassingUpstreamCases.txt` is unchanged: lib cases were skipped and still are, with the new
  reason. Each port task adds its program to `LibTestPrograms.Ported` and its cases to the list.
- The runner's tests drive a lib case with a sample `ILibTest` and a stub engine, without a network.
