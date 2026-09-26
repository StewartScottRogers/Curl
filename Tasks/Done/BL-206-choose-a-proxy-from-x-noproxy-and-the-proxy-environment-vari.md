---
id: BL-206
title: Choose a proxy from -x, --noproxy and the proxy environment variables
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-162]
touches: [Curl.Core.UnitLibrary, Curl.Core.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-26
completed: 2026-09-26
---
# BL-206 — Choose a proxy from -x, --noproxy and the proxy environment variables

## Goal

A proxy selector returns the `ProxyEndpoint` curl would use for a URL, from `-x`, `--noproxy` and injected environment variables.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item K4. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- curl reads `http_proxy` lowercase only, `HTTPS_PROXY`/`https_proxy`, `ALL_PROXY`, `NO_PROXY` (https://curl.se/docs/manpage.html#ENVIRONMENT).
- Where a criterion says *measured*, run curl 8.21.0 - the mingw build `/mingw64/bin/curl`, first on PATH, which is the Windows reference (ADR-0009 and the BL-153 ADR) - against a loopback server (the BL-165 recording script `Record-CurlExchange.ps1` once it exists), record the exact command and the bytes it produced in `Notes`, then pin them in a test. Never pin text that was not measured.

## Acceptance criteria

- [x] Case rules, `NO_PROXY` wildcards and CIDR are measured on curl 8.21.0 and pinned.
- [x] The environment is injected; no test reads the real environment.
- [x] `dotnet build Curl.Core.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Core`.

## Notes

- Plan item: K4 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.
- Touches widened to `Documentation/Planning/Decisions` for ADR-0023 (the decision record the delegation rule requires); no task in `Doing` named it.
- Delivered: `ProxySelector` (order of `-x`, `--noproxy`, the variables), `NoProxyMatcher` (`Curl_check_noproxy`), `ProxyUrlParser` (proxy text to `ProxyEndpoint` or curl's exit 5/7 failure), all in `Curl.Core`. Decisions in ADR-0023. Wiring, `-U`, `--proxy1.0` and the SOCKS options stay with BL-238.
- Measurement harness (curl 8.21.0 mingw, 2026-09-26), no server needed: `env -u http_proxy -u HTTP_PROXY -u https_proxy -u HTTPS_PROXY -u all_proxy -u ALL_PROXY -u no_proxy -u NO_PROXY <vars> curl -sv --connect-timeout 1 --resolve '*:2222:127.0.0.1' [-x ...] [--noproxy ...] http://<host>:2222/`, reading `Trying 127.0.0.1:1111` (proxy) or `:2222` (direct). Credentials from a Python loopback listener on 1111 printing `Proxy-Authorization` (`http://%41:%42@...` gave `Basic QTpC`, `u@` gave `Basic dTo=`, `%zz:p@` gave `Basic JXp6OnA=`).
- Measured, case rules: on Windows `HTTP_PROXY` is used as `http_proxy` (the environment ignores case); `https_proxy` for an `http://` URL is not used; `ws` falls back to `http_proxy`, `wss` to `https_proxy`; `http_proxy=` (empty) falls through to `all_proxy`; `file://` ignores `all_proxy`; `-x ""` goes direct; env `no_proxy` also exempts `-x`; `--noproxy ""` or `--noproxy b.test` hides `no_proxy=a.test`.
- Measured, `NO_PROXY`: `*` only alone (`*,foo`, `foo,*`, ` * ` do not exempt); `foo example.com` stops after `foo`; `foo	example.com` too; leading/trailing dots ignored; suffix only at a dot (`nonexample.com` not matched by `example.com`); `localhost` does not exempt `127.0.0.1`.
- Measured, CIDR: `127.0.0.0/8` exempts `127.0.0.5`; `/0` and no suffix compare the whole address (`127.0.0.0/0` does not exempt `127.0.0.5`); `/33`, `/`, `/abc`, `/8x`, `/-1`, `/+8`, `/0x8` match nothing; `/08` is 8; `127.1` and `0x7f.0.0.1` are not addresses; IPv6 `::1/128`, `fe80::/10`, `fe80::/64` match, `[::1]`, `fe80::1%eth0`, `/129` do not.
- Measured, proxy text: default ports http 80 (not 1080), https 443, socks 1080; `curl: (7) Unsupported proxy scheme for 'foo://127.0.0.1:1111'`; `curl: (5) Unsupported proxy syntax in '<text>': <reason>` for `Port number was not a decimal number between 0 and 65535`, `No host part in the URL`, `Bad IPv6 address`, `Malformed input to a URL function`, `Unsupported number of slashes following scheme`, `Bad hostname`; `-x http://localhost:0` gives `curl: (7) Failed to connect to localhost:0 over proxy localhost after 0 ms: Could not connect to server`.
- Choice: `http://@host` (empty user information) gives no credential; not measured. The IPv6 zone on a bracketed proxy host is dropped, as the measured `Trying [::1]:1111` shows.
- Gates: `dotnet build -warnaserror` clean; fast tests all pass (Curl.Core.UnitTests 371 passed, 2 skipped as before); `Measure-CodeQuality.ps1 -Library Curl.Core.UnitLibrary` reports 100% line, 100% branch, 0 failing members, worst CRAP 10.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. ProxySelector chooses the proxy from -x, --noproxy and injected proxy variables as curl 8.21.0 does, with NO_PROXY wildcards, CIDR and proxy-text failures measured and pinned
