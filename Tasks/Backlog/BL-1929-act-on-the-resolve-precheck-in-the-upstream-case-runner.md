---
id: BL-1929
title: Act on the %RESOLVE precheck in the upstream case runner
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-09
completed:
---
# BL-1929 — Act on the %RESOLVE precheck in the upstream case runner

## Goal

The upstream case runner acts on a `<client><precheck>` of the form `%RESOLVE [--ipv6|--ipv4] <name>`, so test1085 (and test241, once an `http-ipv6` stand-in exists) is measured instead of skipped.

## Context

Split from BL-1893, whose inventory of every precheck and postcheck in curl 8.21.0's tests/data (vendored in Curl.Conformance.UnitTests\UpstreamTestData) found this the only perl-free group the in-process harness can act on: `%RESOLVE` is upstream's `server/resolve` tool, which exits 0 when the name resolves (IPv6 with `--ipv6`) and prints a reason otherwise. Cases: test1085 (`%RESOLVE --ipv6 ::1`, no server, `--interface non-existing-host.haxx.se.`) and test241 (`%RESOLVE --ipv6 ip6-localhost`, needs the `http-ipv6` server, still skipped for that). test1083 wraps it in `%PERL` (BL-1894). Semantics per runtests.pl: a precheck that fails or prints anything skips the case with that text. In-process, decide the resolve result without DNS (an IP literal resolves; a name such as `ip6-localhost` is decided by a fixed table, platform-neutral), record the choice in Notes. Harness files: UpstreamCaseScreening.cs (allow `precheck` when its body is a `%RESOLVE` line; keep the "does not act on <client><precheck>" reason for any other body), UpstreamCaseRunner.cs, and the expander's variables (`%RESOLVE`, plus whatever test1085 still lacks: check `%HOST6IP`, `%NOLISTENPORT`). Read Curl.Conformance.UnitLibrary\CLAUDE.md first.

## Acceptance criteria

- [ ] Unit tests cover a `%RESOLVE` precheck that succeeds (the case runs) and one that fails (the case is skipped with the precheck's reason).
- [ ] test1085 runs through UpstreamCaseRunner and gets Passed or a real Curl difference; a screening test pins that it is no longer skipped for `<client><precheck>`.
- [ ] Curl.Conformance.UnitLibrary holds 100% line and branch coverage and complexity of at most 10 per method; tests are platform-neutral with no TestCategory=Integration.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` is green.
- [ ] Curl.Conformance.UnitLibrary\CLAUDE.md states what the runner does with a `%RESOLVE` precheck.

## Notes

## Log

- 2026-10-09: Created.
