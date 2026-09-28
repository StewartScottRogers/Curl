# ADR-0119 — The composition records each transfer's first connection for the end-point variables

- **Status:** Accepted
- **Date:** 2026-09-28

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-515.

## Context

`%{local_ip}`, `%{local_port}`, `%{remote_ip}` and `%{remote_port}` are read by
`Curl.Output`'s `TransferWriteOutVariables` from `TransferReport.LocalEndPoint` and
`RemoteEndPoint`. Only the HTTP handler filled them, so every other networked scheme
printed `-1` ports and empty addresses where curl 8.21.0 prints the connection's. Eight
more protocol libraries (SMTP, POP3, IMAP, SSH, WebSocket, LDAP, RTSP, SMB) are about to be
written; each would have had to copy the end points too, and each could forget.

curl 8.21.0 (the Git for Windows mingw build), measured with `Record-CurlExchange.ps1`
(BL-515 Notes), with `-w '%{local_ip} %{local_port} %{remote_ip} %{remote_port}\n'`:

| Case | stdout after the body |
| --- | --- |
| `ftp://127.0.0.1:47515/f.txt`, passive | `127.0.0.1 64513 127.0.0.1 47515` - the control connection, not the data connection |
| `dict://127.0.0.1:47516/d:word` | `127.0.0.1 64515 127.0.0.1 47516` |
| `tftp://127.0.0.1:47519/f`, served from port 62775 | ` 0 127.0.0.1 47519` - the URL's server, no local end, local port `0` |
| `tftp://127.0.0.1:47517/f`, closed port, exit 7 | ` 0 127.0.0.1 47517` |
| `ftp://127.0.0.1:47518/f`, closed port, exit 7 | ` -1  -1` |

## Decision

The end points are recorded where connections are made, not in each handler.

1. `CurlComposition.CreateProtocolHandlers` builds one `ConnectionEndPointRecorder` and gives
   every handler an `EndPointRecordingConnector` over the TCP connector and an
   `EndPointRecordingDatagramConnector` over the datagram connector, both recording into it.
   A successful connect records `ConnectResult.LocalEndPoint` and the connection's
   `RemoteEndPoint`; an opened datagram channel records its `ServerEndPoint` and no local end.
   Only the first connection of a transfer is kept, which is FTP's control connection.
2. Every registered handler is wrapped in an `EndPointReportingProtocolHandler`, which clears
   the recorder, runs the handler, and puts the recorded end points on the report when the
   handler reported neither itself. A handler that does report them - HTTP, which knows the
   proxy and each redirect hop - keeps its own. A result with no report gets one holding the
   end points and `BytesTransferred` as its `DownloadSize`, which is what
   `%{size_download}` printed for it before.
3. `%{local_port}` is `0` when the report has a remote end point but no local one (a
   connection with no local end to report, as curl's unconnected TFTP socket) and `-1` when
   it has neither (no connection).

A new protocol library gets the four variables by connecting through the connectors it is
given; it writes nothing for them.

## Consequences

- Every scheme, present and future, prints the four variables with no handler code.
- Wrapping each connect and each transfer costs one allocation-free check and a field store.
- The recorder is shared state: it is correct because a run performs its transfers one at a
  time. Running transfers in parallel (`--parallel`) will need a recorder per transfer, for
  example through the transfer context.
- The composition tests look through the wrappers to check which connector each handler holds.

## Alternatives considered

- **A report field every handler sets from its `ConnectResult`.** Eight new handlers and six
  old ones would each copy the same two values, and a forgotten copy fails silently - the
  defect this task exists to fix.
- **Endpoints on `TransferResult` set by `Curl.Core`'s dispatcher.** The dispatcher never sees
  a connection; it would still need the handlers to pass the end points up.
- **Recording the last connection instead of the first.** FTP's data connection comes last,
  and curl reports the control connection.
