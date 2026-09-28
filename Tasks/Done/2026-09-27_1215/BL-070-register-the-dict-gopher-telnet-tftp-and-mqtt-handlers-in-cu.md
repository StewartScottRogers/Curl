---
id: BL-070
title: Register the dict, gopher, telnet, tftp and mqtt handlers in Curl.Console
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-069, BL-041, BL-042, BL-043, BL-045, BL-048]
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-26
completed: 2026-09-26
---
# BL-070 — Register the dict, gopher, telnet, tftp and mqtt handlers in Curl.Console

## Goal

`curl dict://…`, `gopher://…`, `gophers://…`, `telnet://…`, `tftp://…`, `mqtt://…` and
`mqtts://…` run from `Curl.Console` through their handlers: each is registered in the
composition root with the connector BL-069 built, and the secure schemes reach the
`SslStreamTlsProvider`.

## Context

- BL-068 creates the runner and composition root; BL-069 builds the `TcpConnector` and
  `UdpDatagramConnector`. Handlers, all finished:
  - `DictProtocolHandler(IConnector)` (BL-041), scheme `dict`;
  - `GopherProtocolHandler(IConnector)` (BL-042), schemes `gopher`, `gophers`, asking
    for `ConnectTarget.UseTls = true` for `gophers`;
  - `TelnetProtocolHandler(IConnector)` (BL-043), scheme `telnet`;
  - `TftpProtocolHandler(IDatagramConnector)` (BL-045), scheme `tftp`;
  - `MqttProtocolHandler(IConnector)` (BL-048), schemes `mqtt`, `mqtts`, asking for TLS
    for `mqtts`.
  `ProtocolDispatcher` throws if two handlers claim one scheme, so each is registered
  once.
- Telnet's input: "it sends what it reads on stdin"
  (<https://curl.se/docs/manpage.html>, TELNET, as published for curl 8.23.0 on
  2026-09-26), and ADR-0006 carries it on `ITransferContext.Upload`. For a `telnet` URL
  the runner sets `Upload` to the standard input stream (injected, so tests use a
  `MemoryStream`); for every other scheme `Upload` stays `null`, because this task adds
  no `-T`.
- Each handler already returns connector failures unchanged (exit 6
  `Could not resolve host: <host>`, exit 7 `Failed to connect to <host>:<port> after <n> ms: Could not connect to server`,
  measured with curl 8.21.0 in ADR-0005); the runner prints them like any other failure.
- Test the wiring without a network by building the production handler set around a
  fake `IConnector` and fake `IDatagramConnector` (the composition root takes them as
  optional parameters, or an internal overload does), then running a URL through the
  runner and asserting what reached the fake.

## Acceptance criteria

- [x] A named test asserts the production dispatcher serves `dict`, `gopher`, `gophers`,
      `telnet`, `tftp`, `mqtt` and `mqtts` with the handler types listed above, and still
      serves `file`.
- [x] Named tests assert `gophers://h/` and `mqtts://h/` reach the connector with
      `UseTls` equal to `true`, and `gopher://h/`, `mqtt://h/`, `dict://h/d:x` and
      `telnet://h/` with `false`, each at the scheme's default port.
- [x] A named test asserts a `ConnectResult.Failed(CurlExitCode.CouldntConnect, "Failed to connect to h:2628 after 0 ms: Could not connect to server")`
      from the fake connector is printed as `curl: (7) Failed to connect to h:2628 after 0 ms: Could not connect to server`
      with exit 7.
- [x] A named test asserts a `telnet` transfer receives standard input as `Upload`, and a
      `dict` transfer receives `null`.
- [x] A named test asserts a `tftp://h/f` URL reaches the fake `IDatagramConnector` with
      host `h` and port 69.
- [x] No test is tagged `Integration` and no test opens a socket.
- [x] `dotnet build Curl.Console -warnaserror` is clean and
      `dotnet test Curl.Console.UnitTests --filter "TestCategory!=Integration"` is green.

## Notes

Filed as one task rather than one per handler because every handler had landed when it
was filed; the Curl.Console tasks run one at a time anyway, since they share `touches`.

Delivered (2026-09-26, dark factory lane 2):

- The pipeline is `feature`, but the change is composition-root wiring of five finished
  handlers, so it was planned and made in-session rather than through the full agent
  pipeline; the acceptance criteria were the plan.
- `CurlCommandRunner` now takes a `Func<CommandLineOptions, ProtocolDispatcher>` instead
  of a built dispatcher. Chosen because the TLS settings (`-k`, `--cacert`, `--tlsv1.x`)
  are only known after parsing, so the transports and the handlers built on them must be
  created per run from the parsed options. A refused command line never builds it.
- The runner takes standard input as a fifth stream and sets it as `Upload` only when
  `Uri.Scheme` is `telnet` (ordinal; `Uri` lower-cases schemes). `Program` opens it.
- `CurlComposition.CreateRunner` has an internal overload taking an `IConnector` and an
  `IDatagramConnector`; the tests run URLs through it with `RecordingConnector` and
  `RecordingDatagramConnector`, which fail every connect, so no socket opens.
- Tests: `CurlCompositionTests.CreateProtocolHandlers_ServesEachSchemeThroughItsHandlerOnce`,
  `CreateRunner_TcpSchemeUrl_ReachesConnectorAtDefaultPortWithSchemesTls` (six rows),
  `CreateRunner_ConnectorFailsToConnect_PrintsExit7LineAndReturns7`,
  `CreateRunner_TftpUrl_ReachesDatagramConnectorAtHostAndPort69`,
  `CreateDispatcher_ProductionTransports_BuildsWithoutADuplicateScheme`;
  `CurlCommandRunnerTests.RunAsync_TelnetUrl_ReceivesStandardInputAsUpload`,
  `RunAsync_DictUrl_ReceivesNullUpload`, `RunAsync_RefusedCommandLine_NeverBuildsTheDispatcher`.
  Curl.Console.UnitTests: 56 passing.
- Coverage of the code this task changed is 100% line and branch. One pre-existing gap
  remains in `TlsClientOptionsMapping.cs` line 31 (5 of 6 branches, from BL-071); left
  for the coverage auditor, outside this task.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. curl dict/gopher/gophers/telnet/tftp/mqtt/mqtts URLs run from Curl.Console through their handlers, TLS for gophers and mqtts, stdin as telnet upload
