---
id: BL-461
title: Print curl's -v refusal lines for Set-Cookie lines in a -b cookie file
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-443]
touches: [Curl.Cookies.UnitLibrary, Curl.Cookies.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-27
completed: 2026-09-27
---
# BL-461 — Print curl's -v refusal lines for Set-Cookie lines in a -b cookie file

## Goal

Under `-v`, loading a `-b` cookie file reports each `Set-Cookie:` line curl 8.21.0 refuses with the `* ` line curl prints for it, as `CookieStore.StoreFromResponse` does for a received header since BL-443.

## Context

- Found in BL-443. Measured 2026-09-27 on curl 8.21.0 (mingw) with `Record-CurlExchange.ps1`: `curl -s -v -b cf.txt -c - http://127.0.0.1:<port>/`, where `cf.txt` held `Set-Cookie: f=v; Pa<TAB>th=/; X=<0x01>` and `Set-Cookie: g=v; X=<0x01>` (LF line endings). curl printed `* invalid octets in value, cookie dropped` once (for `g`) and sent `Cookie: f=v`.
- `SetCookieParser.Parse(headerValue, requestUrl, now, out refusal)` already gives the line for a received header; `ParseFromCookieFile` discards it. `CookieStore.LoadCookieFile` / `LoadCookieFileAsync` take no `ITransferEvents`; `Curl.Console\CookieEngine.cs` calls them.
- Before pinning, measure which refusals print for a file line (a file line has no request, so `Secure` and `Domain` never refuse), and whether a refused Netscape tab-separated line prints anything.

## Acceptance criteria

- [x] A test in `Curl.Cookies.UnitTests` asserts loading a file with `Set-Cookie: g=v; X=<0x01>` reports exactly `invalid octets in value, cookie dropped` to a recording `ITransferEvents`, and one with a clean file reports nothing.
- [x] The measurements in Context are recorded in this task's Notes with command and stderr bytes, and each is pinned in a test.
- [x] `Curl.Console` passes its transfer events to the cookie file load; a `Curl.Console.UnitTests` test shows the `* ` line on stderr under `-v`.
- [x] `dotnet test --filter "TestCategory!=Integration"` passes; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Cookies` and `Curl.Console`.

## Notes

### Measurements (curl 8.21.0 mingw, Schannel, 2026-09-27)

Every run: `Record-CurlExchange.ps1 -Port <p> -CurlArgs -s,-v,-b,<file>,-c,-,http://127.0.0.1:<p>/`, file lines ending
in LF, written as Latin-1. Standard error lines below are the ones before `*   Trying`, each ending CR LF (Windows
text mode); "silent" means none.

Mixed file (in `CookieStoreTests.RefusingCookieFile`): `Set-Cookie: a=v; X=<01>`, `Set-Cookie: b<01>x=v`,
`Set-Cookie: c`, `Set-Cookie: s=v; Secure`, `Set-Cookie: d=v; Domain=example.com`, `Set-Cookie: =v`, tab line with
`n<01>` as name, tab line with `v<01>` as value, `bad line`, `Set-Cookie: e=v; X=<01>`. Stderr:

```
* invalid octets in value, cookie dropped\r\n
* invalid octets in name, cookie dropped\r\n
* invalid cookie, dropped\r\n
* invalid octets in value, cookie dropped\r\n
```

Sent `Cookie: s=v`; jar held `.example.com TRUE / FALSE 0 d v`. So `Secure` and `Domain` never refuse a file line,
and a refused tab-separated line (or a bad line) prints nothing.

One `Set-Cookie:` line per file:

| Line after `Set-Cookie:` | Stderr | Sent |
| --- | --- | --- |
| ` c;a=b` | `* invalid cookie, dropped` | - |
| ` ; ;a=b` | `* invalid cookie, dropped` | - |
| ` ; =v` | `* invalid cookie, dropped` | - |
| ` <01>=v` | `* invalid octets in name, cookie dropped` | - |
| ` =v`, `=v`, `  =v`, ` <TAB>=v`, ` =`, ` =v;`, ` =v; X=<01>` | silent | - |
| ` ;=x;a=b`, ` =x;a=b`, ` ;;`, empty, blanks only | silent | - |
| ` ;a=b`, ` ;a=b; Secure`, `  ; a=b`, ` ;a=b;=x;Path=/q` | silent | `Cookie: a=b` |
| ` c=` | silent | `Cookie: c=` |
| ` a=b;=x; Path=/q`, ` a=b;=x; Y=<01>` | silent | `Cookie: a=b` (path not applied) |
| ` a=b;;Path=/q`, ` a=b; =x; Path=/q` | silent | nothing to `/` (path `/q` applied) |

The same headers served in a response (`-Response "HTTP/1.1 200 OK\r\nSet-Cookie: <h>\r\n..."`, `curl -s -v -c -`):
`=v; X=<01>`, `;a=b`, `=`, ` =v`, `=v;`, `<TAB>=v`, `;=x;a=b`, `=x;a=b`, `;;`, `; ;a=b`, `c;a=b` all printed
`* invalid cookie, dropped`; `a=b;=x; Path=/q` and `a=b;=x; Y=<01>` stored `a=b` with path `/`; `a=b;;Path=/q` and
`a=b; =x; Path=/q` stored path `/q`.

### What the measurements mean, and what changed

- The rule (both sources): a part whose name is empty before trimming - it starts at `;`, tab, `=` or the end - is
  skipped, and reading stops there unless `;` follows at once. A response's first part is the exception: refused as
  `invalid cookie, dropped` (as before). A file line skips it too, so `;a=b` is the cookie `a=b` and `=v` is dropped
  silently. `SetCookieParser.TryReadParts` now does this; before, `=x` after the cookie was read as an attribute and
  reading went on, which was wrong for received headers too (`a=b;=x; Path=/q` got path `/q`). Fixing it was the
  same change in the same method, so it is here, pinned by `SetCookieParserTests.Parse_NamelessPart_EndsTheReadingAsCurlDid`.
- New overloads, the old ones delegating with `NoTransferEvents.Instance`: `SetCookieParser.ParseFromCookieFile(..., out
  string? refusal)`, `NetscapeCookieFile.ParseLine(..., out string? refusal)`, `NetscapeCookieFile.Read(reader, now,
  ITransferEvents)`, `CookieStore.LoadCookieFile(..., ITransferEvents)` and `LoadCookieFileAsync(..., ITransferEvents,
  CancellationToken)`. `CookieEngine.LoadCookieFilesAsync` takes the run's events and `CurlCommandRunner` passes
  `transferEventOutput.Events`, which is open before the first transfer.
- No ADR: nothing here was a choice; every behaviour is the measured one.
- Pinned in `CookieStoreTests.CookieFileRefusals.cs`, `SetCookieParserTests` and
  `CurlCommandRunnerCookieTests.RunAsync_VerboseCookieFileWithRefusedSetCookieLines_PrintsCurlsRefusalLines`.
- Also measured: a `-b` file that cannot be opened prints `* WARNING: failed to open cookie file "<path>"` under `-v`,
  which Curl does not print yet. Filed as BL-483.

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. Under -v, loading a -b file prints curl's refusal line for each refused Set-Cookie: line; nameless cookie parts parse as curl 8.21.0 does
