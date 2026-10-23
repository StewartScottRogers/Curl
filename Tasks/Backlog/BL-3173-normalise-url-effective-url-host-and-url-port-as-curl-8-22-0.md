---
id: BL-3173
title: Normalise %{url_effective}, url.host and url.port as curl 8.22.0 does and reject no-host URLs
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3182]
touches: [Curl.Engine.UnitLibrary/UrlEffective.cs, Curl.Protocol.Abstractions.UnitLibrary/CurlUrlParser.cs, Curl.Protocol.Abstractions.UnitLibrary/CurlUrlHost.cs, Curl.Protocol.Abstractions.UnitLibrary/CurlUrlIPv4Address.cs, Curl.Protocol.Abstractions.UnitLibrary/CurlUrlRejectionMessages.cs, Curl.Core.UnitLibrary/UrlSchemeGuesser.cs, Curl.Engine.UnitTests/UrlEffectiveNormalizationTests.cs, Curl.Protocol.Abstractions.UnitTests/CurlUrlHostPortTableTests.cs, Curl.Console.UnitTests/CurlCommandRunnerUrlRejectionTests.cs, Curl.Console/CurlCommandRunner.cs, Curl.Core.UnitTests/UrlSchemeGuesserTests.cs]
requirement: none
created: 2026-10-22
completed:
---
# BL-3173 — Normalise %{url_effective}, url.host and url.port as curl 8.22.0 does and reject no-host URLs

## Goal

`%{url_effective}`, `%{url.host}` and `%{url.port}` print what curl 8.22.0 prints for odd URLs, and malformed forms fail with the same error text.

## Context

Stewart reported on 2026-10-22. Measured with `-w '%{url_effective}'` against a refused port 9, real curl 8.21.0 reports `http://127.1:9/echo` as `http://127.0.0.1:9/echo`; `http://0x7f.1:9/` and `http://2130706433:9/` as `http://127.0.0.1:9/`; `http://127.0.0.1.:9/` as `http://127.0.0.1:9/` (trailing dot dropped); port `09` as `9`; `http://h.invalid:/echo` as `http://h.invalid/echo`. Curl reports every input unchanged, because `Curl.Engine.UnitLibrary/UrlEffective.cs` keeps "the authority as typed". Stewart also reported: `//127.0.0.1:18080/echo` must fail with the URL-rejected no-host error (measured real 8.21 text `curl: (3) URL rejected: No host part in the URL`; Stewart wrote "No host present"; measure 8.22.0) and not Curl's `Unsupported number of slashes following scheme`; `http:127.0.0.1:18080/echo` and URLs with leading or trailing spaces report the URL unchanged, not with an inserted `http://`; zone IDs print as upstream does (`[::1%25lo]` shown as `::1` in the connect error). The URL model is `Curl.Protocol.Abstractions.UnitLibrary` (`CurlUrl.cs`, `CurlUrlParser.cs`, `CurlUrlHost.cs`, `CurlUrlIPv4Address.cs`, `CurlUrlRejectionMessages.cs`); the scheme guess for a scheme-less URL is `Curl.Core.UnitLibrary/UrlSchemeGuesser.cs`. Measure curl 8.22.0 with `Record-CurlExchange.ps1` for the 20 inputs before pinning. Note `Curl.Protocol.Abstractions.UnitLibrary` is a shared contract library: this task edits only its URL files, named in `touches`. The URL-rejection task for unknown schemes and bad ports shares `CurlUrlParser.cs` and `UrlSchemeGuesser.cs` with this one; the board serialises them.

## Acceptance criteria

- [ ] Notes record curl 8.22.0's `%{url_effective}`, `%{url.host}` and `%{url.port}` (and exit code and text for rejected ones) for the 20 inputs, including every example above.
- [ ] A table-driven test in `Curl.Engine.UnitTests/UrlEffectiveNormalizationTests.cs` runs the 20 inputs through `UrlEffective.Normalize` and compares with the recorded `%{url_effective}`; a second table in `Curl.Protocol.Abstractions.UnitTests/CurlUrlHostPortTableTests.cs` compares `%{url.host}` and `%{url.port}` (as `CurlUrl` yields them).
- [ ] `//127.0.0.1:18080/echo` returns exit 3 with 8.22.0's measured no-host text, in a test through the command runner (`Curl.Console.UnitTests/CurlCommandRunnerUrlRejectionTests.cs`).
- [ ] `http:127.0.0.1:18080/echo` and URLs with leading/trailing spaces report `%{url_effective}` unchanged (or as 8.22.0 measures, noted in the test); zone-ID connect-error text is pinned.
- [ ] `dotnet build Curl.Engine.UnitLibrary Curl.Protocol.Abstractions.UnitLibrary Curl.Core.UnitLibrary -warnaserror` is clean and the fast tests of the matching `.UnitTests` projects pass; the existing `UrlEffective` and `CurlUrl` tests still pass.

## Notes

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
- Curl already printed every url.host and url.port right; only url_effective and the no-host text differed.
- Done in this run (left uncommitted for the shift's stash, as the task is requeued): `UrlEffective.Normalize(url, typedUrl, pathAsIs)` rebuilds the authority with the user info and a host name as typed, an IPv4 address dotted-quad, an IPv6 address normalised with its typed zone id, and the port in decimal (dropped when typed empty), and returns the typed URL when curl rejects it; `UrlSchemeGuesser.AddScheme` leaves a URL starting with `/` alone so the parser rejects it as `No host present`; `CurlCommandRunner.WriteOutRequestUrl` passes the given URL. Tests written: `UrlEffectiveNormalizationTests` (20 rows), `CurlUrlHostPortTableTests`, `CurlCommandRunnerUrlRejectionTests` (5 rows) - all pass with Curl.Engine, Curl.Protocol.Abstractions and Curl.Core fast tests green (2256, 929, 1792 passed).
- Touches widened by `Curl.Console/CurlCommandRunner.cs` (the -w URL for a rejected URL must be the given one, not the guessed one; no Doing task names it) and `Curl.Core.UnitTests/UrlSchemeGuesserTests.cs` (a row for the `/` branch, still to add).
- Requeued for BL-3182: the zone-ID connect-error text cannot be pinned here. On Windows Curl's refused connect times out (exit 28) instead of exit 7, and the text is built in `Curl.Networking.UnitLibrary/TcpConnector.cs`, which BL-3172 holds. Left for the next claim: once BL-3182 is Done, pin the zone-ID connect text (`Failed to connect to ::1:9`), add a `UrlSchemeGuesserTests` row for `/x` staying unchanged, run the Curl.Console fast tests, run `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary,Curl.Core.UnitLibrary,Curl.Console`, tick the boxes and commit.
- Decision (sensible default): a URL with no scheme that starts with `/` is not given a guessed scheme, because curl 8.22.0 rejects all such URLs with `No host present` and prints them as typed.

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: 3debb382 9eefc518 e6155fdf dd37067d 92579dca.
- 2026-10-22: Doing -> Backlog. Waits on BL-3182: zone-ID connect-error text needs TcpConnector's refused-connect fix (TcpConnector.cs held by BL-3172); normalisation, no-host and rejected-URL work done and stashed
