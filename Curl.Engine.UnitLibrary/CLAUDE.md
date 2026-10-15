# Curl.Engine.UnitLibrary

The managed transfer engine (ADR-0506): the code that runs a transfer, moving out of
`Curl.Console` so that `Curl.Console`, `Curl.Api` and `Libcurl` can all drive it. Namespace
`Curl.Engine`. Transfer code is moving in task by task (BL-2285 onwards).

It references `Curl.Cli.UnitLibrary` only temporarily; BL-2310 drops that reference.

Never reference `Curl.Console`, `Libcurl` or `Curl.Api`: the engine sits beneath all three.

Held to the four quality gates in the root `CLAUDE.md`. Its tests live in `Curl.Engine.UnitTests`.

## What has moved here so far (BL-2285)

`CurlComposition.CreateProtocolHandlers` (in `Curl.Console`) gives every handler an
`EndPointRecordingConnector` and an `EndPointRecordingDatagramConnector` sharing one
`ConnectionEndPointRecorder`, and wraps each handler in an `EndPointReportingProtocolHandler`, which
puts the end points of the transfer's first connection (FTP's control connection) on its report when
the handler reported neither, so `%{local_ip}`, `%{local_port}`, `%{remote_ip}` and `%{remote_port}`
work for every scheme without handler code; HTTP keeps its own (ADR-0119, BL-515). Each handler sits
behind a `LazyProtocolHandler` that names its schemes and builds the handler on its first transfer
(BL-1715, AF-0063); `EndPointReportingProtocolHandler.Handler` gives the built handler.

An `ftp` or `ftps` URL is claimed by `RoutingFtpProtocolHandler`, which hands an `ftp` one to the HTTP
handler when its proxy is `Http` or `Http10` and `-p` is not given, so it is forwarded to the proxy
as `GET ftp://host/path` with `Host: host:21` (ADR-0056, rule 3; BL-344); any other transfer goes to
`FtpProtocolHandler`.

The trace-event decorators `ClientReaderResetTraceEvents` (the `[READ] client_reset, clear readers`
line before a finished transfer's `left intact` or `shutting down connection` line and after each
followed redirect's `Issue another request to this URL` line; ADR-0357's BL-1159 amendment),
`ClientWriterTraceEvents`, `ConnectReplyHeadWritingEvents` and `ResponseWaitTimerTraceEvents` (the
response wait's `[TIMER]` lines after `Request completely sent off` when the multi is not traced;
ADR-0401, BL-1258) wrap an `ITransferEvents`. `LateBoundDiagnosticLog` is an `IDiagnosticLog` bound
once the runner has opened the run's log.

Four types the plan listed here stay in `Curl.Console` because each uses a tool-side type that has
not moved yet: `ConnectionIdRecordingTransferEvents` and `TlsResultRecordingTransferEvents` (both use
`RunningTransferState`), `MultiStateTraceEvents` (uses `TransferTimers`, BL-2288) and
`GatedDiagnosticLog` (uses `WriteGate`).
