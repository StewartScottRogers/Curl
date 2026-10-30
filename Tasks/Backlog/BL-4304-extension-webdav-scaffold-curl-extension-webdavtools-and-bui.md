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
completed:
---
# BL-4304 — Extension WebDAV: scaffold Curl.Extension.WebDavTools and build PROPFIND and read 207 Multistatus

## Goal

`Curl.Extension.WebDavTools.UnitLibrary` exists with its tests, builds a `PROPFIND` request body and `Depth` header, and reads a `207 Multistatus` response into entries (href, collection or not, content length, last modified, ETag, status).

## Context

Split from BL-4131 (WebDAV list and sync) for size, as BL-4284 and BL-4285 were split from BL-4125. Follow ADR-0999 and the `new-project` skill. One-line `<Folder Name="/Extensions/WebDavTools/">` in `Curl.slnx` under the marker comment (ADR-1003). The catalogue registration, options and ADR stay with BL-4131. Specification RFC 4918: section 9.1 (PROPFIND, `Depth` 0, 1, infinity), 10.2 (Depth header), 13 (Multi-Status response), 14.16 (multistatus), 14.24 (response), 14.22 (propstat), 15 (DAV properties: resourcetype, getcontentlength, getlastmodified as RFC 1123 dates, getetag). Parse with `System.Xml` (BCL), honouring the `DAV:` namespace whatever its prefix; percent-decode hrefs; never resolve external entities (DtdProcessing prohibited).

## Acceptance criteria

- [ ] `Curl.Extension.WebDavTools.UnitLibrary` and `.UnitTests` exist, listed on one line in `Curl.slnx` under `/Extensions/WebDavTools/`, and the build's `VerifyExtensionIsolation` passes.
- [ ] A `PROPFIND` builder gives the body (`propfind` with `prop` naming the four properties) and the `Depth` header for 0, 1 and infinity; unit tests against RFC 4918 9.1 and 10.2.
- [ ] A Multistatus reader returns one entry per `response`, using only `propstat` with a 2xx status, with any namespace prefix; unit tests against RFC 4918 13, 14.16, 14.22 and the 9.1.3 and 9.1.5 examples.
- [ ] A malformed body or one with a DTD is refused with a typed error, not an exception escaping; unit tests.
- [ ] The library meets the four quality gates by `Measure-CodeQuality.ps1`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-29: Created.
