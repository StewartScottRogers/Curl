# ADR-1062 — The AMQP 1.0 client sends or receives one message and maps each AMQP error to one exit code

- **Status:** Accepted
- **Date:** 2026-10-29

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-4257.

## Context

BL-4257 adds `Amqp1ProtocolHandler` in `Curl.Extension.Protocol.Amqp1.UnitLibrary/Client`, serving `amqp1://host[:5672]/address`
and `amqp1s://host[:5671]/address` over the frames of ADR-1058. Upstream curl has no AMQP, so there is no curl output
to match; the OASIS AMQP Version 1.0 specification decides the wire, and curl's own exit codes decide how a failure
ends.

## Decision

- **One message, one link.** With a request body (`-d`, `-T`) the client attaches as sender (role false) on handle 0
  to the URL-decoded path as target, waits for link credit, sends the body as one data section (split across frames
  by the smaller of the peer's max-frame-size and 65536) and waits for a disposition; otherwise it attaches as receiver
  with the path as source, grants link-credit 1, reads the one transfer (across frames while `more` is set), settles
  it accepted unless it came settled, and writes its data, amqp-value or amqp-sequence sections to the output (binary
  as is, strings and symbols in UTF-8, scalars as invariant text). Then detach (closed), end and close.
- **SASL first.** `--user` offers PLAIN with an empty authorization identity, otherwise ANONYMOUS; the hostname is the
  URL's host.
- **Exit codes.**

| Failure | Exit code | Message |
| --- | --- | --- |
| No address in the URL path | 3 (`URL_MALFORMAT`) | `No AMQP 1.0 address found. Forgot to URL encode it?` |
| Mechanism not in sasl-mechanisms | 67 (`LOGIN_DENIED`) | `AMQP 1.0 server does not offer the SASL <mechanism> mechanism` |
| sasl-outcome code other than ok | 67 (`LOGIN_DENIED`) | `AMQP 1.0 SASL authentication failed with outcome code <code>` |
| Detach, end or close with error `amqp:unauthorized-access` | 67 (`LOGIN_DENIED`) | `AMQP 1.0 <performative> error: <condition>: <description>` |
| Detach, end or close with error `amqp:not-found` | 78 (`REMOTE_FILE_NOT_FOUND`) | as above |
| Detach, end or close with any other error | 8 (`WEIRD_SERVER_REPLY`) | as above |
| Disposition rejected, released or modified | 55 (`SEND_ERROR`) | `AMQP 1.0 message rejected: <condition>: <description>`, or `AMQP 1.0 message <outcome>` |
| Unexpected performative or malformed bytes | 8 (`WEIRD_SERVER_REPLY`) | `Weird server reply: ...` |
| Connection closed, read or write failure | 56 (`RECV_ERROR`) / 55 (`SEND_ERROR`) | curl's own texts |

An attach refused by the peer arrives, per Part 2 section 2.6.3, as an attach followed by a detach carrying the
error, so it takes the detach row.

## Consequences

- A script tells "no such queue" (78) from "not allowed" (67) from a broken peer (8), as it does for curl's other
  protocols.
- One message per transfer keeps the client to a single link and delivery id; batching stays out of scope until a task
  asks for it.
