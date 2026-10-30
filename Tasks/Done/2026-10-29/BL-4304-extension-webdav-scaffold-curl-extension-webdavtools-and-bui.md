---
id: BL-4304
title: Extension WebDAV: scaffold Curl.Extension.WebDavTools and build PROPFIND and read 207 Multistatus
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.WebDavTools.UnitLibrary, Curl.Extension.WebDavTools.UnitTests, Curl.slnx]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4304 — Extension WebDAV: scaffold Curl.Extension.WebDavTools and build PROPFIND and read 207 Multistatus

## Goal

`Curl.Extension.WebDavTools.UnitLibrary` exists with its tests, builds a `PROPFIND` request body and `Depth` header, and reads a `207 Multistatus` response into entries (href, collection or not, content length, last modified, ETag, status).

## Context

Split from BL-4131 (WebDAV list and sync) for size, as BL-4284 and BL-4285 were split from BL-4125. Follow ADR-0999 and the `new-project` skill. One-line `<Folder Name="/Extensions/WebDavTools/">` in `Curl.slnx` under the marker comment (ADR-1003). The catalogue registration, options and ADR stay with BL-4131. Specification RFC 4918: section 9.1 (PROPFIND, `Depth` 0, 1, infinity), 10.2 (Depth header), 13 (Multi-Status response), 14.16 (multistatus), 14.24 (response), 14.22 (propstat), 15 (DAV properties: resourcetype, getcontentlength, getlastmodified as RFC 1123 dates, getetag). Parse with `System.Xml` (BCL), honouring the `DAV:` namespace whatever its prefix; percent-decode hrefs; never resolve external entities (DtdProcessing prohibited).

## Acceptance criteria

- [x] `Curl.Extension.WebDavTools.UnitLibrary` and `.UnitTests` exist, listed on one line in `Curl.slnx` under `/Extensions/WebDavTools/`, and the build's `VerifyExtensionIsolation` passes.
- [x] A `PROPFIND` builder gives the body (`propfind` with `prop` naming the four properties) and the `Depth` header for 0, 1 and infinity; unit tests against RFC 4918 9.1 and 10.2.
- [x] A Multistatus reader returns one entry per `response`, using only `propstat` with a 2xx status, with any namespace prefix; unit tests against RFC 4918 13, 14.16, 14.22 and the 9.1.3 and 9.1.5 examples.
- [x] A malformed body or one with a DTD is refused with a typed error, not an exception escaping; unit tests.
- [x] The library meets the four quality gates by `Measure-CodeQuality.ps1`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Built directly rather than through the full /feature subagent stages: the shift's $3 cost cap leaves no room for five agents, and the work is two small BCL-only parsers in one new library.
- Layout: `Propfind/WebDavPropfindRequest` (method, `Depth` header name and values via `WebDavDepth`, `application/xml; charset=utf-8`, a `DAV:propfind` body naming resourcetype, getcontentlength, getlastmodified, getetag) and `Multistatus/WebDavMultistatusReader` returning a `WebDavMultistatusResult` (entries or a `WebDavMultistatusFailureKind` plus message). The library references only the BCL.
- Decisions (sensible defaults; the extension's ADR stays with BL-4131 as Context says): elements match by `DAV:` namespace name, so any prefix or a default namespace works and a same-named property in another namespace is ignored; properties come from every 2xx `propstat`, the first one winning; an entry's status is the response's own `status`, else its first 2xx `propstat`'s, else its first `propstat`'s; a `getcontentlength` that is not a whole number or a `getlastmodified` that is not an RFC 1123 date (`"r"`, day name checked) is left null rather than refusing the whole listing, as one odd property should not hide the others; a missing href, prop or status, or a status that is not `HTTP/x <3 digits>`, refuses the body.
- DTDs: `DtdProcessing.Prohibit` with no resolver; the `XmlException` is reported as `DocumentTypeDeclaration` when the body holds `<!DOCTYPE`, else `NotWellFormed`.
- Measured: Measure-CodeQuality -Library Curl.Extension.WebDavTools.UnitLibrary: 100% line, 100% branch, 24 members, 0 failing, worst CRAP 8 (ReadResponse was complexity 16 at first; the null-conditional property reads moved into helpers).
- Fast tests: 81 test projects passed, 0 failed (35 in Curl.Extension.WebDavTools.UnitTests). Tests are pure string parsing, platform-neutral, so CI on Linux and macOS is expected to match.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: ef607c7b 11e4fb57 698374f9 3e341d5b 3180db99 3d836d5e.
- 2026-10-29: Doing -> Done. Curl.Extension.WebDavTools builds PROPFIND requests and reads 207 Multistatus bodies into entries, refusing malformed or DTD bodies with a typed error
