# Curl.Extension.WebDavTools.UnitLibrary

The WebDAV tools extension (ADR-0999): listing a WebDAV collection with `PROPFIND`. Namespace
`Curl.Extension.WebDavTools`. It references only the base class library. The options, the catalogue
registration and the ADR arrive in BL-4131.

- `Propfind/WebDavPropfindRequest` gives the `PROPFIND` method, the request body (a `DAV:propfind`
  whose `prop` names `resourcetype`, `getcontentlength`, `getlastmodified` and `getetag`), its media
  type, and the `Depth` header value for each `Propfind/WebDavDepth` (`0`, `1`, `infinity`), as RFC 4918
  sections 9.1 and 10.2 say.
- `Multistatus/WebDavMultistatusReader` reads a `207 Multistatus` body (RFC 4918 sections 13, 14.16,
  14.22, 14.24) into one `Multistatus/WebDavResourceEntry` per `response`: the percent-decoded href,
  whether it is a collection, content length, last modified (an RFC 1123 date), ETag and status. It
  matches elements in the `DAV:` namespace whatever their prefix and takes properties only from
  `propstat` elements with a 2xx status. A length or date it cannot read is left `null`.
- The reader never throws on a bad body: it returns a `Multistatus/WebDavMultistatusResult` refused
  with a `WebDavMultistatusFailureKind` (not well-formed, a document type declaration, a root that is
  not `DAV:multistatus`, a missing required element, a status that is not an HTTP status line).
  Document type declarations are prohibited, so no external entity is ever resolved.

Tested against the specification, not against curl. Held to the four quality gates in the root
`CLAUDE.md`. Its tests are in `Curl.Extension.WebDavTools.UnitTests`.
