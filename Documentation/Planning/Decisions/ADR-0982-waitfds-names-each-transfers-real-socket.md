# ADR-0982: curl_multi_waitfds names each transfer's real socket, with or without a socket callback

Status: Accepted. Decided by Claude under Stewart's delegation (BL-4037, for CGR-034). Its point on a transfer
with no dialled socket is replaced in part by ADR-0997: `FirstTransferSocket` is gone.

## Context

`ManagedMultiTransfer.GetWaitSockets` (`curl_multi_waitfds`) gave every running transfer a number of the
handle's own from `FirstTransferSocket`, while libcurl gives the transfer's connection socket. With a socket
callback a `ManagedEasyTransfer` already runs on a `TransferSocketWait` that knows its real socket (ADR-0973,
ADR-0955); without one it had no wait, so the engine knew no socket to report.

## Decision

- A `ManagedEasyTransfer` started on a handle with no socket callback still gets a `TransferSocketWait`, with
  its gating stopped from the start (`StopGating`), so every read and write goes on at once and perform and
  wait drive the transfer as before. The socket-action dialer records its socket on it (`ConnectingOn`,
  `TakeSocket`) as it does with a callback. The multi entry keeps it apart from the gating `SocketWait`, so
  socket actions, the timer and the settle loop never see it.
- `GetWaitSockets` names that recorded socket (the operating system socket, or ADR-0955's engine number for a
  connection with none) with or without a socket callback, and a socket callback set later is told the same
  socket. (Replaced in part by ADR-0997.) Until the dial has a socket, and for a transfer that is not a
  `ManagedEasyTransfer`, the transfer is now told no socket and left out of `GetWaitSockets` (ADR-0997), where
  this ADR first kept a synthetic number from `FirstTransferSocket` for it.

## Consequences

- An application that polls the sockets `curl_multi_waitfds` gives polls the transfer's real socket, as with
  libcurl.
- A transfer without a socket callback now runs through the socket-action dialer and connection wrappers;
  with gating stopped they only pass calls through.
