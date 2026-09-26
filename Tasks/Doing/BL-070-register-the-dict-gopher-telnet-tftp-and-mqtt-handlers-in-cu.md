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
completed:
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

- [ ] A named test asserts the production dispatcher serves `dict`, `gopher`, `gophers`,
      `telnet`, `tftp`, `mqtt` and `mqtts` with the handler types listed above, and still
      serves `file`.
- [ ] Named tests assert `gophers://h/` and `mqtts://h/` reach the connector with
      `UseTls` equal to `true`, and `gopher://h/`, `mqtt://h/`, `dict://h/d:x` and
      `telnet://h/` with `false`, each at the scheme's default port.
- [ ] A named test asserts a `ConnectResult.Failed(CurlExitCode.CouldntConnect, "Failed to connect to h:2628 after 0 ms: Could not connect to server")`
      from the fake connector is printed as `curl: (7) Failed to connect to h:2628 after 0 ms: Could not connect to server`
      with exit 7.
- [ ] A named test asserts a `telnet` transfer receives standard input as `Upload`, and a
      `dict` transfer receives `null`.
- [ ] A named test asserts a `tftp://h/f` URL reaches the fake `IDatagramConnector` with
      host `h` and port 69.
- [ ] No test is tagged `Integration` and no test opens a socket.
- [ ] `dotnet build Curl.Console -warnaserror` is clean and
      `dotnet test Curl.Console.UnitTests --filter "TestCategory!=Integration"` is green.

## Notes

Filed as one task rather than one per handler because every handler had landed when it
was filed; the Curl.Console tasks run one at a time anyway, since they share `touches`.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
