# ADR-0013 — curl's upstream test cases run as data-driven MSTest cases, in process

- **Status:** Accepted
- **Date:** 2026-09-26
- **Decided by:** Stewart chose the approach (port the test cases to MSTest, no Perl);
  the harness design below was decided by Claude under Stewart's delegation.

## Context

curl ships 2,126 test cases in `tests/data` (counted on `master`; see the scope table in
`Documentation/Product/Product-Overview.md`). The product overview names them the
oracle for the drop-in claim: a stated pass rate against them, rising per release, is
the headline number. Open question 3 asked how they are driven from .NET.

Upstream drives them with `runtests.pl`: Perl starts real test servers (`sws` for HTTP,
`ftpserver.pl` for FTP, IMAP, POP3 and SMTP, `tftpd`, and others) on loopback ports,
runs the `curl` binary as a process, and compares what the servers received and what
`curl` wrote against each test file's `<verify>` section.

Each test file is a tagged text format, not well-formed XML: sections such as
`<reply><data>`, `<client><command>`, `<client><file>`, `<verify><protocol>`,
`<verify><stdout>`, `<verify><errorcode>` and `<verify><strip>`, attributes such as
`nonewline`, `crlf`, `base64` and `hex`, variables such as `%HOSTIP`, `%HTTPPORT`,
`%TESTNUMBER` and `%LOGDIR`, and `%if` / `%else` / `%endif` blocks keyed on features.

Two constraints bear on the choice. The fast suite
(`dotnet test --filter "TestCategory!=Integration"`) must pass with networking
disabled. The solution is base class library only, with MSTest the one package.

## Decision

1. **Ported to MSTest.** The test files are parsed and run by C# in the solution. There
   is no Perl, no `runtests.pl` and no dependency on upstream's test servers.
2. **Two projects.** `Curl.Conformance.UnitLibrary` holds the harness: the test-file
   parser, variable substitution and `%if` evaluation, the in-memory test servers, and
   the comparison of results against `<verify>`. It is held to the same quality gates as
   every other `.UnitLibrary`. `Curl.Conformance.UnitTests` holds the harness's own
   tests and the data-driven MSTest method that runs the upstream cases.
3. **Vendored, pinned test data.** The `tests/data/test*` files are copied into
   `Curl.Conformance.UnitTests/UpstreamTestData/` from one curl release tag (the release
   the solution matches, curl 8.21.0 at the time of writing), with curl's `COPYING`
   notice beside them. A script in that folder refreshes them to a named tag. Tests never
   download anything.
4. **In process, through the connector seam.** Each case runs through
   `CurlComposition.CreateRunner` with an `IConnector` and `IDatagramConnector` that
   route every connection to an in-memory test server emulating upstream's server for
   that protocol, driven by the case's `<reply>` and `<servercmd>`. The server records
   the bytes it received, which are compared with `<verify><protocol>` after `<strip>`
   and `<strippart>`. Standard output, standard error and the exit code are captured
   from the runner's streams and compared with `<verify><stdout>`, `<stderr>` and
   `<errorcode>`. `%LOGDIR` is a fresh temporary directory per case, substituted as an
   absolute path so cases can run in parallel. `Curl.Console` makes its internals
   visible to `Curl.Conformance.UnitTests` for this.
5. **A ratchet, not a red suite.** One data-driven test method (`[DynamicData]`, one row
   per vendored case) runs every case in `TestCategory("Conformance")`, which is part of
   the fast suite. A committed list of passing case numbers decides the outcome: a case
   on the list must pass, so a regression fails the build; a case off the list that fails
   is reported `Inconclusive` with the first difference; a case off the list that passes
   is reported `Inconclusive` saying it can be added to the list. The pass rate is the
   length of the list over the number of runnable cases.
6. **Out of scope, reported as skipped.** Cases that exercise libcurl rather than the
   `curl` tool (`<tool>` libtest and unittest cases), and cases whose `<features>`,
   `<server>` or keywords name something the harness does not emulate yet, are reported
   `Inconclusive` with the reason and are not counted as runnable until they are.
7. **Server emulations arrive per protocol.** The HTTP emulation of `sws` comes first,
   because most cases are HTTP. FTP, IMAP, POP3, SMTP, TFTP and the rest are filed as
   their own tasks once the runner exists.

## Consequences

- The conformance suite runs on every `dotnet test`, on every platform, with no Perl,
  no sockets and no network, and a passing case can never silently regress.
- The pass rate is a number anyone can recompute from the repository.
- `conformance-auditor` can point at a case number and its first difference instead of
  hand-running real curl for every behaviour the upstream suite already covers.
- The in-memory servers are an emulation. Where one differs from upstream's real server,
  a case can pass here and fail upstream, or the reverse. Differential testing against
  real `curl` (success criterion 2) stays the check on the emulations.
- Behaviour below the connector seam (DNS, socket options, TLS handshakes, proxies that
  change the connect target) is not exercised by these cases. Those cases are skipped
  with a reason until a seam covers them.
- Vendoring puts about two thousand small files in the repository and makes refreshing
  to a new curl release a deliberate commit, which is the intent: the pass rate is
  always stated against a named curl release.
- The harness is real code with its own tests, and parsing a loosely specified format
  will need follow-up work as new constructs turn up.

## Alternatives considered

- **Run upstream's `runtests.pl` against the published binary.** Most faithful, since it
  uses upstream's own servers and comparison. Lost because it needs Perl and a build of
  upstream's test servers on every machine, needs loopback networking, cannot run in the
  fast suite, and Stewart chose porting.
- **Port the cases, but run the published binary as a process against loopback
  servers.** Closer to how a script sees Curl, but it needs real sockets and a publish
  step before testing, and it is slow. Differential testing already covers the process
  boundary.
- **Fetch the test data at test time.** Keeps the repository small but puts the network
  in the test run and lets the pass rate drift with upstream `master`.
- **Hand-translate each case into its own C# test.** Readable, but two thousand
  hand-written tests drift from upstream and cannot be refreshed to a new release.
- **Let failing cases fail the suite.** Honest, but the suite would be red for years and
  a red suite hides regressions. The ratchet keeps both the rate and the guard.
