# ADR-0040 — The HTTP handler enforces `-m` and `--connect-timeout` itself, on the transfer's clock

- **Status:** Accepted
- **Date:** 2026-09-26

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions").

## Context

ADR-0008 put `ConnectTimeout` and `MaxTime` on `ITransferContext` and left each handler to
read them. BL-174 makes the HTTP handler honour both. curl 8.21.0 (mingw, Schannel) was
measured against a loopback server and a non-answering address (BL-174 Notes):

- a stall after the connect ends with exit 28 and
  `Operation timed out after N milliseconds with M bytes received`, or
  `with M out of T bytes received` while a Content-Length body is read;
- a connect that does not finish ends with exit 28 and
  `Connection timed out after N milliseconds`, whether `--connect-timeout` or `-m` ran out;
- a send the peer reset ends with exit 55 and `Send failure: Connection was reset`.

`IConnector` takes only a `CancellationToken`, and `Curl.Networking.UnitLibrary` does not
read either timeout.

## Decision

- The handler owns both limits (`HttpTransferDeadline`). It starts one clock on
  `ITransferContext.TimeProvider` when the transfer starts; `-m` cancels a token that every
  read, write and connect of the transfer runs under, the authentication retry included,
  and each connect runs under its own `--connect-timeout` token as well. The connector is
  only asked to honour cancellation.
- With no `--connect-timeout`, or `--connect-timeout 0`, the connect limit is curl's
  `DEFAULT_CONNECT_TIMEOUT` of 300 seconds. `-m 0`, like no `-m`, sets no limit.
- N is whole milliseconds since the transfer started. M is the body bytes written so far,
  counted as `%{size_download}` counts them; T is the Content-Length of the body being read.
- A cancelled operation is a timeout unless the transfer's own token was cancelled, which
  still leaves the handler as `OperationCanceledException`.
- A write or flush that throws `IOException` is exit 55: `Send failure: Connection was
  reset` for a reset, and `curl_easy_strerror`'s `Failed sending data to the peer` for
  anything else, as the receive side already does for exit 56.

## Consequences

Good:

- Timeouts are tested on `FakeTimeProvider` with no real delay and no socket.
- The connector stays a plain seam; no timeout reaches `IConnector` or `ConnectTarget`.

Costs and caveats:

- `-m` runs per handler call. `Curl.Core`'s `RedirectFollower` calls the handler once per
  hop, so a `-L` chain gets `-m` per hop rather than for the whole chain as curl does. Filed
  as follow-up work.
- DNS resolution happens inside the connector, so a slow lookup reports curl's connect
  message rather than `Resolving timed out after N milliseconds`.
- A connector that throws `OperationCanceledException` on its own would be reported as a
  timeout.

## Alternatives considered

- **Pass the timeouts to the connector.** Rejected: it changes the shared
  `Curl.Protocol.Abstractions` contract for a limit the handler can impose with the token
  it already passes.
- **No default connect limit.** Rejected: curl applies 300 seconds, and a drop-in
  replacement must end at the same point.
