# ADR-0117 — The connector owns `--connect-timeout` and the runner owns `-m`, for every scheme

- **Status:** Accepted
- **Date:** 2026-09-28

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-498.

## Context

ADR-0008 put `ConnectTimeout` and `MaxTime` on `ITransferContext` and left each handler to
read them. Only two do: HTTP (ADR-0040, `HttpTransferDeadline`) and TFTP
(`TftpTimeLimits`, which derives its re-send schedule from both). The conformance audit of
2026-09-28 (row 12, Blocker) found that `ftp://`, `ftps://`, `dict://`, `gopher://`,
`telnet://` and `mqtt://` never time out, and that `Curl.Networking.UnitLibrary`'s
`TcpConnector` ignores `--connect-timeout`, so any scheme but HTTP waits on a dead address
for as long as the operating system lets it. SMTP, POP3, IMAP, SSH, WebSocket, LDAP, RTSP
and SMB handlers are planned and wait on this decision.

What curl 8.21.0 does (measured for HTTP in BL-174 and BL-299, see ADR-0040):

- Both limits are checked by libcurl's multi loop, not by the protocol code, so every
  scheme ends the same way: exit 28 (`CURLE_OPERATION_TIMEDOUT`).
- The message depends on the phase. While connecting (resolve, TCP connect, proxy tunnel
  and TLS handshake) it is the connect message, `Connection timed out after N milliseconds`,
  whether `--connect-timeout` or `-m` ran out first; N counts from the start of the current
  request. After the connect it is `Operation timed out after N milliseconds with M bytes
  received`, or `with M out of T bytes received` when the download size is known; N counts
  from the start of the operation (the first request of a `-L` chain), M is
  `%{size_download}` and T the expected download size.
- `--connect-timeout` not given, or given as 0, is curl's `DEFAULT_CONNECT_TIMEOUT` of
  300 seconds. `-m 0`, like no `-m`, is no limit.
- `--retry` starts each attempt with a fresh `-m`.

Two precedents shape the choice. ADR-0106 put `-Y`/`-y` in the runner: `LowSpeedWatchdog`
wraps the attempt's output and progress sink, and its token becomes the context's
`CancellationToken`, so every scheme is watched without a handler change. ADR-0113 and
ADR-0114 made `TcpConnector` a per-command-line object built by `CurlComposition` from the
parsed options, as it is for `--resolve` and `--connect-to`.

## Decision

1. **The connect timeout is enforced by the connector.** `TcpConnector` takes the
   `--connect-timeout` value in its constructor, as it takes `--resolve` and
   `--connect-to`; `CurlComposition.CreateTcpConnector` passes `options.ConnectTimeout`.
   Each `ConnectAsync` runs its resolve, every dial, any proxy tunnel and any TLS handshake
   under one limit on the connector's injected `TimeProvider`: the given timeout, or 300
   seconds when none or 0 was given. When it passes, `ConnectAsync` returns
   `ConnectResult.Failed(CurlExitCode.OperationTimedOut, <curl's connect message>)`, N
   counted from the start of that `ConnectAsync`, the message text as BL-510 measures it
   (per platform where it differs). A cancellation that arrives with the limit already
   passed on the clock is reported as that failure, so a simultaneous `-m` cannot turn it
   into an exception. `ConnectTarget` and `IConnector` do not change: no handler passes a
   timeout, and the connector learns nothing about the transfer context.
2. **The whole-transfer deadline is enforced by the runner.** A new `MaxTimeWatchdog` in
   `Curl.Core`, beside `LowSpeedWatchdog`, is started by `CurlCommandRunner` for every
   attempt that has a positive `-m`. The runner takes the attempt's start timestamp on its
   `TimeProvider`, sets it as the context's `OperationStarted` (so `RedirectFollower`,
   `HttpTransferDeadline`, `TftpTimeLimits` and the watchdog count from the same instant),
   and the watchdog cancels at `OperationStarted + MaxTime`. The context's
   `CancellationToken` is the watchdog's token linked with the low-speed watchdog's. An
   attempt that throws `OperationCanceledException` after the watchdog fired ends with
   exit 28 and curl's message, which `--retry` counts as transient like any timeout; any
   other cancellation still propagates.
3. **The byte counts reach the message through `ITransferProgress`.** The watchdog wraps the
   attempt's progress sink, as `LowSpeedWatchdog.WatchProgress` does, and keeps the last
   `ReportDownloaded(bytesSoFar, expectedTotal)` and whether `ReportTransferStarted` was
   called. After `ReportTransferStarted` the failure is `Operation timed out after N
   milliseconds with M bytes received`, or `with M out of T bytes received` when the last
   report carried an expected total, N counted from `OperationStarted`. Before it, the
   attempt was still connecting, and the failure is the connect message with N counted from
   the attempt's start.
4. **HTTP and TFTP keep their own enforcement.** Both already pin curl's measured output,
   and each knows more than the runner: HTTP counts a redirect hop's connect N from the hop,
   and TFTP derives its re-send schedule from both limits. Their limits run from the same
   `OperationStarted` on the same clock as the watchdog, so both fire at the same instant.
   So that the handler's own result wins that tie, `HttpTransferDeadline.EndedByLimit` and
   `TftpTimeLimits.ReceiveBeforeAsync` treat a cancellation as their limit when their limit
   has passed on the clock, not only when the transfer's token is uncancelled. Either way
   the exit code and the operation message are the same.

## Consequences

The work, by task:

- **BL-510 (TcpConnector).** Measure `--connect-timeout 1` against a non-routable address
  and against a TLS listener that never answers; give `TcpConnector` the constructor
  parameter and the one connect limit of Decision 1 (resolve, dial, tunnel and handshake);
  pass `options.ConnectTimeout` from `CurlComposition.CreateTcpConnector`. `ConnectTarget`
  and `Curl.Protocol.Abstractions` stay unchanged, so BL-510 can drop them from its
  `touches`.
- **BL-511 (dict, gopher, telnet, mqtt).** Add `MaxTimeWatchdog` to `Curl.Core` and start it
  in `CurlCommandRunner` and `TransferContextFactory` beside the low-speed watchdog
  (Decisions 2 and 3); make the tie-break change of Decision 4 in `HttpTransferDeadline` and
  `TftpTimeLimits`. In the four handlers, report `ReportTransferStarted` once connected and
  `ReportDownloaded` for every body byte written, and let `OperationCanceledException`
  escape; nothing else. Its `touches` gain `Curl.Core.UnitLibrary`, `Curl.Core.UnitTests`,
  `Curl.Protocol.Http.UnitLibrary`, `Curl.Protocol.Http.UnitTests`,
  `Curl.Protocol.Tftp.UnitLibrary` and `Curl.Protocol.Tftp.UnitTests`.
- **BL-512 (ftp, ftps).** The control and data connections get the connect limit from the
  connector (BL-510) and `-m` from the runner (BL-511), so it depends on BL-511 as well.
  FTP already reports progress; what is left is to check that every wait lets the runner's
  `OperationCanceledException` escape, and to pin the measured messages for each phase. The
  active-mode accept wait keeps its own `AcceptTimeout` (exit 12, curl's
  `--ftp-port` accept timeout) and is bounded by `-m` through the context's token.

The contract a new protocol handler follows:

- Get every TCP connection from `IConnector`; the connect timeout then comes free.
- Pass `ITransferContext.CancellationToken` to every read, write, connect and wait, and let
  `OperationCanceledException` escape; never turn it into a result.
- Call `ITransferProgress.ReportTransferStarted` once the connection is up, and
  `ReportDownloaded` with the running total of body bytes written, with the expected size
  when it is known.
- Read `MaxTime` or `ConnectTimeout` only to derive something the protocol itself needs
  from them, as TFTP does.

Good:

- Every scheme, present and future, times out with no handler code for it; tests pin the
  behaviour once, on `FakeTimeProvider`, in `Curl.Core` and `Curl.Networking`.
- `IConnector`, `ConnectTarget` and `ITransferContext` do not change.

Costs and caveats:

- A handler that reports no progress prints `with 0 bytes received`; the contract above is
  what prevents it.
- A redirect hop that runs out of `-m` while connecting prints N from the attempt's start
  in the runner's message; only HTTP, which keeps its own enforcement, prints it from the
  hop. Only HTTP answers with a redirect, so the gap is a chain whose last hop is another
  scheme that runs out of `-m` while connecting.
- `-m` and a handler's own limit can fire at the same instant; Decision 4 is what keeps the
  handler's result deterministic. A handler that adds its own limit later must follow it.
- `IDatagramConnector` is not covered by Decision 1; TFTP, its only user, enforces the
  connect phase itself.

## Alternatives considered

- **Per handler, as HTTP does.** Rejected: every present and future handler would repeat
  `HttpTransferDeadline`, and the audit shows what happens when one forgets: the transfer
  never ends. curl itself enforces both limits outside the protocol code.
- **A shared runner deadline for both limits.** Rejected for the connect timeout: the runner
  cannot see when a connect starts, so it cannot restart the limit for FTP's data
  connection or for each redirect hop, while the connector sees exactly the span curl
  counts. Kept for `-m`, which spans the whole attempt.
- **A connector-level deadline for both limits.** Rejected for `-m`: the connector sees only
  connects, and a transfer that stalls after connecting would still never end.
- **Carry the timeout on `ConnectTarget`.** Rejected: every handler would have to copy it
  from the context, which is the per-handler failure again, and it changes the shared
  Abstractions contract, which serialises every protocol task behind it.
- **Retire HTTP's and TFTP's own enforcement.** Rejected for now: their measured output
  (the per-hop connect N, TFTP's re-send schedule) would be lost or have to move into the
  runner, for no behaviour gained.
