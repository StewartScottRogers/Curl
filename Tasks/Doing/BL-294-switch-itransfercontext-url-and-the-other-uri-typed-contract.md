---
id: BL-294
title: Switch ITransferContext.Url and the other Uri-typed contract members to CurlUrl
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-292]
touches: [Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests, Curl.Console, Curl.Console.UnitTests, Curl.Core.UnitLibrary, Curl.Core.UnitTests, Curl.Cookies.UnitLibrary, Curl.Cookies.UnitTests, Curl.Authentication.UnitTests, Curl.Protocol.Dict.UnitLibrary, Curl.Protocol.Dict.UnitTests, Curl.Protocol.File.UnitLibrary, Curl.Protocol.File.UnitTests, Curl.Protocol.Gopher.UnitLibrary, Curl.Protocol.Gopher.UnitTests, Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Curl.Protocol.Mqtt.UnitLibrary, Curl.Protocol.Mqtt.UnitTests, Curl.Protocol.Telnet.UnitLibrary, Curl.Protocol.Telnet.UnitTests, Curl.Protocol.Tftp.UnitLibrary, Curl.Protocol.Tftp.UnitTests]
requirement: none
created: 2026-09-26
completed:
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

- [ ] No production project in `touches` declares a `System.Uri` field, property, parameter or local for a transfer URL, and none calls `Uri.TryCreate` or `new Uri(` on one.
- [ ] The existing tests pass, converted to `CurlUrl`, with no expected exit code or output byte changed.
- [ ] A URL `CurlUrl` rejects ends the transfer with exit 3 and `URL rejected: Malformed input to a URL function`, as a `Uri.TryCreate` failure did.
- [ ] `dotnet build` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for every touched library.

## Notes

- Filed by BL-010 when ADR-0010 was accepted.

## Log

- 2026-09-26: Created.
- 2026-09-27: Backlog -> Doing.
