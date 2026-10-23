---
id: BL-3266
title: Normalise %{url_effective} and reject no-host URLs as curl 8.22.0 does
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3182]
touches: [Curl.Engine.UnitLibrary/UrlEffective.cs, Curl.Protocol.Abstractions.UnitLibrary/CurlUrlParser.cs, Curl.Protocol.Abstractions.UnitLibrary/CurlUrlHost.cs, Curl.Protocol.Abstractions.UnitLibrary/CurlUrlIPv4Address.cs, Curl.Protocol.Abstractions.UnitLibrary/CurlUrlRejectionMessages.cs, Curl.Core.UnitLibrary/UrlSchemeGuesser.cs, Curl.Engine.UnitTests/UrlEffectiveNormalizationTests.cs, Curl.Protocol.Abstractions.UnitTests/CurlUrlHostPortTableTests.cs, Curl.Console.UnitTests/CurlCommandRunnerUrlRejectionTests.cs, Curl.Console/CurlCommandRunner.cs, Curl.Core.UnitTests/UrlSchemeGuesserTests.cs, Curl.Console/CLAUDE.md]
requirement: none
created: 2026-10-23
completed: 2026-10-23
---
# BL-3266 — Normalise %{url_effective} and reject no-host URLs as curl 8.22.0 does

## Goal

`%{url_effective}`, `%{url.host}` and `%{url.port}` print what curl 8.22.0 prints for odd URLs, and a URL with no host fails with curl's `URL rejected: No host present`.

## Context

Split from BL-3173 (over its cost cap). The URL model is `Curl.Protocol.Abstractions.UnitLibrary` (`CurlUrlParser.cs`, `CurlUrlHost.cs`, `CurlUrlIPv4Address.cs`, `CurlUrlRejectionMessages.cs`); the scheme guess is `Curl.Core.UnitLibrary/UrlSchemeGuesser.cs`; `Curl.Engine.UnitLibrary/UrlEffective.cs` builds `%{url_effective}`. The shared contract library is edited only in its URL files named in `touches`. The 20 measured inputs are in the Notes of BL-3173 (Deferred); copy that list into this task's Notes first.

## Acceptance criteria

- [x] Notes record curl 8.22.0's `%{url_effective}`, `%{url.host}`, `%{url.port}` and, for rejected ones, exit code and text for the 20 inputs of BL-3173's Notes.
- [x] A table-driven test in `Curl.Engine.UnitTests/UrlEffectiveNormalizationTests.cs` runs the 20 inputs through `UrlEffective.Normalize` and compares with the recorded `%{url_effective}`; a second table in `Curl.Protocol.Abstractions.UnitTests/CurlUrlHostPortTableTests.cs` compares `%{url.host}` and `%{url.port}`.
- [x] `//127.0.0.1:18080/echo` returns exit 3 with `curl: (3) URL rejected: No host present`, in a test through the command runner (`Curl.Console.UnitTests/CurlCommandRunnerUrlRejectionTests.cs`).
- [x] `http:127.0.0.1:18080/echo` and URLs with leading or trailing spaces report `%{url_effective}` as typed (their rejection text pinned as measured), and `Curl.Core.UnitTests/UrlSchemeGuesserTests.cs` has a row showing `/x` stays unchanged.
- [x] `dotnet build Curl.Engine.UnitLibrary Curl.Protocol.Abstractions.UnitLibrary Curl.Core.UnitLibrary -warnaserror` is clean, the fast tests of Curl.Engine, Curl.Protocol.Abstractions, Curl.Core and Curl.Console pass, and `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary,Curl.Core.UnitLibrary,Curl.Console` is within the gates.

## Notes

- Split from BL-3173.
- Continues the stashed work of BL-3173.
- Measured 2026-10-22 on Windows with curl 8.22.0 (mingw, Schannel; `C:\Program Files\Git\ucrt64\bin\curl.exe -sS --connect-timeout 2 -w '[%{url_effective}][%{url.host}][%{url.port}]'`), run directly against refused port 9 (no server needed, so not through Record-CurlExchange.ps1). Input -> exit, url_effective, url.host, url.port:
  1. `http://127.1:9/echo` -> 7, `http://127.0.0.1:9/echo`, `127.0.0.1`, `9`
  2. `http://0x7f.1:9/` -> 7, `http://127.0.0.1:9/`, `127.0.0.1`, `9`
  3. `http://2130706433:9/` -> 7, `http://127.0.0.1:9/`, `127.0.0.1`, `9`
  4. `http://127.0.0.1.:9/` -> 7, `http://127.0.0.1:9/`, `127.0.0.1`, `9`
  5. `http://127.0.0.1:09/` -> 7, `http://127.0.0.1:9/`, `127.0.0.1`, `9`
  6. `http://h.invalid:/echo` -> 6, `http://h.invalid/echo`, `h.invalid`, `80`
  7. `//127.0.0.1:18080/echo` -> 3 `curl: (3) URL rejected: No host present`, as typed, empty, empty (also `/127.0.0.1:9/`, `///127.0.0.1:9/`, `//:9/`; `http:////127.0.0.1:9/` is `Unsupported number of slashes following scheme`)
  8. `http:127.0.0.1:18080/echo` -> 3 `URL rejected: Port number was not a decimal number between 0 and 65535`, as typed, empty, empty
  9. ` http://127.0.0.1:9/echo` (leading space) -> 3 `URL rejected: Malformed input to a URL function`, as typed, empty, empty
  10. `http://127.0.0.1:9/echo ` (trailing space) -> same as 9
  11. `http://[::1%25lo]:9/` -> 7 `Failed to connect to ::1:9 after 9 ms: Could not connect to server`, `http://[::1%25lo]:9/`, `[::1]`, `9`
  12. `http://HOST.Invalid:9/` -> 6, as typed, `HOST.Invalid`, `9`
  13. `http://0177.0.0.1:9/` -> 7, `http://127.0.0.1:9/`, `127.0.0.1`, `9`
  14. `http://127.0.0.1:65535/` -> 7, as typed, `127.0.0.1`, `65535`
  15. `http://[0:0:0:0:0:0:0:1]:9/` -> 7, `http://[::1]:9/`, `[::1]`, `9`
  16. `http://user:pw@127.0.0.1:9/` -> 7, as typed, `127.0.0.1`, `9`
  17. `127.0.0.1:9/echo` -> 7, `http://127.0.0.1:9/echo`, `127.0.0.1`, `9`
  18. `http://127.0.0.1:9` -> 7, `http://127.0.0.1:9/`, `127.0.0.1`, `9`
  19. `http://127.0.0.1:0009/` -> 7, `http://127.0.0.1:9/`, `127.0.0.1`, `9`
  20. `http://[::ffff:127.0.0.1]:9/` -> 28 (connect timeout), as typed, `[::ffff:127.0.0.1]`, `9`
- Resumed BL-3173's stash: `UrlEffective.Normalize(url, typedUrl, pathAsIs)` rebuilds the authority (user info and host name as typed, IPv4 dotted-quad, IPv6 normalised with its typed zone id, port in decimal, an empty port dropped) and returns the typed URL for a rejected one; `CurlCommandRunner.WriteOutRequestUrl` passes the given URL, except when the transfer URL is empty (an unresolved upload URL or IPFS gateway), which still prints empty as before.
- Merged with BL-3175's empty-port check in `UrlSchemeGuesser.AddScheme`. Decision (sensible default): a URL starting with `/` gets no guessed scheme, so the parser rejects it with `No host present` as curl 8.22.0 does, except under `--proto-default file`, where it still becomes `file:///x` (libcurl's file scheme takes an empty host); rows for both in `UrlSchemeGuesserTests`.
- Touches widened by `Curl.Console/CLAUDE.md` (its `%{url_effective}` paragraph described the 8.21.0 behaviour); no Doing task names it.
- The zone-ID connect-error text left over from BL-3173 is not among this task's criteria (BL-3265 took the rest of BL-3173).
- Fast tests: Curl.Engine 2347, Curl.Protocol.Abstractions 927, Curl.Core 1830, Curl.Console 2855 passed. Measure-CodeQuality for Curl.Engine.UnitLibrary, Curl.Core.UnitLibrary and Curl.Console: Console and Core at 100/100; Engine had one member, `UrlEffective.Authority` (IPv6 address with no port), short of coverage, closed by `Normalize_IPv6AddressWithoutAPort_NormalisesTheAddressAndAddsNoPort`; worst CRAP 10. Solution build with -warnaserror clean. All tests platform-neutral (no paths, no OS text).

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: e58ff96f f6df622b 18168d82 796ab2d5 9d48b5c4.
- 2026-10-23: Doing -> Done. %{url_effective} normalises IP addresses and ports as curl 8.22.0 does, and a URL starting with / is rejected with No host present
