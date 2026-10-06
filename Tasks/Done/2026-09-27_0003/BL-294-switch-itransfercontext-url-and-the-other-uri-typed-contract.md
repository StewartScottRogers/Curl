---
id: BL-294
title: Switch ITransferContext.Url and the other Uri-typed contract members to CurlUrl
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-292]
touches: [Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests, Curl.Console, Curl.Console.UnitTests, Curl.Core.UnitLibrary, Curl.Core.UnitTests, Curl.Cookies.UnitLibrary, Curl.Cookies.UnitTests, Curl.Authentication.UnitTests, Curl.Protocol.Dict.UnitLibrary, Curl.Protocol.Dict.UnitTests, Curl.Protocol.File.UnitLibrary, Curl.Protocol.File.UnitTests, Curl.Protocol.Gopher.UnitLibrary, Curl.Protocol.Gopher.UnitTests, Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Curl.Protocol.Mqtt.UnitLibrary, Curl.Protocol.Mqtt.UnitTests, Curl.Protocol.Telnet.UnitLibrary, Curl.Protocol.Telnet.UnitTests, Curl.Protocol.Tftp.UnitLibrary, Curl.Protocol.Tftp.UnitTests, Documentation/Planning/Decisions/ADR-0010-representing-urls-system-uri-cannot-round-trip.md]
requirement: none
created: 2026-09-26
completed: 2026-09-27
---
# BL-294 — Switch ITransferContext.Url and the other Uri-typed contract members to CurlUrl

## Goal

`ITransferContext.Url`, `TransferContext.Url`, `HttpAuthRequest.Url` and the `Uri`
parameters of `ICookieStore` are `CurlUrl`, and `CurlCommandRunner` parses a URL with
`CurlUrl.TryParse` instead of `Uri.TryCreate`. No production code holds a `System.Uri`
for a transfer URL any more.

## Context

- ADR-0010 (Accepted, option 1 "Replace"). `CurlUrl` comes from BL-292 and offers the
  `Uri` members handlers read, so most handler code compiles unchanged; the bulk of the
  work is the tests' `new Uri(...)` (about 160 occurrences on 2026-09-26) becoming
  `CurlUrl` values.
- Production readers of a transfer `Uri` on 2026-09-26: `CurlCommandRunner`,
  `TransferContextFactory`, `ProtocolDispatcher`, `ProxySelector`, `RedirectFollower`,
  `CookieOrigin`, `CookieStore`, `SetCookieParser`, and the handlers of `dict`, `file`,
  `gopher`, `http` (with `HttpRedirectLocation` and `HttpRequestHeadFormatter`), `mqtt`,
  `telnet` and `tftp`. `Curl.Cli.UnitLibrary` only calls `Uri.EscapeDataString` and
  stays as it is.
- Redirect resolution (`RedirectFollower`, `HttpRedirectLocation`) needs relative
  reference resolution on `CurlUrl`; add it to `CurlUrl` here if BL-292 did not.
- Pass `ITransferContext.PathAsIs` (BL-293) to `CurlUrl.TryParse` if it exists by then;
  otherwise `false`.
- This task changes a shared contract, so its `touches` is wide and it runs alone.

## Acceptance criteria

- [x] No production project in `touches` declares a `System.Uri` field, property, parameter or local for a transfer URL, and none calls `Uri.TryCreate` or `new Uri(` on one.
- [x] The existing tests pass, converted to `CurlUrl`, with no expected exit code or output byte changed.
- [x] A URL `CurlUrl` rejects ends the transfer with exit 3 and `URL rejected: Malformed input to a URL function`, as a `Uri.TryCreate` failure did.
- [x] `dotnet build` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for every touched library.

## Notes

- Filed by BL-010 when ADR-0010 was accepted.
- Done 2026-09-27. `ITransferContext.Url`, `TransferContext.Url`, `HttpAuthRequest.Url`
  and the `ICookieStore` parameters are `CurlUrl`; `CurlCommandRunner` parses with
  `CurlUrl.TryParse(text, options.PathAsIs, ...)`, and `RedirectFollower` and
  `TransferRetrier` parse targets with it too. The only `Uri` left in production is
  `Uri.EscapeDataString`/`UnescapeDataString` on strings.
- Added `CurlUrl.Parse` (throwing twin of `TryParse`) and made `CurlUrl` a record so
  the ~160 test sites converted mechanically (`new Uri(` became `CurlUrl.Parse(`) and
  still compare by value.
- Added `HttpUrlText` (Http): request target, host and port, origin, replacing
  `Uri.PathAndQuery`, `GetLeftPart` and the old `HostAsWritten` search.
- Measured, curl 8.21.0 `/mingw64/bin/curl` on Windows, 2026-09-27: a one-shot TCP
  listener on 127.0.0.1:18777 received `GET /%E4?ö=1 HTTP/1.1` for
  `curl -s "http://127.0.0.1:18777/ä?ö=1"` - path bytes above 0x7F percent-encoded,
  query bytes raw. `curl -sS file://example.com/x` and `file://[::1]/x`:
  `curl: (3) URL rejected: Bad file:// URL`. `curl -sS "http:////h/"`:
  `curl: (3) URL rejected: Unsupported number of slashes following scheme`.
- Decisions (ADR-0010, "Switch decisions taken under BL-294", decided by Claude under
  Stewart's delegation): request target per the measurement above; hosts keep their
  case (one test's expected connect host changed from `example.com` to `Example.com`
  for `HTTPS://Example.com:8443/`, not an exit code or output byte); a rejected
  `file://` host now prints the generic exit-3 line, as criterion 3 asks - curl's
  specific reason is follow-up BL-324.
- `FileUrlPath` lost its host check and its empty-path check: `CurlUrl` rejects every
  other host and reads a scheme only before `:/`, so both were unreachable. The
  FileUrlPath tests for rejected hosts now pin `CurlUrl.TryParse` returning false.
- `dict` and `mqtt` percent-decoders now copy a stray `%` (it used to arrive as `%25`).
- Touches: added ADR-0010 for the decision record; no task in Doing names it.
- Gates: build clean, `dotnet format --verify-no-changes` clean, fast tests green,
  `Measure-CodeQuality.ps1` 100% line and branch for every touched library. The only
  failing members are in `Curl.Networking.UnitLibrary` (socket code, not touched).

## Log

- 2026-09-26: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. Transfer URLs are CurlUrl end to end: the contract, the runner's parse, redirects, retries, proxy, cookies and every handler; System.Uri is gone from the transfer path
