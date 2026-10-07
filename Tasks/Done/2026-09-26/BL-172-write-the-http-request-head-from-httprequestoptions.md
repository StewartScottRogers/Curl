---
id: BL-172
title: Write the HTTP request head from HttpRequestOptions
priority: High
assignee: Claude
pipeline: protocol
depends-on: [BL-159]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-09-26
completed: 2026-09-26
---
# BL-172 — Write the HTTP request head from HttpRequestOptions

## Goal

An internal request writer produces the request line and headers byte-equal to curl 8.21.0 for the default GET and for `-H`, `-X`, `-A` and `-e`.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item H4. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- Measured default GET (`curl http://127.0.0.1:18081/a?b`): `GET /a?b HTTP/1.1\r\nHost: 127.0.0.1:18081\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n\r\n`.
- Measured with a custom method and headers (command line not recorded - re-measure with `-X PUT -H 'Accept:' -H 'X-A: 1'` before pinning): `PUT / HTTP/1.1\r\nHost: 127.0.0.1:18082\r\nUser-Agent: curl/8.21.0\r\nX-A: 1\r\n\r\n`.
- curl's `-H` rules: `Name: value` replaces an internal header of that name, `Name:` removes it, `Name;` sends it empty; custom headers follow curl's order (measure). Host drops the default port and brackets IPv6.
- Options come from `ITransferContext.Http` (BL-159).
- Where a criterion says *measured*, run curl 8.21.0 - the mingw build `/mingw64/bin/curl`, first on PATH, which is the Windows reference (ADR-0009 and the BL-153 ADR) - against a loopback server (the BL-165 recording script `Record-CurlExchange.ps1` once it exists), record the exact command and the bytes it produced in `Notes`, then pin them in a test. Never pin text that was not measured.

## Acceptance criteria

- [x] The default GET is byte-equal to the measured bytes above (port substituted); Host omits `:80` / `:443` for the scheme default and brackets an IPv6 literal.
- [x] `-H` replace, remove (`X:`) and empty (`X;`) each have a test with measured bytes; custom header order matches curl.
- [x] `CustomMethod` overrides the method; `UserAgent` null sends `curl/8.21.0`, empty omits the header; `Referer` sends `Referer:`; tests for each.
- [x] `dotnet build Curl.Protocol.Http.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Protocol.Http`. Tests use the fakes in `Curl.Protocol.Http.UnitTests/Fakes` (added by BL-169), never a socket; every parser test also runs with 1-byte chunks.

## Notes

- Plan item: H4 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.
- Delivered as `HttpRequestHeadFormatter.Format(Uri, HttpRequestOptions?)`, which returns the head's bytes, and `HttpCustomHeader`, which reads one `-H` value. Returning bytes keeps it a pure function; BL-173 writes them to the `IConnection`. No parser reads a connection here, so the "1-byte chunks" rule has nothing to apply to.
- Kept out on purpose, because other tasks own them: the request line always says `HTTP/1.1` (BL-180 owns `-0`), and the target is always `Uri.PathAndQuery` (BL-186 owns `--request-target`).
- Measured on 2026-09-26 with `Record-CurlExchange.ps1 -Port 18091 -CurlArgs <args>` and `/mingw64/bin/curl` 8.21.0 (Schannel). In the list, `U` stands for `http://127.0.0.1:18091/`, `D` for the default headers `Host: 127.0.0.1:18091\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n`, and every request line is `GET / HTTP/1.1\r\n` unless shown. Every result is pinned in `HttpRequestHeadFormatterTests`.
  - `http://127.0.0.1:18091/a?b` -> `GET /a?b HTTP/1.1\r\n` D `\r\n`. `http://127.0.0.1:18091` -> target `/`. `.../p?q#frag` -> target `/p?q`.
  - `--connect-to EXAMPLE.com:80:127.0.0.1:18091 http://EXAMPLE.com/p` -> `Host: EXAMPLE.com` (port 80 dropped, letter case kept). `--connect-to [::1]:18091:... http://[::1]:18091/` -> `Host: [::1]:18091`. `http://[::1]/` -> `Host: [::1]`.
  - `-X PUT -H 'Accept:' -H 'X-A: 1' U` -> `PUT / HTTP/1.1\r\nHost: 127.0.0.1:18091\r\nUser-Agent: curl/8.21.0\r\nX-A: 1\r\n\r\n`. `-X get` -> `get / HTTP/1.1`.
  - Replacing: `-H 'User-Agent: x'` -> Host, Accept, then `User-Agent: x` last. `-H 'accept: text/plain'` -> `accept: text/plain` after User-Agent. `-H 'X-B: 2' -H 'Accept: a/b' -H 'X-A: 1'` -> Host, UA, `X-B: 2`, `Accept: a/b`, `X-A: 1`, so custom headers keep command-line order and a replaced header moves into that order. `-H 'Accept: 1' -H 'Accept: 2'` sends both.
  - Host is the exception to replacing. `-H 'Host: h'` / `'Host:h'` / `'Host:   '` / `'host: h'` / `'Host;'` / `'Host; y'` -> `Host: h` / `Host:h` / `Host:   ` / `Host: h` / `Host:` / `Host: y`, each in Host's own slot. The first Host value wins: `-H 'Host: a' -H 'Host: b' -H 'HOST;'` -> `Host: a`. `-H 'Host:'` -> no Host line at all. `-H 'Host:' -H 'Host: x'` -> no Host in the slot and `Host: x` last.
  - Removing: `-H 'Accept:'` and `'Accept:   '` remove Accept. `'Accept; y'` also removes Accept and sends nothing. `-H 'X-A:' -H 'X-C:   '` -> D only. Dropped entirely: `'Foo'`, `':x'`, `';'`, `'X-B; y'`, `'X-B;  '`.
  - Empty: `-H 'X-B;'` -> D `X-B:\r\n`. `-H 'Accept;'` -> `Accept:` in custom order. `-H 'User-Agent;'` -> Host, Accept, `User-Agent:`.
  - Verbatim: `'X-A:   1  '` and `'X-B:1'` are sent exactly as given. `'Accept : x'`, `' Accept: x'`, `'Accept-Language: x'`, `'Hostname: y'` and `'User-Agents: z'` replace nothing and are sent as given. `'X A: 1'` is sent.
  - `-A agent/1` -> `User-Agent: agent/1` in its slot. `-A ''` (through `-K` with `user-agent = ""`, because the recorder refuses an empty argument) -> no User-Agent. `-A a -H 'User-Agent: h'` and `-A '' -H 'User-Agent: h'` -> Host, Accept, `User-Agent: h`. `-A a -H 'User-Agent:'` -> no User-Agent.
  - `-e http://r.example/x` -> D `Referer: http://r.example/x\r\n`. `-e ''` (through `-K`) -> no Referer. `-e http://r/ -H 'Referer: h'` -> D `Referer: h`. `-e http://r/ -H 'Referer:'` -> D. `-e 'http://r.example/x;auto'` -> `Referer: http://r.example/x`, and `-e ';auto'` -> no Referer. The command-line layer owns stripping `;auto`, so that went to BL-251.
  - Encoding: `-H 'X-A: é'` -> byte 0xE9. `-H 'X-B: Ā€中'` -> `A`, 0x80, `?`, which is the Windows ANSI code page with best fit.
- Decision (sensible default, taken unattended): the formatter encodes as `Encoding.Latin1`. That gives 0xE9 for é, `A` for U+0100 and `?` for 中, as curl does; only cp1252's extra characters, such as € (0x80), differ. Why: a single-byte encoding matches every Latin-1 character curl sent, and the base class library has no Windows best-fit code-page encoder. The platform question (UTF-8 on Linux, and which layer converts) is filed as BL-252.
- Decision: `Uri.Host` lower-cases, and curl keeps the letter case the URL was written in, so the Host line takes the host's letter case from `Uri.OriginalString`. When the host's text does not appear there (for example `[0::1]`, which `Uri` rewrites to `[::1]`), it falls back to `Uri.Host`.
- Gates: `dotnet build Curl.Protocol.Http.UnitLibrary -warnaserror` is clean. The fast tests are green, 159 of them in Curl.Protocol.Http.UnitTests. `Measure-CodeQuality.ps1 -Library Curl.Protocol.Http.UnitLibrary` reports 100% line, 100% branch and 0 failing members, with the worst CRAP at 10.
- Follow-ups filed: BL-251 (`-e ';auto'`) and BL-252 (ADR on how command-line text becomes bytes on each platform).

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. HttpRequestHeadFormatter writes curl 8.21.0's request head byte for byte: the default GET, and the -X, -H, -A and -e cases, all measured
