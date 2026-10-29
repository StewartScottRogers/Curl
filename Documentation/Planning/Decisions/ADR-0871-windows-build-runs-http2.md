# ADR-0871: The Windows build runs HTTP/2 and lists HTTP2 in curl -V

- Status: Accepted
- Date: 2026-10-23
- Decision: Stewart's (2026-10-22, Libcurl API conformance batch, report 31: "do it"); recorded by Claude (BL-3207)
- Supersedes: the HTTP/2 part of [ADR-0397](ADR-0397-the-windows-build-refuses-the-options-curls-schannel-build-lacks.md);
  the `HTTP2` part of [ADR-0450](ADR-0450-windows-features-line-omits-http2.md)

## Context

ADR-0397 made Curl on Windows refuse `--http2`, `--http2-prior-knowledge` and `--proxy-http2` with exit 2
(`the installed libcurl version does not support this`), because System32's curl (8.21.0, Schannel) has no
HTTP/2 and refuses them, and ADR-0450 left `HTTP2` off the Windows `curl -V` Features line for the same
reason. Curl's engine has its own HTTP/2 (ADR-0141) and runs these options on Linux and macOS already.

## Decision

Stewart decided to turn HTTP/2 on for Windows:

- `--http2`, `--http2-prior-knowledge` and `--proxy-http2` (and `--no-proxy-http2`) parse and run on
  Windows as on Linux and macOS. `--ssl-sessions` stays refused there (ADR-0397's other part holds).
- The Windows `curl -V` Features line lists `HTTP2` after `HSTS`, upstream's position, and
  `curl_version_info` sets `CURL_VERSION_HTTP2` there. The version line stays System32's, with no
  `nghttp2/...` text, since Curl's HTTP/2 is its own.
- The behaviour follows an nghttp2-built curl 8.22.0 (recorded in
  `Documentation/Product/Http2OriginRecordings.md`, BL-3209).

## Consequences

- Drop-in trade-off: a Windows script that relied on the refusal (exit 2) now sees a transfer. Stewart
  accepted that in exchange for HTTP/2 on Windows.
- `--ai-help` no longer carries the Windows refusal line for the three options; it follows the option
  table's `RefusedBySchannelBuild` rows.
- The upstream case runner still reports `http/2` and `h2c` off Windows; BL-3268 turns them on.
