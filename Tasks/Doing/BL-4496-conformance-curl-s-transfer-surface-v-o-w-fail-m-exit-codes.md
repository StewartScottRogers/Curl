---
id: BL-4496
title: Conformance: curl's transfer surface (-v, -o, -w, --fail, -m, exit codes, bad URLs) for every extended protocol
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4500, BL-4505, BL-4506, BL-4507, BL-4502, BL-4503, BL-4463]
touches: [Curl.Conformance.Extended.UnitTests/CurlSurface/Transfer, Documentation/Conformance/Extended/CurlSurface-Transfer.md, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-02
completed:
---
# BL-4496 — Conformance: curl's transfer surface (-v, -o, -w, --fail, -m, exit codes, bad URLs) for every extended protocol

## Goal

Every extended protocol's scheme behaves under curl's generic transfer options as curl's own manual says any
transfer does - `-v`, `-o`, `-w`, `--fail`, `-m`, exit codes and malformed or refused URLs - checked through the real
curl command line, with every gap fixed or listed with its fix task.

## Context

The protocol tasks (BL-4464 to BL-4495, BL-4498 and BL-4499) measure each wire protocol; this task measures the curl surface around all
of them, the same checks for every scheme: `amqp`, `amqps`, `amqp1`, `amqp1s`, `az`, `abfs`, `abfss`, `coap`,
`coaps`, `dns`, `gs`, `grpc`, `grpcs`, `kafka`, `kafkas`, `mcp`, `mcps`, `mcp+stdio`, `mqtt5`, `mqtt5s`, `mqtt5+ws`,
`nats`, `natss`, `oci`, `redis`, `rediss`, `s3`, `signalr`, `signalrs`, `socketio`, `socketios`, `stomp`, `stomps`,
`webtransport`, `xmpp`, `xmpps`, and a `curl-proto-<scheme>` helper scheme (each library's `*Extension.cs` lists its
schemes; take the list from there, not from this text, if they differ).

Checks, per scheme: `-v` writes curl's connection lines (`* Connected to ...`, and for TLS schemes the TLS lines) on
stderr and nothing extra on stdout; `-o file` (in a temporary directory) and `-O` write the body; `-w` gives
`%{exitcode}`, `%{errormsg}`, `%{url}`, `%{url_effective}`, `%{scheme}`, `%{remote_ip}`, `%{remote_port}`,
`%{size_download}` and `%{time_total}` (with the injected clock); `--fail` and `--fail-with-body` against a server
error the library maps to exit 22 (each library's `CLAUDE.md` or ADRs say which); `-m` against a server that never
answers gives exit 28 at the deadline on the injected clock; connection refused gives exit 7; an unresolvable host
exit 6; a malformed URL (bad port, bad percent-encoding, a space) exit 3; and `-s`/`-S` change only what curl's manual
says they change. Expected values come from curl's manual page for curl 8.22.0 (curl.se/docs/manpage.html, "-w",
"--fail", "-m", "EXIT CODES") and from each extension's ADRs; where an extension's ADR deliberately differs, the
case pins the ADR's answer and names it.

Use the harness's scripted reference server (BL-4462) for the canned server bytes each scheme needs, one file per
scheme in `Curl.Conformance.Extended.UnitTests/CurlSurface/Transfer/` (for example `RedisTransferSurfaceTests.cs`),
or a shared data-driven test with one row file per scheme; never a central switch. The helper scheme and
`mcp+stdio` use the stand-in process starter BL-4463 adds. Requirement IDs: `CurlSurface:<option>-<n>`, in a matrix
`Documentation/Conformance/Extended/CurlSurface-Transfer.md`.

Part of the extended-protocol conformance plan Stewart approved on 2026-11-02. Follow
`Documentation/Conformance/Extended/README.md` and the ADR BL-4462 records. This task touches only its own files so
it runs beside the protocol tasks: a defect in an extension library or the engine is listed in `KnownFailures.txt`
with a filed High task `Fix <Name> conformance: <requirement>` (pipeline feature; touches that library, its
`.UnitTests` and `KnownFailures.txt`; criteria: the case passes, a focused unit test pins the fix (ADR-0482), the
line is removed).

## Acceptance criteria

- [ ] `Documentation/Conformance/Extended/CurlSurface-Transfer.md` lists each check above for each scheme with a
      `CurlSurface:` ID mapped to a test or to `n/a: <reason>` (for example `-O` for a URL with no file name), and the
      harness's matrix check passes.
- [ ] Cases in `Curl.Conformance.Extended.UnitTests/CurlSurface/Transfer/` run curl in process for every scheme and
      assert stdout, stderr and exit code for each check, with the expected values and their manual-page or ADR
      source named in the case.
- [ ] Every failing case is fixed in this task or listed in `Curl.Conformance.Extended.UnitTests/KnownFailures.txt`
      with the ID of its filed High `Fix <Name> conformance: ...` task; the known-failures check passes.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is
      green; no test needs `TestCategory=Integration`, opens a real socket or starts a real process.
- [ ] `Curl.Conformance.Extended.UnitLibrary` stays at 100% line and branch coverage, complexity at most 10 and CRAP
      at most 30 per method (`Measure-CodeQuality.ps1`).
- [ ] No package added; every test is platform-neutral (no drive letters, Windows-only paths or text).

## Notes

## Log

- 2026-11-02: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: fd060db8 ba635b0b fe617ef5 be674468 46eaa437 ef6c9026.
