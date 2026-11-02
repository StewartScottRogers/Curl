---
id: BL-4532
title: Conformance: gRPC message framing, timeout and grpcs cases - split DATA, truncated, compressed flag, grpc-timeout from -m, ALPN h2 (Grpc part 1c)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4500, BL-4505, BL-4506, BL-4507, BL-4502, BL-4503, BL-4530]
touches: [Curl.Conformance.Extended.UnitTests/Grpc/GrpcFramingCases.cs, Curl.Conformance.Extended.UnitTests/Grpc/GrpcTimeoutCases.cs, Curl.Conformance.Extended.UnitTests/Grpc/GrpcTlsCases.cs, Curl.Conformance.Extended.UnitTests/KnownFailures.txt, Documentation/Conformance/Extended/Grpc.md]
requirement: none
created: 2026-11-02
completed: 2026-11-02
---
# BL-4532 — Conformance: gRPC message framing, timeout and grpcs cases - split DATA, truncated, compressed flag, grpc-timeout from -m, ALPN h2 (Grpc part 1c)

## Goal

Conformance cases pin curl's gRPC message framing, its `grpc-timeout` from `-m` and `grpcs://` with ALPN `h2`.
## Context

Split from BL-4477 for size (one run could not build the server, the cases and the matrix). Read BL-4477's
Context (in `Tasks/Deferred/`): specification grpc/grpc `doc/PROTOCOL-HTTP2.md` and `doc/statuscodes.md`; library
under test `Curl.Extended.Protocol.Grpc.UnitLibrary` (ADR-1133, ADR-1147); follow
`Documentation/Conformance/Extended/README.md` and ADR-1186 (`[SpecRequirement("Grpc:<source>-<locator>")]`, matrix
format, `KnownFailures.txt` rule). Measure against the specification, never against upstream curl. A defect in the
extension library is listed in `KnownFailures.txt` with a filed High task `Fix Grpc conformance: <requirement>`
(pipeline feature; touches the library, its `.UnitTests` and `KnownFailures.txt`; criteria: the case passes, a
focused unit test in the library's `.UnitTests` pins the fix (ADR-0482), the `KnownFailures.txt` line is removed).
Cases run curl in process through the harness, e.g. `curl --grpc-proto svc.proto -d '{"name":"x"}'
grpc://host/pkg.Service/Method`, the `.proto` file in a temporary directory the test creates and deletes.
Uses the reference server BL-4530 adds; adds no file under `Curl.Conformance.Extended.UnitLibrary/Grpc/`. The TLS
route is the harness's (`ReferenceRoute` TLS, `ReferenceCertificate.CreateSelfSigned`, curl under `-k`); the timeout
runs on the harness's fake clock (BL-4503).

## Acceptance criteria

- [x] Cases in `Curl.Conformance.Extended.UnitTests/Grpc/` assert frames, stdout, stderr and exit code for a message
      split across DATA frames, a truncated message, a compressed-flag message the client cannot decode,
      `grpc-timeout` from `-m` (units and digit limit) and `grpcs://` negotiating ALPN `h2`, each carrying its
      `Grpc:` ID.
- [x] Every failing case is fixed here (if the fault is in the cases) or listed in `KnownFailures.txt` with the ID of
      its filed High `Fix Grpc conformance: ...` task; the known-failures check passes.
- [x] `dotnet build Curl.slnx -warnaserror` is clean and the fast tests are green; no package added; tests
      platform-neutral.
## Notes

- Touches widened to `Documentation/Conformance/Extended/Grpc.md`: the matrix check fails on a `[SpecRequirement]` ID in no
  row, so the six new IDs need rows there. No task in Doing on `origin/work/dark-factory` names that file.
- New IDs: `Grpc:Http2-Message-2` (split across DATA), `-Message-3` (truncated, exit 8), `-Message-4` (Compressed-Flag 1
  with no accepted encoding, exit 61), `-Timeout-1` (TimeoutValue of at most 8 digits plus unit, from `-m`),
  `-Timeout-2` (no `-m`, so no `grpc-timeout`), `-Tls-1` (`grpcs://` offers and negotiates ALPN `h2`). Exit codes and
  error text are Curl's own (an extension is measured against the specification, not against curl); stderr ends in
  `Environment.NewLine`, as Curl writes it on each platform.
- `-m` rows: 10 -> `10000000u`, 0.5 -> `500000u`, 0.001 -> `1000000n`, 2000000 -> `2000000S` (the 8-digit limit forces
  seconds). Values big enough to need `M` or `H` (from 100000000 s up) are past curl's own `-m` range (exit 2) where `long`
  is 32 bits, so they are left out to keep the case platform-neutral. The run uses a frozen clock whose timers never
  fire (the harness's `timeProvider`), so `-m 0.001` reads the header and never expires.
- The harness's TLS route offers no ALPN, so `GrpcTlsCases` serves TLS itself (`TlsServerStream`, ALPN `h2`) over a TCP
  route and checks curl's raw ClientHello for the ALPN extension. It found a defect: Curl's ClientHello has no ALPN at
  all and the call ends with exit 16, because `TcpConnector.ApplicationProtocolsFor` offers a target's protocols only
  for `PoolScheme` `https`, which the gRPC handler does not set. Listed in `KnownFailures.txt` under BL-4545 (High,
  filed). The deadline expiry itself (exit 28 on the fake clock) is not in the criteria and was not added.
- Checks: `Curl.Conformance.Extended.UnitTests` 254/254 (the known-failures and matrix checks included);
  `dotnet build Curl.slnx -warnaserror` clean; full fast run 115 test projects, 0 failed; `dotnet format` clean. Only
  test code and docs changed, so no Measure-CodeQuality run applies (no gated library changed). Tests use temp
  directories and in-memory transport only, so they do not depend on the platform.

## Log

- 2026-11-02: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: dff3d69e d3c3b130 3af8a194.
- 2026-11-02: Doing -> Done. gRPC framing, grpc-timeout and grpcs ALPN cases pin PROTOCOL-HTTP2.md; the ALPN defect is a known failure under BL-4545
