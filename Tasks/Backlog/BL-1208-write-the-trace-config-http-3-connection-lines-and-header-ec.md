---
id: BL-1208
title: Write the --trace-config http/3 connection lines and header echoes
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Curl.Quic.UnitLibrary, Curl.Quic.UnitTests, Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-10-02
completed:
---
# BL-1208 — Write the --trace-config http/3 connection lines and header echoes

## Goal

Under `-v --trace-config http/3` an HTTP/3 transfer also writes curl's QUIC connection lines and its per-header `status:` / `header:` echoes, as curl 8.18.0's ngtcp2 build does.

## Context

- Follow-up of BL-1168 (ADR-0375), which writes the stream lines (`end_headers`, `DATA len`, `ACK`, `CLOSED`, `quic close`) from `Http3StreamConnection` through `Http3StreamTrace`.
- Measured in BL-1168's Notes against `https://cloudflare-quic.com/`. Still unwritten:
  - connection lines: `handshake complete after <n>ms, remote transport[max_udp_payload=<n>, initial_max_data=<n>]`, `max bidi streams now <n>, used 0`, `peer verified`, `connect -> 0, done=1`, `peer idle timeout is <n>ms, set keep-alive to <n> ms.`, and at the end `[0] easy handle is done`, `no active streams, unset keep-alive`, `query conn[0]: MAX_CONCURRENT -> <n> (0 in use)`;
  - echoes: `[0] status: HTTP/3 200 ` followed by an empty line, then `[0] header: <name>: <value>` before each `<` header line. They interleave with the `<` lines, so they belong where the response head is parsed (compare BL-1205 for HTTP/2).
- The connection lines need the QUIC layer's transport parameters and handshake time; find where `IMultiplexedConnection` is made and how a trace sink can reach it.

## Acceptance criteria

- [ ] Tests pin the connection lines above, in curl's places, under `--trace-config http/3`.
- [ ] Tests pin the `status:` / `header:` echoes in curl's places among the `<` lines.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-02: Created.
