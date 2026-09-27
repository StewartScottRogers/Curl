---
id: BL-273
title: Read Set-Cookie header lines in cookie files
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-221]
touches: [Curl.Cookies.UnitLibrary, Curl.Cookies.UnitTests]
requirement: none
created: 2026-09-26
completed: 2026-09-26
---
# BL-273 — Read Set-Cookie header lines in cookie files

## Goal

`NetscapeCookieFile` reads a `Set-Cookie:` header line in a `-b` file the way curl 8.21.0 does, instead of refusing it as a malformed line.

## Context

- Found in BL-221 (2026-09-26). curl's `cookie_load` treats a line starting `Set-Cookie:` as an HTTP header and hands it to the header parser with no request host; BL-221's `NetscapeCookieFile.ParseLine` refuses it today (it has too few tab fields).
- Start at `Curl.Cookies.UnitLibrary/NetscapeCookieFile.cs` and `SetCookieParser.cs`. Upstream: https://curl.se/docs/http-cookies.html, curl 8.21.0.
- Measure first: run curl 8.21.0 (`/mingw64/bin/curl`) with `-b` on a file mixing Netscape lines and `Set-Cookie:` lines (with and without `Domain`, `Path`, `Expires`, in varying case of the prefix), against `Record-CurlExchange.ps1`, and record the `Cookie` header sent and the `-c` jar written.

## Acceptance criteria

- [x] A `NetscapeCookieFileTests` test loads a file with `Set-Cookie:` lines and matches the measured `-c` jar byte for byte.
- [x] A test pins the measured `Cookie` header curl sends for such a file.
- [x] `dotnet build Curl.Cookies.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Cookies`.

## Notes

- Measured 2026-09-26 with curl 8.21.0 (mingw64, Schannel) and `Record-CurlExchange.ps1` on port 18273:
  `curl -b inN.txt -c jarN.txt http://127.0.0.1:18273/` (in2 against `/x/y`; in5/in6 twice against
  `http://foo.test:18273/` with `--resolve foo.test:18273:127.0.0.1`, the response setting `s=2` and
  `t=2`; in7 against `bar.test`). Each file and the jar and `Cookie` header it produced are pinned in
  `NetscapeCookieFileTests` (the `BL-273` tests).
- What curl does: a line starting `Set-Cookie:` in any case (not `Set-Cookie2:`, not with a leading
  space) is a header with no request, blanks after the colon skipped. `Secure` is always accepted;
  any `Domain` is accepted and includes subdomains, even an IP address; `Expires` is capped at 400
  days and `Max-Age` counts from the load; expired ones are dropped; `__Host-` needs `Path=/`
  and no `Domain`. Without `Domain` the cookie has no domain: it goes to every host, sorts as an empty
  domain and is never written to the jar. Without `Path` it has no path: it matches every path,
  sorts before `/`, is not the namesake of a `/` cookie, and is written as `/`. A `Secure` cookie
  without a domain or a path never stops an insecure origin setting its name.
- Model (my choice, no ADR: it is measured behaviour, and BL-303 in Doing holds
  `Documentation/Planning/Decisions`): `Cookie.Domain` became `string?`, `null` meaning curl's
  `co->domain == NULL`, because a Netscape line can already carry an empty domain that curl does
  write. A missing path is the empty string, which a Netscape line or `Path` attribute never
  produces (both are sanitized to start with `/`). `NetscapeCookieFile.Read` and `ParseLine` now take
  the load time for `Max-Age`; `SetCookieParser.ParseFromCookieFile` reads the header without a request.
- Gates: `dotnet build` clean, fast tests 0 failures (Curl.Cookies 288), `Measure-CodeQuality.ps1
  -Library Curl.Cookies.UnitLibrary`: 100% line, 100% branch, 0 failing members, worst CRAP 10.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. Set-Cookie: lines in -b cookie files load as curl 8.21.0 loads them, pinned to measured jars and Cookie headers
