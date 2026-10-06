---
id: BL-210
title: Rewrite ipfs:// and ipns:// URLs to gateway URLs
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Core.UnitLibrary, Curl.Core.UnitTests]
requirement: none
created: 2026-09-26
completed: 2026-09-27
---
# BL-210 — Rewrite ipfs:// and ipns:// URLs to gateway URLs

## Goal

`ipfs://` and `ipns://` URLs are rewritten to gateway URLs from `--ipfs-gateway`, then `IPFS_GATEWAY`, then `~/.ipfs/gateway`.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item K8. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- https://curl.se/docs/ipfs.html (curl 8.21.0).
- Where a criterion says *measured*, run curl 8.21.0 - the mingw build `/mingw64/bin/curl`, first on PATH, which is the Windows reference (ADR-0009 and the BL-153 ADR) - against a loopback server (the BL-165 recording script `Record-CurlExchange.ps1` once it exists), record the exact command and the bytes it produced in `Notes`, then pin them in a test. Never pin text that was not measured.

## Acceptance criteria

- [x] Each source in order is tested with an injected environment and file system; the error when none is configured is measured and pinned.
- [x] `dotnet build Curl.Core.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Core`.

## Notes

- Plan item: K8 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.

- Plan: one class, `Curl.Core.IpfsGatewayRewriter`, plus the `IpfsGatewayFailure` record; the environment and the gateway file are injected as `Func<string, string?>` (as `ProxySelector` does), and the input is an already-parsed `CurlUrl`, because curl rejects a bad `ipfs://` host (`(3) URL rejected: Bad hostname` / `No host part in the URL`) in its URL parser before the rewrite. Tests: `Curl.Core.UnitTests/IpfsGatewayRewriterTests.cs` (78 cases).
- Measured on curl 8.21.0 (`/mingw64/bin/curl`, Schannel) on 2026-09-27 with `curl -sS -m 1 -w '%{url_effective}
' [--ipfs-gateway <g>] <url>`, gateways on unreachable loopback ports (no server is needed: `%{url_effective}` shows the rewrite). Key results, all pinned:
  - No gateway anywhere (`HOME` without `.ipfs`): `curl: IPFS automatic gateway detection failed` + the `try 'curl --help'` line, exit 37.
  - `--ipfs-gateway http://127.0.0.1:1 ipfs://bafyabc/x` -> `http://127.0.0.1:1/ipfs/bafyabc/x`; the port is always written (`http://127.0.0.1` -> `:80`, `https` -> `:443`); input user info, query and fragment kept, empty ones dropped; input port and gateway user info dropped; an empty user or password is kept as written (`ipfs://u:@cid/x` -> `http://u:@127.0.0.1:1/ipfs/cid/x`, `ipfs://@cid/x` -> `http://@127.0.0.1:1/ipfs/cid/x`, found by the review stage). A gateway with no scheme gets `UrlSchemeGuesser`'s guess (`ftp.127.0.0.1` -> `ftp://ftp.127.0.0.1:21/...`).
  - Order: `--ipfs-gateway` beats `IPFS_GATEWAY` beats `$IPFS_PATH/gateway` beats `$HOME/.ipfs/gateway`; only the first line of the file (up to CR or LF) counts, an empty one is exit 37. `USERPROFILE` is never read, even with `HOME` empty.
  - `curl: malformed target URL`, exit 3: gateway with a query, IPv6 host, no host (`file:///C:/x`), unknown scheme from the environment, no scheme from the environment or file, empty `IPFS_GATEWAY`, surrounding spaces, and a path decoding to a byte below 0x20.
  - The path is decoded and re-encoded: space `" # % < > ? \ ^ \` |` and 0x7F-0xFF become `%XX`, everything else printable is literal (full 0x20-0x7E and 0x80-0xFF sweeps pinned).
- Defaults taken (rule 1), none measured because they are unreachable or platform-bound: raw non-ASCII in a `CurlUrl` path is encoded as UTF-8 (the mingw build received the console code page, so `/é` gave `%E9`, which is argv encoding, not the rewrite); an empty `IPFS_PATH` reads `/gateway`; a gateway whose scheme is `ipfs`/`ipns` is malformed. No ADR: every pinned behaviour is curl's measured one, not a design choice.
- Option-time checks on `--ipfs-gateway` (blank -> exit 2, malformed or unknown scheme -> exit 43, `--ipfs-gateway was given a malformed URL`) belong to Curl.Cli, outside `touches`: filed as BL-353 and added to BL-240's `depends-on`.
- Gates: `dotnet build -warnaserror` clean; fast tests green (Curl.Core.UnitTests 754 passed, 3 skipped); `Measure-CodeQuality.ps1 -Library Curl.Core.UnitLibrary`: 100% line, 100% branch, 0 failing members, worst CRAP 10.

## Log

- 2026-09-26: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. Curl.Core.IpfsGatewayRewriter rewrites ipfs:// and ipns:// URLs to gateway URLs from --ipfs-gateway, IPFS_GATEWAY or the gateway file, with curl 8.21.0's exit 37 and exit 3 failures
