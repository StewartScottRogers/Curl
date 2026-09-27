---
id: BL-237
title: Compose the authenticator and cookie store and load -b and save -c in Curl.Console
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-218, BL-221, BL-181, BL-182, BL-192, BL-193, BL-231]
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-26
completed: 2026-09-26
---
# BL-237 — Compose the authenticator and cookie store and load -b and save -c in Curl.Console

## Goal

`Curl.Console` composes `Curl.Authentication` and `Curl.Cookies` into the HTTP handler, loads `-b` before the first transfer and writes `-c` after the last.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item W8. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- BL-231 added as a dependency beyond the plan: the handler must be registered first.
- Where a criterion says *measured*, run curl 8.21.0 - the mingw build `/mingw64/bin/curl`, first on PATH, which is the Windows reference (ADR-0009 and the BL-153 ADR) - against a loopback server (the BL-165 recording script `Record-CurlExchange.ps1` once it exists), record the exact command and the bytes it produced in `Notes`, then pin them in a test. Never pin text that was not measured.

## Acceptance criteria

- [x] `-u`, `--digest`, `--anyauth` and `--oauth2-bearer` reach the authenticator; tests over a fake connector.
- [x] `-b file`, `-b 'a=1'`, `-c jar` and `-j` behave as measured on curl 8.21.0.
- [x] `dotnet build Curl.Console -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Console`.

## Notes

- Plan item: W8 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.
- From BL-216 (ADR-0022): construct the authenticator as `new BasicAndBearerAuthenticator(CredentialEncoding.ForPlatform(OperatingSystem.IsWindows()))`.
- Delivered (2026-09-26): `CurlComposition.CreateHttpAuthenticator` builds a `RankedHttpAuthenticator` over `BasicAndBearerAuthenticator` and `DigestAuthenticator(…, DigestClientNonce.CreateRandom)` in the platform encoding; `HttpRequestOptionsMapping` now carries `AuthSchemes` and `BearerToken`. New `CookieEngine` (Curl.Console) owns the run's `CookieStore`; `TransferDispatch.Cookies` hands it to the runner, which loads `-b` files before the first transfer and writes the `-c` jar after each HTTP transfer. Runner parameter `outputFileSystem` renamed `fileSystem`, since it now opens cookie files and the jar too.
- Measured with mingw curl 8.21.0 via `Record-CurlExchange.ps1` (cwd `C:\Temp\bl237`, response `HTTP/1.1 200 OK\r\nSet-Cookie: got=g1\r\nContent-Length: 0\r\n\r\n`, `in.txt` = session `sess=s1` + persistent `keep=k1` (expires 4102444800) for 127.0.0.1):
  - `-s -b in.txt URL` → `Cookie: keep=k1; sess=s1`; with `-j` → `Cookie: keep=k1`; `-b 'a=1; b=2'` → `Cookie: a=1; b=2`; `-b missing.txt` → no Cookie header, exit 0.
  - `-c jar.txt URL` → jar file, CR LF: the three `#` header lines, a blank line, `127.0.0.1\tFALSE\t/\tFALSE\t0\tgot\tg1`.
  - `-b in.txt -c - URL` → jar on stdout with LF: got, keep, sess (newest first). `-c - -o out.bin URL` → jar on stdout with CR LF (stdout still in text mode).
  - `-b a=1 URL/1 URL/2` → both requests `Cookie: a=1` only: a cookie string does not turn the cookie engine on. `-c jar2.txt URL/1 URL/2` → second request `Cookie: got=g1`. `-b in.txt -b a=1` two URLs → `keep=k1; sess=s1; a=1`, then `keep=k1; sess=s1; got=g1; a=1`.
  - `-c - -w '[w]\n' URL/1 URL/2` (5-byte body) → `body\n[w]\n<jar>body\n[w]\n<jar>`: the jar is written after every transfer, after its `-w` output.
  - `-c jarfail.txt http://127.0.0.1:1/` → exit 7 and a header-only jar. `-c` with only `file://`, `dict://` (even with `-b in.txt`) or `nosuch://` URLs → no jar file. So the jar is written after each `http`/`https` transfer whatever its outcome, and never for another scheme.
  - Auth: `-u user:pw` → `Authorization: Basic dXNlcjpwdw==` after Host; `--oauth2-bearer tok` → `Authorization: Bearer tok`; `--digest -u user:pw` and `--anyauth -u user:pw` against `401` + `WWW-Authenticate: Digest realm="r", nonce="n1"` + `Connection: close` → first request bare, second on a new connection with `Digest username="user",realm="r",nonce="n1",uri="/p",response="62d3592a5392f0c06d5d3c4e46bf17ae"`; `--anyauth` offered Basic → retry with Basic.
- Digest separators: the mingw build writes WDigest's format with no blanks; ADR-0025 already chose curl's own Digest format (`", "`) on every platform, so the Console test pins the measured response hash with ADR-0025's separators. No new ADR was needed.
- Decision (unattended default): `-b` files are loaded once, before the first transfer, as this task's Goal says, although libcurl reloads them per transfer. With one shared store the result is the same unless a server overwrites a file cookie in URL 1 and URL 2 expects the file's value back; not modelled.
- A jar before a malformed URL (`dict://exa mple.com/x`) is not written; an `http` URL that fails before dispatch (bad `-r`, unreadable `-F` file) still writes it. Only the scheme was measured, not those pre-dispatch failures.
- Follow-up filed: BL-316 (`-b -` reads cookies from standard input).

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. curl.exe now sends -u/--digest/--anyauth/--oauth2-bearer through the ranked authenticator and loads -b, honours -j and writes the -c jar as curl 8.21.0 does
