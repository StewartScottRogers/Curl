---
id: BL-338
title: Set ITransferContext.Proxy in Curl.Console and refuse unsupported tunnels for every scheme
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-337]
touches: [Curl.Console, Curl.Console.UnitTests, Documentation/Planning/Decisions/ADR-0053-curl-console-refuses-a-proxy-tunnel-the-connector-cannot-open-yet-with-exit-4.md]
requirement: none
created: 2026-09-27
completed: 2026-09-27
---
# BL-338 — Set ITransferContext.Proxy in Curl.Console and refuse unsupported tunnels for every scheme

## Goal

`Curl.Console` sets `TransferContext.Proxy` from `TransferProxySelection` for every scheme except `file`, and refuses a tunnelled non-HTTP scheme through a SOCKS or HTTPS proxy with exit 4, as it refuses `http` and `https` ones.

## Context

- ADR-0056, rules 1, 5 and 6; ADR-0053 for the guard and its message.
- `TransferProxySelection.NeedsTunnelNotBuiltYet` covers only `http`/`https` today; once handlers read `Proxy`, `curl --socks5 h dict://x/` would reach `TcpConnector` and throw `NotSupportedException`.
- `-U` applies to `Proxy` as to `ForwardProxy`; both come from the one selection.

## Acceptance criteria

- [x] A test shows `-x http://127.0.0.1:1 dict://example.com/d:x` produces a context whose `Proxy` is `127.0.0.1:1`, kind `Http`.
- [x] A test shows a `file://` transfer with `-x` gets `Proxy == null`.
- [x] A test shows `--socks5 127.0.0.1:1 dict://example.com/d:x` ends with exit 4 and ADR-0053's `Unsupported proxy` message, with nothing connected.
- [x] ADR-0053's decision no longer says other schemes are not refused; it points to ADR-0056.

## Notes

- Added `Curl.Console.UnitTests` to `touches`: the acceptance tests live there, and no task in
  Doing names it (BL-299 touches Core/Http, BL-313 touches Abstractions).
- Measured curl 8.21.0 (mingw Schannel), 2026-09-27: `curl -sS -x foo://h:1 file:///C:/Windows/win.ini`
  prints the file and exits 0. A `file` transfer ignores the proxy outright, even text curl
  cannot use, so `TransferProxySelection.TrySelect` now skips selection for `file` and returns no
  proxy (before, Curl exited 7 `Unsupported proxy scheme`). That is ADR-0056 rule 5; no new ADR.
- Default taken: the guard refuses *every* networked non-HTTP scheme through a SOCKS or HTTPS
  proxy, `ftp` and `tftp` included, rather than special-casing ftp's HTTP forwarding (rule 3)
  or tftp's MASQUE failure (rule 4). Neither route exists yet, and exit 4 is the safe answer
  until BL-213/BL-266/BL-328 land; the tasks that build those routes narrow it.
- `TransferContextFactory` sets `TransferContext.Proxy` from the same `proxy` argument it gives
  `HttpRequestOptions.ForwardProxy`, so the two cannot disagree (tested with `-U`).
- Tests: `CurlCommandRunnerProxyContextTests` (dict gets `127.0.0.1:1`/Http; `-U` credential;
  file gets null for `127.0.0.1:1` and `foo://127.0.0.1:1`), and new DataRows in
  `CurlCompositionProxyTests.RunAsync_TunnelTheConnectorCannotOpenYet_ExitsFourWithoutConnecting`
  (`--socks5 127.0.0.1:1 dict://example.com/d:x`, https proxy + gopher). The old
  `RunAsync_SocksProxyForAnotherScheme_ConnectsDirectly` became
  `RunAsync_SocksProxyEnvironmentVariableForAnotherScheme_ExitsFourWithoutConnecting`.

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. Every non-file transfer carries the selected proxy on ITransferContext.Proxy, and any scheme through a SOCKS or HTTPS proxy it cannot tunnel through ends with exit 4
