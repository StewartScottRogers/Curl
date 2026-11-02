---
id: BL-4462
title: Build the extended-protocol conformance harness: in-process curl against spec-faithful in-memory reference servers
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.Extended.UnitLibrary, Curl.Conformance.Extended.UnitTests, Curl.slnx, .gitattributes, Tasks/SharedListFiles.txt, Documentation/Planning/Decisions/README.md, Documentation/Conformance/Extended/README.md, Curl.Console/Curl.Console.csproj, Curl.Mcp/Curl.Mcp.csproj, Curl.Mcp.UnitLibrary/Curl.Mcp.UnitLibrary.csproj]
model: opus
requirement: none
created: 2026-11-02
completed:
---
# BL-4462 — Build the extended-protocol conformance harness: in-process curl against spec-faithful in-memory reference servers

## Goal

The new projects `Curl.Conformance.Extended.UnitLibrary` and `Curl.Conformance.Extended.UnitTests` run the real curl
command line in process against in-memory reference servers for Curl's extended protocols, report the wire bytes,
stdout, stderr and exit code of each run, and give every later conformance task the requirement-ID, matrix and
known-failures machinery it needs, so the 20 per-protocol tasks that depend on this one can each work in their own
subfolder in parallel.

## Context

Stewart approved the plan on 2026-11-02: exhaustive conformance tests for the 20 extended protocols
(`Curl.Extended.Protocol.<Name>.UnitLibrary`: Amqp0, Amqp1, AzureStorage, Coap, DnsQuery, GoogleCloudStorage, Grpc,
Kafka, McpClient, Mqtt5, Nats, OciRegistry, Plugin, Redis, S3, SignalR, SocketIo, Stomp, WebTransport, Xmpp),
measured against each protocol's own specification, never against upstream curl (curl has none of them; root
`CLAUDE.md`, "Extensions": "It is tested against its specification, not against curl").

Where to start:

- Running curl in process: `Curl.Console/CurlCommandRunner.cs` (internal; its constructor takes
  `createTransferDispatch`, the streams, `timeProvider` and `readEnvironmentVariable`) and
  `Curl.Mcp/InProcessCurlCommandRunner.cs`. `Curl.Conformance.UnitTests` already references `Curl.Console` and is
  named in its `InternalsVisibleTo` list; follow it.
- Routing connections to an in-memory server: `Curl.Engine.UnitLibrary/EngineComposition.cs` builds the
  `ProtocolHandlerServices(Connector, DatagramConnector, TlsProvider, DnsResolver)`
  (`Curl.Protocol.Abstractions.UnitLibrary/ProtocolHandlerServices.cs`) every extension handler connects through.
  `Curl.Conformance.UnitTests/InMemoryServerTcpDialer.cs`, `InMemoryDuplexStream`, `LoopbackOnlyDnsResolver.cs` and
  the `HttpsServerConnector` (in-memory TLS) are the existing pattern; reuse `Curl.Conformance.UnitLibrary` rather
  than copying it. HTTP/2 and HTTP/3 reference servers exist in `Curl.Conformance.Http2Server.UnitLibrary` and
  `Curl.Conformance.Http3Server.UnitLibrary`; the SSH one in `Curl.Conformance.SshServer.UnitLibrary`.
- Extension isolation: `VerifyExtensionIsolation` in `Directory.Build.props` lets only the hosts (`Curl.Console`,
  `Curl.Api`, `Curl.Mcp` and their tests) and the extensions reference an extension, and checks direct
  `ProjectReference`s only. Decide here (do not change `Directory.Build.props`): `Curl.Conformance.Extended.UnitTests`
  references the host `Curl.Console` (which reaches every extension through `Curl.Extension.Composition`) and
  `Curl.Mcp`; `Curl.Conformance.Extended.UnitLibrary` references only `Curl.Protocol.Abstractions`,
  `Curl.Conformance.UnitLibrary`, the conformance servers above and the shared layers (`Curl.Http2`, `Curl.Quic`,
  `Curl.Tls`, `Curl.WebSocket`, `Curl.Protobuf`), never an extension. A reference server implements its protocol's
  wire format from the specification on its own and never reuses the client library's codec, so a client bug is not
  mirrored by the server.
- Add `Curl.Conformance.Extended.UnitTests` to the `InternalsVisibleTo` lists of `Curl.Console/Curl.Console.csproj`,
  `Curl.Mcp/Curl.Mcp.csproj` and `Curl.Mcp.UnitLibrary/Curl.Mcp.UnitLibrary.csproj`, so the curl-surface tasks can
  reach `McpUrlTool` and `McpVersionTool` (ADR-1181) without touching a csproj.
- Solution placement: one `<Folder Name="/Conformance/Extended/">` holding both projects in `Curl.slnx` (ADR-0731).

The contract the dependent tasks are written against (keep these names, or change every dependent task's text):

- Layout: shared harness code in `Curl.Conformance.Extended.UnitLibrary/Harness/` and its tests in
  `Curl.Conformance.Extended.UnitTests/Harness/`; each protocol's reference server in
  `Curl.Conformance.Extended.UnitLibrary/<Name>/` and its cases in `Curl.Conformance.Extended.UnitTests/<Name>/`;
  the curl-surface checks in `Curl.Conformance.Extended.UnitTests/CurlSurface/`.
- No central registry or switch: a case names the reference server it runs against, MSTest finds the cases, and the
  matrix check finds matrices by file name. One file per case group.
- A run: give the harness the reference server(s) for a host and port (TCP, TLS, UDP), the curl arguments, optional
  environment variables and an injected `TimeProvider`; get back the exit code, stdout and stderr bytes and, per
  connection or datagram channel, the bytes each side sent in order.
- A scripted reference server that replays a fixed byte script and records what the client sent, for cases that only
  need canned server bytes (the curl-surface tasks use it).
- Requirement IDs: `<Name>:<source>-<locator>`, `<Name>` being the library's protocol name, `<source>` the
  specification and `<locator>` its section and a running number, using the specification's own normative IDs where
  it has them (`Mqtt5:MQTT-3.1.2-3`, `Coap:RFC7252-5.4.1-1`, `Stomp:STOMP12-2.3-1`). A test method carries
  `[SpecRequirement("<id>")]` (repeatable).
- Matrices: `Documentation/Conformance/Extended/<Name>.md` (and, for a protocol split into parts,
  `<Name>-<Part>.md` beside it), one Markdown table whose rows are `| ID | Level | Requirement | Covered by |`, Level
  being MUST, MUST NOT, SHOULD, SHOULD NOT (or REQUIRED, SHALL and the like, as the spec words it), and Covered by
  either the test method's `Class.Method` or `n/a: <one-line reason it does not apply to a client>`. The matrices are
  copied into the test output as content so the check reads no repository file at run time.
- Matrix check (in `Harness/`): every row's test exists and carries that ID; every `[SpecRequirement]` ID appears in
  its protocol's matrix; no ID appears twice; a matrix with no rows fails.
- Known failures: `Curl.Conformance.Extended.UnitTests/KnownFailures.txt`, one `<requirement id> BL-####` per line,
  `#` comments, order-free, `merge=union` in `.gitattributes` and listed in `Tasks/SharedListFiles.txt`. A case whose
  ID is listed passes while it still fails and fails, saying "remove its KnownFailures.txt line", once it passes; a
  listed ID that no test carries fails the check. A case is listed only with the ID of the filed task that fixes it.
- `Documentation/Conformance/Extended/README.md` states all of the above for the per-protocol tasks.

Record the decisions in a new ADR (the next free number, slug `extended-protocols-conform-to-their-own-specifications`,
one line added to `Documentation/Planning/Decisions/README.md`), marked "Decided by Claude under Stewart's
delegation": extended protocols are measured against their own specifications, not against curl; how the harness
reaches the extensions within `VerifyExtensionIsolation`; why reference servers do not reuse client codecs; the ID,
matrix and known-failures rules.

## Acceptance criteria

- [ ] `Curl.Conformance.Extended.UnitLibrary` and `Curl.Conformance.Extended.UnitTests` exist, sit together in
      `/Conformance/Extended/` in `Curl.slnx`, and the UnitLibrary references no `Curl.Extension.*` or
      `Curl.Extended.Protocol.*` project; `Directory.Build.props` is unchanged.
- [ ] A test in `Curl.Conformance.Extended.UnitTests/Harness/` runs a real curl command line in process (for example
      `redis://` against the scripted server) and asserts its exit code, stdout, stderr and the client's wire bytes.
- [ ] Tests in `Harness/` cover a TCP, a TLS and a UDP route, an environment variable reaching the run (`CURL_EXTENSIONS=0`
      gives the unsupported-scheme exit code 1), and a `-m` timeout on an injected `TimeProvider` (exit 28), with no
      real socket, file outside a temporary directory, or process.
- [ ] `SpecRequirementAttribute`, the matrix check and the known-failures check exist and are tested both ways
      (a passing listed case fails with the "remove its KnownFailures.txt line" message; an unmapped matrix row fails).
- [ ] `Curl.Conformance.Extended.UnitTests/KnownFailures.txt` exists (comments only), is marked `merge=union` in
      `.gitattributes` and listed in `Tasks/SharedListFiles.txt`.
- [ ] `Documentation/Conformance/Extended/README.md` states the layout, ID scheme, matrix format and known-failures
      rule above.
- [ ] The new ADR exists with the decisions above and "Decided by Claude under Stewart's delegation", and its line is
      in `Documentation/Planning/Decisions/README.md`.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green;
      no test needs `TestCategory=Integration`.
- [ ] `Curl.Conformance.Extended.UnitLibrary` is at 100% line and branch coverage, complexity at most 10 and CRAP at
      most 30 per method (`Measure-CodeQuality.ps1`).
- [ ] No package added; every test passes on Windows, Linux and macOS (no drive letters or Windows-only text).

## Notes

## Log

- 2026-11-02: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: 94bce983 ff67eeb1 9db82948 e9fc1ee0 32b5ab0c 074d54e3 bcfa9865 d8e3fb0e cc077f22 7111045e.
- 2026-11-02: Doing -> Deferred. Split into BL-4500, BL-4501, BL-4502, BL-4503
- 2026-11-02: Deferred -> Backlog. Stewart 2026-11-02: complete all existing tasks before the third-party handoff.
- 2026-11-02: Backlog -> Doing. Criteria at claim: 94bce983 ff67eeb1 9db82948 e9fc1ee0 32b5ab0c 074d54e3 bcfa9865 d8e3fb0e cc077f22 7111045e.
