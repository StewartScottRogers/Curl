---
id: BL-1397
title: Decide how Curl builds --proxy-http2 and --proxy-http3 per platform, record the ADR and file the work
priority: Normal
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions]
requirement: none
created: 2026-10-03
completed: 2026-10-03
---
# BL-1397 — Decide how Curl builds --proxy-http2 and --proxy-http3 per platform, record the ADR and file the work

## Goal

An ADR, "Decided by Claude under Stewart's delegation", says how Curl implements `--proxy-http2` (an HTTP/2 CONNECT tunnel to an HTTPS proxy) and `--proxy-http3`, which platforms accept each option and which refuse it as the installed libcurl does, and the implementation tasks it calls for are filed on the board.

## Context

- Today `--proxy-http2` and `--proxy-http3` are the only options of curl 8.21.0's release option table (`src/tool_getparam.c` lines 252-253) with no row in `Curl.Cli.UnitLibrary/CommandLineOptionTable.cs`; they exist only in `CurlOptionAliasTable.cs` (line 196 and the next) and `CurlHelpTable.cs` (line 181), so both are refused with `curl: option --proxy-http2: the installed libcurl version does not support this` and exit 2 (ADR-0137).
- curl 8.21.0, `src/tool_getparam.c` lines 2030-2046: `--proxy-http2` needs the HTTPS-proxy and HTTP/2 features and sets `CURLPROXY_HTTPS2` (`--no-proxy-http2` sets `CURLPROXY_HTTPS`); `--proxy-http3` is refused unless libcurl is built with `USE_PROXY_HTTP3`, and then needs HTTPS-proxy and HTTP/3 and sets `CURLPROXY_HTTPS3`.
- Measured 2026-10-03: the installed curl 8.21.0 (mingw, Schannel, no nghttp2) refuses both with the message above and exit 2. The Linux and macOS OpenSSL builds the standing rules name ship with nghttp2, so they accept `--proxy-http2`; whether they define `USE_PROXY_HTTP3` must be settled from the curl 8.21.0 source and build docs and stated in the ADR.
- Relevant decisions to build on: ADR-0009 (TLS matches the platform's build), ADR-0061 and ADR-0190 (an HTTPS proxy is tunnelled over its own TLS handshake and offers `http/1.1` by ALPN), ADR-0159 (HTTP/2 over `IConnection`), ADR-0223 and ADR-0250 (HTTP/3 through a proxy), ADR-0137 (unimplemented options).
- The root `CLAUDE.md` default answer applies: a complete reimplementation, never "left out".

## Acceptance criteria

- [x] A new ADR under `Documentation/Planning/Decisions/` marked "Decided by Claude under Stewart's delegation" states: which platforms accept `--proxy-http2` and `--proxy-http3` and which keep the ADR-0137 refusal; the ALPN offered to the proxy (`h2`) and the fallback when the proxy picks `http/1.1`; which library holds the HTTP/2 CONNECT tunnel (named projects that exist in `Curl.slnx`); and the `-v` lines that must be measured before the work pins them.
- [x] The implementation tasks the ADR calls for are filed with `task-board.ps1 new`, each naming its project in `touches`, in dependency order, the option-parsing task first and keeping `--ai-help` right in its acceptance criteria.
- [x] `Documentation/Planning/Decisions/README.md` lists the new ADR if that README keeps an index.

## Notes

- No code changes in this task.
- Decided in ADR-0408: `--proxy-http2` refused on Windows (Schannel build has no HTTP2), accepted off Windows (measured: WSL curl 8.18.0 OpenSSL with nghttp2 accepts it); `--proxy-http3` refused everywhere (no reference build has HTTP3 or USE_PROXY_HTTP3; 8.18.0 does not know the option). Tunnel in Curl.Networking.UnitLibrary using Curl.Http2.UnitLibrary, ALPN `h2,http/1.1`, falls back to the HTTP/1.1 tunnel.
- Filed in dependency order: BL-1416 (Cli parsing, --ai-help), BL-1413 (Record-CurlExchange.ps1 -Http2 to measure the -v lines), BL-1414 (Networking tunnel, depends on BL-1413), BL-1415 (Console wiring, depends on BL-1416 and BL-1414).

## Log

- 2026-10-03: Created.
- 2026-10-03: Backlog -> Doing.
- 2026-10-03: Doing -> Done. ADR-0408 decides --proxy-http2/--proxy-http3 per platform; BL-1416..BL-1415 filed
