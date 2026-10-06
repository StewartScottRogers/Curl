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
completed: 2026-09-27
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

- [x] An `IpfsGatewayFailure` for a malformed `--ipfs-gateway` carries exit 43 and the message `--ipfs-gateway was given a malformed URL`; `IpfsGatewayRewriterTests` pins it for `:::` and `foo://h:1/`.
- [x] `dotnet build -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Core`.

## Notes

- Measured curl 8.21.0 (mingw, Schannel) 2026-09-27, `curl -s -m 1 --ipfs-gateway <g> ipfs://cid/x`: exit 43 `--ipfs-gateway was given a malformed URL` for `:::`, `foo://h:1/`, `http://h:1/ x`, `garbage ::`, `http://`, `http://:1/`, `http://u@/`, `ftp://`, `file://`, `file://h/x`, `http://h:99999/`; still exit 3 `malformed target URL` for a query, an IPv6 host, `file:///x`, `file://localhost/x`, `FILE:///x`, `file:///C:/x`. `IPFS_GATEWAY` of `:::`, `foo://h:1/` or `http://`, and a gateway file holding `foo://h:1/`, stay exit 3. This matches `tool_ipfs.c`: only a `curl_url_set` failure on the option is `CURLE_BAD_FUNCTION_ARGUMENT`.
- Design: `TryParseGateway` now models `curl_url_set` (parses, known scheme, a host unless `file`); query, IPv6 and empty-host checks moved to `IsUsableGateway`, which stays exit 3 for every source. `TryResolveGateway` was extracted to keep `TryRewrite` at complexity 10. No ADR: no choice beyond matching measured curl.
- The old `foo://h:1/` and `http://h:1/ x` rows under `TryRewrite_UnusableGatewayOption_IsMalformedTargetUrl` were wrong for the option source; they moved to the new exit-43 test.
- `Curl.Console` still maps the new failure to exit 3 (right message, wrong code); `Curl.Console` is BL-242's this shift, so BL-403 is filed for it.
- Measure-CodeQuality: Curl.Core 100/100, 0 failing members, max Cx 10. The one failing member reported (`Curl.Console` `DiskWriteOutFileOpener.TryOpen`, from d41f141) is outside this task.

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. IpfsGatewayRewriter refuses an unparsable --ipfs-gateway with exit 43 and curl's message; environment and file gateways stay exit 3
