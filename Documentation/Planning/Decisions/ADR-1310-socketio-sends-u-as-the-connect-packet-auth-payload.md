# ADR-1310: The socketio client sends -u as the CONNECT packet's auth payload

- Status: Accepted
- Date: 2026-11-19
- Task: BL-5567
- Decided by Claude under Stewart's delegation.
- Amends ADR-1035.

## Context

Socket.IO protocol v5 carries a client's credentials in the `auth` object of the namespace CONNECT packet (`0{...}`; the reference server hands it to its `io.use` middleware as `socket.handshake.auth`). The client sent a bare `0`, so it could join only a server that allowed anonymous clients (bug report 2026-11-19T0930, finding 4). The handshake already carried `Authorization: Basic` for `-u` (`HandshakeHeaderFields`).

## Decision

With `-u user:pass` the CONNECT packet is `40[/ns,]{"username":"user","password":"pass"}`, the JSON written by `Utf8JsonWriter` (so quotes are escaped). The handshake's `Authorization` header stays, so a server checking either form accepts the client. Without `-u` the packet is the bare `0[/ns,]` as before. A server that refuses the credentials sends a CONNECT_ERROR (`44`), which ends the transfer with exit 22 and the server's data, as for any refused namespace. The password is never written to `-v` output, `--log-file` or an error message: the session writes no packet to the log, and the refusal message quotes only the server's data.

The field names `username` and `password` are a choice: the protocol fixes only that `auth` is an object. Not measured against Surl, which was not available to the lane.

## Consequences

`--extended-help` for `socketio` describes the mapping.
