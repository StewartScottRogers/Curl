# ADR-0821: A socket-action transfer's socket I/O waits for the multi handle's readiness signal, beneath TLS

- Status: Accepted
- Date: 2026-10-26
- Decision: Decided by Claude under Stewart's delegation (BL-3847)
- Relates to: [ADR-0599](ADR-0599-engine-multi-socket-action-and-callbacks.md), [ADR-0820](ADR-0820-iconnection-exposes-the-operating-system-socket-handle.md)

## Context

CGR-034 (`Documentation/Reviews/Curl-Gap-Report-2026-10-25.md`): real libcurl driven by
`curl_multi_socket_action` tells the socket callback `OUT` while it connects or sends and `IN` while it
awaits data, touches the socket only when the application reports it ready or the timer fires, and sets
the timer to its next deadline (`add T0 kick S1=2(OUT) T-1 S1=1(IN) S1=4(REMOVE)`; with
`CURLOPT_TIMEOUT_MS` 600 against a silent server `T0 S=2 T400+ S=1 S=4 T-1`, result 28).

The managed engine runs each transfer as its own task that reads and writes whenever data comes
(ADR-0599). Once the application polls the real socket (ADR-0820, BL-3563), such a transfer would take the
bytes before the application's level-triggered poll saw them, and the application would not be woken
again; ADR-0599's 10 ms polling timer exists for that.

## Decision

Two engine types, used only for a transfer in a multi handle driven by socket actions:

- `TransferSocketWait` is what the transfer waits for and by when. A read waits in
  `WaitUntilReadableAsync`, a write in `WaitUntilWritableAsync`, until the multi handle calls
  `Signal` with the events `curl_multi_socket_action` (or a perform) reported: `In` releases a read, `Out`
  a write, `Error` both, so the I/O meets the error. A signal that comes while nothing waits in its
  direction is kept for the next wait in it, since a level-triggered poll would report it again; each wait
  takes one signal. `Waiting` is `CurlPoll.Out` while the connect runs (`BeginConnect` to `EndConnect`) or a
  write waits, `CurlPoll.In` while a read waits, both when both do. `EarliestDeadline` is the earliest of
  the connect deadline (`CURLOPT_CONNECTTIMEOUT_MS`, else libcurl's 300 s `DEFAULT_CONNECT_TIMEOUT`, until
  the connect ends), `CURLOPT_TIMEOUT_MS` from `Start`, the low-speed time counted from the start of each
  wait when `CURLOPT_LOW_SPEED_LIMIT` and `CURLOPT_LOW_SPEED_TIME` are both set (no bytes move while it
  waits, so that is when libcurl's low-speed check would fire), and the expect-100 timeout between
  `BeginExpectContinueWait` and `EndExpectContinueWait`. `Changed` is raised after each change, for the
  multi handle to call the socket and timer callbacks.
- `SocketActionGatedConnection` wraps a dialed plaintext TCP connection and makes its reads and writes
  wait on the `TransferSocketWait`; every other member is the inner connection's.

The gate sits beneath TLS and HTTP/2 framing, on the connection the `ITcpDialer` gives, not on the
connection the pool hands a transfer. TLS (`SslStream` included, which exposes no count of bytes it holds)
and the HTTP/2 framer read their socket only when what they already hold is spent, so a read they answer
from buffered records or frames never reaches the gate and never waits on a signal the application would
never give, its socket being empty. The socket is the unit the application polls and the multi handle
signals, so a pooled connection reused by a later transfer keeps the gate of the socket it wraps.

The timer does not release a waiting read or write: the transfer's own timeouts end it through its
cancellation token, which ends the wait. A perform (not a socket action) signals every socket `In | Out`.

## Consequences

- The two types are built and unit tested; nothing routes a transfer through them yet. BL-3849 wraps the
  transfer's dialer and calls `Start`, `BeginConnect`, `EndConnect` and the expect-100 pair; BL-3563 has
  `ManagedMultiTransfer` signal each socket from `SocketActionAsync` and report `Waiting` and
  `EarliestDeadline` through the callbacks, and then removes ADR-0599's `RunningTransferCheckInterval`
  polling, which this decision is to replace. Until then ADR-0599 stands as it is.
- Easy performs and `curl_multi_perform` never create a `TransferSocketWait`, so they run as before.
