# ADR-1132 — --grpc-web sends one framed message and checks grpc-status

- **Status:** Accepted
- **Date:** 2026-10-29

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-4103.

## Context

BL-4103 adds calling gRPC-Web services as an extension (ADR-0999) in `Curl.Extension.GrpcWeb.UnitLibrary`. curl has
no such option, so the extension is tested against the gRPC-Web protocol as grpc.io publishes it
(`doc/PROTOCOL-WEB.md` in grpc/grpc, the current text on 2026-10-29, which builds on `doc/PROTOCOL-HTTP2.md`), the
version followed, and never changes what curl does without the option. ADR-1019's transfer hook may replace the
request body and read the response before curl writes it.

## Decision

- **Option.** One flag, `--grpc-web`, with no argument; curl has no option of that name. It is a transfer hook
  (ADR-1007, ADR-1019) registered in the option, description and transfer hook catalogues; it has no URL scheme, so,
  as ADR-1094's `--content-digest`, it adds no line to `ExtensionCatalogue`. The URL names the method
  (`https://host/package.Service/Method`), as gRPC's path does.
- **Request.** The `-d` / `--data-binary` data is the serialized request message (a protobuf the user encoded); with
  none, an empty message. It is sent as one uncompressed frame (flags 0, four-byte big-endian length, "Message
  framing") with `Content-Type: application/grpc-web+proto` ("Content-Type"), `Accept: application/grpc-web+proto`
  and `X-Grpc-Web: 1`, the header gRPC-Web clients send and proxies such as Envoy look for. A header `-H` already
  gave is left to it. A `-F` body fails the transfer with exit 2: a form is not a message.
- **HTTP version.** Whatever curl's own options pick: HTTP/1.1 by default, HTTP/2 with `--http2` or over TLS ALPN
  ("HTTP wire protocols": gRPC-Web runs over any HTTP/*). The extension writes no bytes of HTTP itself.
- **Reply.** A 200 reply whose content type is `application/grpc-web` or `application/grpc-web+<format>` is read as
  frames: each data frame's message is written to the output without its frame; the trailers frame (flags bit
  0x80, an HTTP/1 header block) must be the last ("Message framing"). With no trailers frame, the response headers
  carry the status ("Trailers-only responses"). `grpc-status` 0 succeeds; another fails with exit 22, curl's code for
  a server's error answer, and `--grpc-web: the call failed with grpc-status N: <grpc-message percent-decoded>`; none
  at all, a frame cut short or a frame after the trailers fail with exit 8 (weird server reply); a compressed frame,
  which Curl never asks for (it sends no `grpc-accept-encoding`), fails with exit 61. Any other reply (not 200, not
  gRPC-Web, or a failed transfer) is written as curl received it, so a proxy's error page shows as it would without
  the option.

## Left out, and why

- **`application/grpc-web-text`** (base64 bodies, "Text-encoded (response) streams") exists for browsers that
  cannot read binary streams; curl reads binary, so text replies are written as received.
- **Streaming calls.** gRPC-Web has no client streaming, and a server-streaming reply is read whole, as the
  messages are written in order once the transfer ends; a unary call is the common case.
- **Message compression and `grpc-timeout`.** Curl asks for neither; `--max-time` bounds the call.
- **Protobuf encoding.** Messages are bytes the user supplies and reads; turning JSON into protobuf needs the
  service's schema, which is beyond a transport option.
