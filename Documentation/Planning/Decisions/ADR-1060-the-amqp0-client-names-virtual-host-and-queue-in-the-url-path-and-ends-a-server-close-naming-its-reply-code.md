# ADR-1060 — The AMQP 0-9-1 client names the virtual host and queue in the URL path and ends a server close naming its reply code

- **Status:** Accepted
- **Date:** 2026-10-29

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-4260.

## Context

BL-4260 adds `Amqp0ProtocolHandler` and `Amqp0ClientFlow` to `Curl.Extension.Amqp0.UnitLibrary` (ADR-0999) for
`amqp://` (port 5672) and `amqps://` (port 5671), on BL-4259's codec (ADR-1056). Upstream curl has no AMQP, so the
model for what the user sees is curl's `mqtt://` (and ADR-1049 for MQTT 5): with `-d` publish, otherwise receive and
write the payload. The URL form follows RabbitMQ's URI specification, `amqp://user:pass@host:port/vhost`, which names
no queue.

## Decision

- **URL.** The path is `/<virtual host>/<queue>`, each segment URL-decoded. An empty virtual host is `/`, so
  `amqp://host//jobs` and `amqp://host/%2F/jobs` both use RabbitMQ's default virtual host. Any other path (no queue,
  an empty queue, more segments) ends with exit 3 `CURLE_URL_MALFORMAT` and `No AMQP queue found: the URL path must
  be /<virtual host>/<queue>` before anything is connected. The URL names no exchange or routing key: a publish goes
  to the default exchange with the queue as routing key, which delivers it to that queue, and that is the one thing a
  curl-style "send this there" needs. A later task can add a query form without breaking this one.
- **Flow.** Protocol header; connection.start must offer `PLAIN`, answered by start-ok with the PLAIN response from
  `--user` (guest/guest without one, RabbitMQ's default), locale `en_US`, and client-properties `product=curl` with the
  `authentication_failure_close` capability so RabbitMQ reports a failed login with connection.close 403 rather than
  dropping the socket; tune-ok takes the smaller of the server's and the client's channel-max (2047) and frame-max
  (131072), a server 0 meaning no limit, and heartbeat 0 (none: the transfer is short); connection.open of the virtual
  host; channel.open on channel 1; queue.declare (passive, durable, exclusive, auto-delete all off). With a body:
  basic.publish to the default exchange, then the content header and body frames split at the negotiated frame-max,
  in one write. Without: basic.get with no-ack; get-ok's body is written to the output, get-empty is an empty
  successful transfer. Then channel.close and connection.close (200, `Goodbye`), each awaiting its close-ok. Heartbeat
  frames from the server are skipped.
- **Exit codes.**

  | Failure | Exit code | Message |
  |---|---|---|
  | connection.close or channel.close from the server with reply code 403 access-refused | 67 `CURLE_LOGIN_DENIED` | `AMQP server sent <connection.close or channel.close>: reply code <code>, <reply text>` |
  | connection.close or channel.close from the server with any other reply code | 8 `CURLE_WEIRD_SERVER_REPLY` | the same |
  | connection.start offers no PLAIN mechanism | 67 | `AMQP server offers no PLAIN mechanism (it offers "<mechanisms>")` |
  | Another method where one is due | 8 | `Weird server reply: unexpected AMQP method <class>.<method>` |
  | Frames that break the wire format, or a server answering with a protocol header | 8 | `Weird server reply: AMQP <reply code> <fault>`, the reply code of ADR-1056 |
  | The server closes the connection while a frame is due | 56 `CURLE_RECV_ERROR` | curl's `Connection disconnected` |
  | A read or a write on the connection fails | 56 / 55 | curl's `Failure when receiving data from the peer` / `Failed sending data to the peer` |
  | The output cannot be written | 23 `CURLE_WRITE_ERROR` | curl's `Failure writing output to destination, passed N returned 0` |
  | No queue in the URL | 3 `CURLE_URL_MALFORMAT` | as above |

  A close from the server is answered with its close-ok first, best effort: a connection already gone cannot take
  it, and the server's reply is still the failure reported. 403 maps to 67 because it is the login failing, as an FTP
  530 or an SSH refused password is in curl; every other reply code is the server refusing what curl asked for, which
  ADR-1049 already maps to 8 for MQTT 5.
- **TLS.** `amqps://` runs the same flow over the connection the connector returns for a `ConnectTarget` with
  `UseTls`; the handler never builds a socket or a TLS stream.

## Consequences

- A queue declared by this client must match an existing queue's flags (durable off): declaring a durable queue
  RabbitMQ already has ends with channel.close 406 precondition-failed, exit 8, naming it.
- The handler is not registered anywhere yet; BL-4117 adds the option, the catalogue line and `--ai-help`.
