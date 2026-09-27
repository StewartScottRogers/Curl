---
id: BL-338
title: Set ITransferContext.Proxy in Curl.Console and refuse unsupported tunnels for every scheme
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-337]
touches: [Curl.Console, Documentation/Planning/Decisions/ADR-0053-curl-console-refuses-a-proxy-tunnel-the-connector-cannot-open-yet-with-exit-4.md]
requirement: none
created: 2026-09-27
completed:
---
# BL-338 — Set ITransferContext.Proxy in Curl.Console and refuse unsupported tunnels for every scheme

## Goal

`Curl.Console` sets `TransferContext.Proxy` from `TransferProxySelection` for every scheme except `file`, and refuses a tunnelled non-HTTP scheme through a SOCKS or HTTPS proxy with exit 4, as it refuses `http` and `https` ones.

## Context

- ADR-0056, rules 1, 5 and 6; ADR-0053 for the guard and its message.
- `TransferProxySelection.NeedsTunnelNotBuiltYet` covers only `http`/`https` today; once handlers read `Proxy`, `curl --socks5 h dict://x/` would reach `TcpConnector` and throw `NotSupportedException`.
- `-U` applies to `Proxy` as to `ForwardProxy`; both come from the one selection.

## Acceptance criteria

- [ ] A test shows `-x http://127.0.0.1:1 dict://example.com/d:x` produces a context whose `Proxy` is `127.0.0.1:1`, kind `Http`.
- [ ] A test shows a `file://` transfer with `-x` gets `Proxy == null`.
- [ ] A test shows `--socks5 127.0.0.1:1 dict://example.com/d:x` ends with exit 4 and ADR-0053's `Unsupported proxy` message, with nothing connected.
- [ ] ADR-0053's decision no longer says other schemes are not refused; it points to ADR-0056.

## Notes

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
