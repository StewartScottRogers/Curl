---
id: BL-4080
title: Fail a proxied transfer whose host holds a slash with exit 27 as curl does
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4079]
touches: [Curl.Engine.UnitLibrary/TransferPerformer.Setup.cs]
requirement: none
created: 2026-10-29
completed:
---
# BL-4080 — Fail a proxied transfer whose host holds a slash with exit 27 as curl does

## Goal

A transfer through an HTTP proxy to a host whose IDNA conversion holds a slash (`http://a℀b.test/` with `-x`) fails with exit 27 and `curl: (27) Out of memory` before sending a byte, as curl 8.18.0 with libidn2 2.3.8 does.

## Context

Measured in BL-4077's run (WSL Ubuntu, `LC_ALL=C.utf8`): with a socat fake proxy listening, `curl -x http://127.0.0.1:PORT http://a℀b.test/` exits 27 for U+2100, U+2101, U+2105 and U+2106, and the proxy receives nothing. With the proxy port closed, curl exits 7 (`Failed to connect to aa/cb.test port 80 via 127.0.0.1`), so the failure comes after the proxy connection opens, when curl builds the absolute-form request target. Direct transfers send `Host: aa/cb.test` (BL-4079). Find where Curl builds the proxy request target (start at `TransferPerformer.Setup.cs`, `HttpProtocolHandler.cs`) and narrow `touches` to the right file.

## Acceptance criteria

- [ ] A test pins exit 27 with `curl: (27) Out of memory` and no bytes sent for a proxied transfer to `http://a℀b.test/`.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Filed by BL-4077's run.

## Log

- 2026-10-29: Created.
