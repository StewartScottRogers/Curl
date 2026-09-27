---
id: BL-330
title: Send non-HTTP schemes through a selected proxy as curl does
priority: Normal
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions]
requirement: none
created: 2026-09-27
completed:
---
# BL-330 — Send non-HTTP schemes through a selected proxy as curl does

## Goal

A non-HTTP transfer (for example `ftp://`, `dict://`, `gopher://`) with `-x` or a proxy variable goes through the proxy as curl 8.21.0 does, or is refused as it refuses it.

## Context

- Filed by BL-238 (2026-09-27). The proxy chosen by `TransferProxySelection` reaches only `HttpRequestOptions.ForwardProxy`, which only `HttpProtocolHandler` reads; every other handler connects directly whatever `-x`, `all_proxy` or `--socks5` say. curl forwards `ftp://` through an HTTP proxy as a GET, and tunnels other schemes with `-p` or a SOCKS proxy.
- Needs a way for non-HTTP handlers to see the proxy, probably `ConnectTarget.Proxy` set from the transfer context; that may touch `Curl.Protocol.Abstractions.UnitLibrary` and each protocol library - split per protocol when planning.
- Where a criterion says *measured*, run curl 8.21.0 - the mingw build `/mingw64/bin/curl`, first on PATH (ADR-0009) - against a loopback server (`Record-CurlExchange.ps1`), record the exact command and the bytes in `Notes`, then pin them in a test. Never pin text that was not measured.

## Acceptance criteria

- [ ] Measured: `curl -x <proxy> dict://example.com/d:x`, `curl -x <proxy> gopher://example.com/` and `curl -p -x <proxy> dict://example.com/d:x` - the bytes the proxy receives are recorded in `Notes` and each case's route is decided in an ADR.
- [ ] Follow-up tasks per protocol are filed for the decided routes.
- [ ] The ADR is indexed in `Documentation/Planning/Decisions/README.md`.

## Notes

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
