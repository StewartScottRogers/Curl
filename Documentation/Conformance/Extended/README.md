# Extended protocol conformance

Curl's extended protocols (ADR-0999: what Curl adds beyond upstream curl) are measured against their
own specifications, not against curl (ADR-1188). Each one is tested by running a real curl command
line in process against a reference server written from the specification, and each test is traced
to the specification's requirements through a requirement matrix in this folder.

## Layout

- `Curl.Conformance.Extended.UnitLibrary/Harness/` holds the shared harness; its tests are in
  `Curl.Conformance.Extended.UnitTests/Harness/`.
- Each protocol's reference server goes in `Curl.Conformance.Extended.UnitLibrary/<Name>/`, one file
  per server, and its cases in `Curl.Conformance.Extended.UnitTests/<Name>/`, one file per case group.
- The curl-surface checks (options, `--help`, the MCP tools) go in
  `Curl.Conformance.Extended.UnitTests/CurlSurface/`.
- This folder holds one requirement matrix per protocol, `<Name>.md` (or `<Name>-<Part>.md` for a
  protocol split into parts), and this README, which the test project does not copy as a matrix.
- There is no registry or switch: a case names the reference server it runs against, MSTest finds the
  cases, and the matrix check finds the matrices by file name.

## A run

`ExtendedConformanceHarness.RunAsync(curl, routes, arguments, environment, timeProvider)` runs one
command line and returns a `CurlRunResult`:

- `curl` is the entry point, an `InProcessCurlRunner`: the tests pass `Curl.Console`'s
  `InProcessCurl.RunAsync`, which the library cannot reference.
- `routes` are the reference servers, each a `ReferenceRoute.Tcp`, `ReferenceRoute.Tls` (with a
  certificate; `ReferenceCertificate.CreateSelfSigned` makes one curl accepts under `-k`) or
  `ReferenceRoute.Udp` on a port. Every host name resolves to 127.0.0.1. No socket is opened.
- `environment` is the whole environment curl sees; none when not given.
- `timeProvider` is the clock every timer of the run runs on, `-m`, `-y` and `--connect-timeout`
  among them; `TimeProvider.System` when not given. A test drives a timeout with a fake clock and no
  real wait: `ExtendedCurlRunTimeoutTests` runs `-m 100` against a server that never answers and gets
  exit 28 once it advances its clock.
- A case whose command line runs an extension handler that starts a program (an `mcp+stdio://`
  server, a `curl-proto-<scheme>` helper) passes a stand-in `IExtensionProcessStarter` through the
  overload `RunAsync(curl, routes, arguments, processStarter, environment, timeProvider)`, whose
  `curl` is an `InProcessCurlProcessRunner` - `Curl.Console`'s `InProcessCurl.RunAsync` overload that
  takes the starter. Every program the handlers run is then started by the stand-in and looked for in
  the environment its `ReadEnvironmentVariable` reads, so no real process starts (ADR-1200, BL-4577):
  `ExtendedConformanceHarnessProcessTests` runs `mcp+stdio://localhost/server?a` and sees the stand-in
  asked to start `server` with `a`.
- The result holds the exit code, the standard output and standard error bytes and, per connection
  or datagram channel in the order curl opened them, a `WireExchange` with the bytes each side sent
  (on a TLS route, the plaintext behind the handshake).
- A server that fails makes `RunAsync` throw its exception; one still waiting when curl ends is
  cancelled.

## Reference servers

A reference server implements `IReferenceServer` (TCP and TLS) or `IDatagramReferenceServer` (UDP).
`ScriptedReferenceServer` replays a fixed script of `ScriptStep.ReceiveUntil` and `ScriptStep.Send`
steps, and `ScriptedDatagramServer` fixed datagrams, for cases that need only canned server bytes.
A protocol's own reference server implements the wire format from the specification on its own and
never reuses the client library's codec, so a client bug is not mirrored by the server.

## Requirement IDs

An ID is `<Name>:<source>-<locator>`: `<Name>` is the protocol name (the matrix's file name),
`<source>` the specification and `<locator>` its section and a running number, using the
specification's own normative IDs where it has them (`Mqtt5:MQTT-3.1.2-3`, `Coap:RFC7252-5.4.1-1`,
`Stomp:STOMP12-2.3-1`). A test method carries `[SpecRequirement("<id>")]`, repeated for each
requirement it covers.

## Matrix format

`<Name>.md` holds one Markdown table:

| ID | Level | Requirement | Covered by |
|----|-------|-------------|------------|

- `Level` is MUST, MUST NOT, SHOULD, SHOULD NOT (or REQUIRED, SHALL and the like, as the
  specification words it).
- `Covered by` is the test method's `Class.Method`, or `n/a: <one-line reason it does not apply to a
  client>`.

`RequirementMatrixTests.EveryMatrixAgreesWithTheTestsThatCarrySpecRequirement` fails when a row's test does not exist or does not carry
that row's ID, when a `[SpecRequirement]` ID is in no row of its protocol's matrix, when an ID is a
row twice, when an `n/a` row gives no reason, or when a matrix has no rows. The matrices are copied
into the test output, so the check reads no repository file at run time.

## Known failures

`Curl.Conformance.Extended.UnitTests/KnownFailures.txt` lists cases known to fail, one
`<requirement id> BL-####` per line, `#` comments, in any order, naming the filed task that fixes
the case. A case runs through `KnownFailures.RunCaseAsync`: listed, it passes while it still fails,
and fails once it passes, saying "remove its KnownFailures.txt line". A listed ID that no test
carries fails the list's own check. The file merges by union (`.gitattributes`, `Tasks/SharedListFiles.txt`),
so add or remove only whole lines.

## Extension isolation

`VerifyExtensionIsolation` in `Directory.Build.props` lets only the hosts and the extensions
reference an extension. `Curl.Conformance.Extended.UnitTests` references the hosts `Curl.Console`
(which reaches every extension through `Curl.Extension.Composition`) and `Curl.Mcp`;
`Curl.Conformance.Extended.UnitLibrary` references `Curl.Protocol.Abstractions`, `Curl.Networking`,
`Curl.Conformance.UnitLibrary` and the shared layers only, never an extension.
