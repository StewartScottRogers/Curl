---
id: BL-363
title: Refuse a malformed --ipfs-gateway in IpfsGatewayRewriter with curl's exit 43
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Core.UnitLibrary, Curl.Core.UnitTests]
requirement: none
created: 2026-09-27
completed:
---
# BL-363 — Refuse a malformed --ipfs-gateway in IpfsGatewayRewriter with curl's exit 43

## Goal

`IpfsGatewayRewriter` refuses an `ipfs://` or `ipns://` URL whose `--ipfs-gateway` text is malformed with exit 43 and curl's message `--ipfs-gateway was given a malformed URL`, not today's `malformed target URL` exit 3.

## Context

- Found by BL-353, which parses `--ipfs-gateway` in `Curl.Cli` and accepts any non-empty value, because curl checks the gateway only when it rewrites an IPFS URL.
- Measured on curl 8.21.0 (mingw, Schannel) on 2026-09-27: `curl -s --ipfs-gateway ::: ipfs://cid` and `curl -s --ipfs-gateway foo://h:1/ ipfs://cid` print `curl: --ipfs-gateway was given a malformed URL`, then `curl: try 'curl --help' or 'curl --manual' for more information`, exit 43 (`CURLE_BAD_FUNCTION_ARGUMENT`), `-s` not hiding it. With `http://127.0.0.1:1/` as the URL the same options are ignored (exit 7).
- `Curl.Core.UnitLibrary/IpfsGatewayRewriter.cs` maps a gateway that fails `TryParseGateway` to `IpfsGatewayFailure.MalformedTargetUrl` (exit 3) whatever its source. Measure the `IPFS_GATEWAY` and gateway-file cases before changing them; this task is about the `--ipfs-gateway` source.
- Upstream: `src/tool_ipfs.c` in curl 8.21.0.

## Acceptance criteria

- [ ] An `IpfsGatewayFailure` for a malformed `--ipfs-gateway` carries exit 43 and the message `--ipfs-gateway was given a malformed URL`; `IpfsGatewayRewriterTests` pins it for `:::` and `foo://h:1/`.
- [ ] `dotnet build -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Core`.

## Notes

## Log

- 2026-09-27: Created.
