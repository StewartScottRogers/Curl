# Curl.Extension.WebDavTools.UnitLibrary

The WebDAV tools extension (ADR-0999, ADR-1149): `--webdav-list` lists a WebDAV collection from a
`PROPFIND`, and `--webdav-sync <dir>` downloads a collection tree into a local folder. Namespace
`Curl.Extension.WebDavTools`. It references only `Curl.Extension.Abstractions.UnitLibrary`.

- `WebDavToolsExtension` declares the two options and the description, and `HookFor` gives the hook for
  an option group: the listing hook, the sync hook, or `Transfer/RefusedWebDavTransferHook` (exit 2) for
  both options together or an empty folder. `Curl.Extension.Composition` registers them in its option,
  description and transfer hook catalogues; `ExtensionCatalogue.cs` has no line, as there is no scheme.
- `Transfer/WebDavCollectionListing` sends a `Depth: 1` `PROPFIND` through curl's handler
  (`WithRequest`, `WithRequestHeaders`) and reads the 207 body: exit 1 for a transfer that is not HTTP,
  22 for an answer that is not 207, 8 for a body the reader refuses, and a failed transfer's own result.
- `Transfer/WebDavListTransferHook` writes one tab-separated line per `response`: `collection` or `file`,
  length, last modified (RFC 1123), href; `-` for a property not reported.
- `Transfer/WebDavSyncTransferHook` plans each listing with `Sync/WebDavSyncPlanner`, downloads each file
  that is not current with `GET` into a `.webdav-part` file renamed over the local copy on a 2xx, sets its
  last-write time to `getlastmodified`, then syncs each sub-collection into its own folder. It writes
  `refused`, `current` and `downloaded` lines; a non-2xx `GET` fails with exit 22, a folder it cannot
  write with exit 23, and the first failure ends the sync.

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
- `Sync/WebDavSyncPlanner` plans the sync of one collection listed with `Depth: 1` into a local folder
  as a `Sync/WebDavSyncPlan`: sub-collections (RFC 4918 section 15.9) to list next, files to download
  (each `WebDavSyncItem` carries the `getlastmodified` the download sets as its last-write time), and
  files already current (same length, section 15.4, and a last-write time no older than
  `getlastmodified`, section 15.7). The entry for the listed collection itself is skipped. An href
  outside the collection, a `.` or `..` name or one holding `/` or `\`, and an empty name or one holding
  a character Windows forbids in a file name are refused (`WebDavSyncRefusalKind`) on every platform,
  so nothing is written outside the folder. It reads the disk through `Sync/IWebDavLocalFileSystem`
  (`Sync/WebDavDiskFileSystem` in production) and never deletes a local file.

Tested against the specification, not against curl. Held to the four quality gates in the root
`CLAUDE.md`. Its tests are in `Curl.Extension.WebDavTools.UnitTests`.
