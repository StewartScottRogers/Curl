# ADR-1127: --connect-rpc sends Connect unary and streaming calls with JSON

- Status: Accepted
- Date: 2026-10-29
- Task: BL-4104
- Decided by Claude under Stewart's delegation

## Context

BL-4104 adds the extension `Curl.Extension.ConnectRpc.UnitLibrary` (ADR-0999): an option that sends a Connect
protocol call. Connect runs over plain HTTP POSTs, so the extension is a transfer hook over curl's own HTTP
stack (TLS, proxies, `--user`, `-H`), like `--jsonrpc` (ADR-1028), and adds no URL scheme.

## Decision

- **Option:** `--connect-rpc <unary|stream>`, one value as every extension option takes (ADR-1028). The URL is
  the procedure's path (`https://host/<package>.<Service>/<Method>`). Any other value fails with exit 2 before
  anything is sent.
- **Specification:** the Connect protocol reference at https://connectrpc.com/docs/protocol, as published in
  October 2026 (protocol version 1).
- **Request message:** the `-d`/`--data-binary` body, `{}` when none is given. A unary call sends it as is with
  `Content-Type: application/json` ("Unary-Request"). A streaming call reads it as JSON values one after
  another (white space between them optional, so `-d`'s removal of line breaks does no harm) and sends each in
  its own envelope (flags 0, 4-byte big-endian length) with `Content-Type: application/connect+json`
  ("Streaming-Request"). A body curl reads from a stream (`-T`, `@-` streamed) fails with exit 2.
- **Headers:** `Connect-Protocol-Version: 1` on both kinds, and `Connect-Timeout-Ms` from `--max-time`,
  rounded up to whole milliseconds and capped at ten digits ("Timeout"). The user's `-H` headers follow them.
- **Unary response:** written unchanged, success message or error JSON, so `-f`, `-i`, `-w` and `%{http_code}`
  work as for any HTTP transfer ("Unary-Response").
- **Streaming response:** each message's JSON written on its own line. An end-of-stream message carrying an
  `error` fails the transfer with exit 22 (curl's code for an HTTP error) and the message
  `Connect error <code>: <message>`, the code `unknown` when none is named ("End-Stream Message", "Error
  Codes"). A body that is not envelopes ending in an end-of-stream message, or one with a compressed message,
  fails with exit 8 (weird server reply). A transfer that itself failed keeps its own result.
- Without `--connect-rpc`, or with `CURL_EXTENSIONS=0` (the option is then unknown, exit 2), nothing changes.

## Left out, and why

- **The protobuf codec (`application/proto`, `application/connect+proto`).** Curl has no schema to encode
  messages from, and splitting a streaming body needs a boundary that binary protobuf does not give. JSON is the
  codec every Connect server must accept.
- **Compression (`Connect-Content-Encoding`, `Connect-Accept-Encoding`).** None is offered, so a conforming
  server sends none; `--compressed` still governs the HTTP layer of a unary call.
- **Unary GET requests (`?connect=v1&encoding=...&message=...`).** Optional in the specification and only for
  side-effect-free procedures; a POST works for every procedure.
- **End-of-stream `metadata` (trailers).** Not shown: curl has no place for trailers in its output today.
- **Bidirectional streaming interleaving.** All request messages are sent before the response is read, which
  covers client, server and half-duplex bidirectional streams over HTTP/1.1 and HTTP/2.
