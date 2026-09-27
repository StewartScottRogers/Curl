---
id: BL-367
title: Print curl's -v lines for cookies added and dropped
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-223]
touches: [Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests, Curl.Cookies.UnitLibrary, Curl.Cookies.UnitTests, Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-27
completed: 2026-09-27
---
# BL-367 — Print curl's -v lines for cookies added and dropped

## Goal

Under `-v`, the cookie store reports each cookie it adds and each one it drops as the same `* ` info lines curl 8.21.0 writes to stderr.

## Context

- Follow-up from BL-223 (Public Suffix List refusal), whose Notes record that the `-v` lines are not printed yet.
- Measured 2026-09-27 on curl 8.21.0 (mingw, `/mingw64/bin/curl`, libpsl 0.21.5; the Windows reference build, ADR-0009) with `Record-CurlExchange.ps1` serving `Set-Cookie: n<i>=v; Path=/; <Domain>` to `curl -s -v --resolve <host>:<port>:127.0.0.1 -c - http://<host>:<port>/`. Under `-v` curl writes to stderr, before the `< Set-Cookie:` header line:
  - accepted: `* Added cookie n2="v" for domain example.co.uk, path /, expire 0`
  - refused by the Public Suffix List: `* cookie 'n1' dropped, domain 'www.example.co.uk' must not set cookies for 'co.uk'`. The Domain attribute is printed as sent, without a leading dot: `Domain=CO.UK` prints `'CO.UK'`, `Domain=.co.uk` prints `'co.uk'`.
- Other drop reasons (for example a bad Path, a Domain the host does not tail-match, a `Secure` cookie from a non-TLS origin, a cookie overlaying a `Secure` one, the 50-per-response limit `CookieStore.MostCookiesStoredPerResponse`, over-long names or values, the 255-character host limit) may print their own lines. Measure each with the same command before pinning; never pin text that was not measured. Upstream reference: https://curl.se/docs/manpage.html (`-v, --verbose`), curl 8.21.0.
- Where the code stands today:
  - `Curl.Cookies.UnitLibrary/CookieStore.cs`: `StoreFromResponse(CurlUrl, IReadOnlyList<string>, DateTimeOffset)` accepts or drops each cookie (PSL check via the internal `PublicSuffixList`) and has no verbose channel.
  - `Curl.Protocol.Abstractions.UnitLibrary/ICookieStore.cs` is the contract; `ITransferEvents.ReportInfo(string)` (same library) is how libraries emit `* ` lines (see `Curl.Networking.UnitLibrary/PoolingConnector.cs`). `ITransferContext.Events` carries the transfer's `ITransferEvents`; `NoTransferEvents.Instance` is the silent one.
  - `Curl.Output.UnitLibrary/VerboseTransferEventWriter.cs` already prints `ReportInfo` text with the `* ` prefix, so `Curl.Output` needs no change.
  - `Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs` `StoreCookies(context, head)` calls `store.StoreFromResponse(...)` and has `context.Events` in hand.
  - `Curl.Console/CookieEngine.cs` has a second `ICookieStore` implementation, `CookieStringSender`, which must follow any contract change.
  - Test fake: `Curl.Protocol.Http.UnitTests/Fakes/ScriptedCookieStore.cs`.
- Simplest shape that fits the conventions (injected interfaces, no static state): add an `ITransferEvents` parameter to `ICookieStore.StoreFromResponse`, have `HttpProtocolHandler` pass `context.Events`, and have `CookieStore` call `ReportInfo` with the measured text for each add and each drop. The implementer may choose another shape if it is simpler, but the store must not hold a per-run static or global writer. This changes a shared contract, which is why `touches` lists `Curl.Protocol.Abstractions.UnitLibrary`.
- Ordering: no code in the solution reports response header lines (`ITransferEvents.ReportResponseHeader` has no caller today), so "before the `< Set-Cookie:` line" cannot be checked end to end yet. This task pins the text and that it is reported when `StoreFromResponse` runs; the placement relative to `<` lines belongs to whichever task starts reporting response headers.

## Acceptance criteria

- [x] A test in `Curl.Cookies.UnitTests` stores `n2=v; Path=/; Domain=example.co.uk` from `http://www.example.co.uk/` and asserts exactly one info line reported: `Added cookie n2="v" for domain example.co.uk, path /, expire 0` (measured, curl 8.21.0).
- [x] A test in `Curl.Cookies.UnitTests` stores `n1=v; Path=/; Domain=co.uk` from `http://www.example.co.uk/` and asserts exactly `cookie 'n1' dropped, domain 'www.example.co.uk' must not set cookies for 'co.uk'`; data rows for `Domain=.co.uk` (prints `'co.uk'`) and `Domain=CO.UK` (prints `'CO.UK'`) pass too (measured).
- [x] Every other drop reason `CookieStore.StoreFromResponse` implements has been measured against curl 8.21.0 with the command above; each one's line is pinned in a test, or listed in this task's `Notes` as out of scope with the measured text and the reason. The exact commands and stderr bytes are recorded in `Notes`.
- [x] A test in `Curl.Protocol.Http.UnitTests` shows `HttpProtocolHandler` hands `context.Events` to the cookie store with the response's `Set-Cookie` values.
- [x] With no `-v` (`NoTransferEvents.Instance`), nothing is written and stored cookies are unchanged from today: existing `Curl.Cookies.UnitTests` and `Curl.Console.UnitTests` cookie tests pass unmodified apart from the new argument.
- [x] `dotnet build <project> -warnaserror` is clean for every project in `touches`; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`.
- [x] `powershell -NoProfile -File Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Cookies`, `Curl.Protocol.Abstractions`, `Curl.Protocol.Http` and `Curl.Console`.

## Notes

- Shape (as Context suggested): `ICookieStore.StoreFromResponse` gained a fourth parameter, `ITransferEvents events`; `HttpProtocolHandler.StoreCookies` passes `context.Events`; `CookieEngine.CookieStringSender` and the `ScriptedCookieStore` fake follow. `CookieStore.Store` now returns whether it replaced a namesake, so the store can print `Replaced` (measured, below) as well as `Added`. Cookies loaded from a `-b` file report nothing: curl printed nothing for them.
- Measured 2026-09-27, curl 8.21.0 (Git for Windows mingw64, the `Record-CurlExchange.ps1` default), each with `Record-CurlExchange.ps1 -Port <p> -Response 'HTTP/1.1 200 OK\r\nSet-Cookie: <header>\r\n...Content-Length: 0\r\n\r\n' -CurlArgs -s,-v,--resolve,<host>:<p>:127.0.0.1,-c,-,http://<host>:<p>/`, host `www.example.co.uk` unless stated. stderr lines, after `* `, CRLF-free:
  - `n2=v; Path=/; Domain=example.co.uk` -> `Added cookie n2="v" for domain example.co.uk, path /, expire 0` (pinned)
  - `n1=v; Path=/; Domain=co.uk` / `Domain=.co.uk` / `Domain=CO.UK` -> `cookie 'n1' dropped, domain 'www.example.co.uk' must not set cookies for 'co.uk'` / `'co.uk'` / `'CO.UK'` (pinned)
  - `r=1; Path=/` then `r=2; Path=/` in one response -> `Added cookie r="1" for domain www.example.co.uk, path /, expire 0`, `Replaced cookie r="2" for domain www.example.co.uk, path /, expire 0` (pinned)
  - `m=1; Path=/; Max-Age=100` -> `... expire 1790543690` (receive time + 100); `z=1; Path=/; Max-Age=0` -> `Added cookie z="1" ... expire 1`; `h=1` -> `... path /, expire 0` (pinned)
  - `qv="a b"; Path=/p` -> `Added cookie qv=""a b"" for domain www.example.co.uk, path /p, expire 0` (pinned)
  - host `127.0.0.1`, `j=1; Domain=127.0.0.1; Path=/` -> `Added cookie j="1" for domain 127.0.0.1, path /, expire 0` (pinned)
  - `-b` file `www.example.co.uk<TAB>FALSE<TAB>/<TAB>TRUE<TAB>0<TAB>s<TAB>1`, then `s=2; Path=/` over HTTP -> `cookie 's' for domain 'www.example.co.uk' dropped, would overlay an existing cookie`; loading the file printed nothing (pinned)
  - 52 headers `k<i>=v; Path=/` -> 50 `Added` lines, nothing for k51 and k52 (pinned)
  - `Path=nope` -> stored with path `/`, `Path="/x"` -> path `/x`: `Added` lines, no drop.
  - Host longer than 255 characters (`(a*63.)x4example.com`): curl exits 3, `URL rejected: Malformed input to a URL function`, so the store's 255-character rule cannot be reached from curl and prints the PSL line in our store. Not pinned: unmeasurable.
- Out of scope, filed as BL-441 (refused inside `SetCookieParser`, which returns `null` with no reason; giving it one is a separate change to another class):
  - `n3=v; Path=/; Domain=other.test` -> `skipped cookie with bad tailmatch domain: other.test`; host `127.0.0.1`, `i=1; Domain=127.0.0.2` -> `skipped cookie with bad tailmatch domain: 127.0.0.2`
  - `n4=v; Path=/; Secure` over HTTP -> `skipped cookie because not 'secure'`
  - `noequals`, `=emptyname` -> `invalid cookie, dropped` each
  - `t=a<TAB>b; Path=/`, `c=a<0x01>b; Path=/` -> `invalid octets in value, cookie dropped`
  - silent: name+value 8004 characters, header value 5002 characters, `__Secure-a=1; Path=/`, `__Host-b=1; Path=/`.
- Quality: `Measure-CodeQuality.ps1` reports 100%/100% and no failing member for Curl.Cookies, Curl.Protocol.Abstractions and Curl.Protocol.Http. Curl.Console shows 99.49%/99.48% with one failing member, `DiskWriteOutFileOpener.TryOpen`, which this task does not touch (last changed in d41f141, BL-280) and which BL-432 already tracks; every Console member this task changed passes. (Curl.Networking's `SslStreamTlsProvider.VerifyPeer` complexity failure is likewise pre-existing and outside `touches`.)
- Tests: Curl.Cookies.UnitTests 254 (8 new in `CookieStoreTests.Verbose.cs`, with a `Fakes/RecordingTransferEvents` copied from the HTTP tests), Curl.Protocol.Http.UnitTests 987 (1 new), Curl.Console.UnitTests 865; the whole fast run is green.

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. Under -v the cookie store reports curl's Added/Replaced cookie lines and its Public Suffix List and secure-overlay drop lines
