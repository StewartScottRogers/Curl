# ADR-1164 — `--webdav-list` and `--webdav-sync` list and sync a WebDAV collection through curl's HTTP stack

- **Status:** Accepted
- **Date:** 2026-10-30

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-4131.

## Context

BL-4131 adds the extension `Curl.Extension.WebDavTools.UnitLibrary` (ADR-0999): listing a WebDAV collection from a
parsed `PROPFIND` and syncing a collection into a local directory. Upstream curl has no such options, so there is no
curl output to match; RFC 4918 (HTTP Extensions for WebDAV, June 2007) decides the wire behaviour. BL-4304 built the
`PROPFIND` request parts and the `207 Multistatus` reader, BL-4305 the sync planner, and BL-4303 let a transfer hook
send a request of its own (`WithRequest`) through curl's handler.

## Decision

- **Names.** Two options, `--webdav-list` (a flag) and `--webdav-sync <dir>`, and the extension name `webdav-tools` in
  `curl --extensions` and `curl --ai-help extensions`. No scheme: the requests ride on curl's own `http` and `https`.
- **Specification.** RFC 4918: sections 9.1 (`PROPFIND`), 10.2 (`Depth`), 13 and 14 (`207 Multistatus`), 15.4, 15.7
  and 15.9 (`getcontentlength`, `getlastmodified`, `resourcetype`).
- **Registration.** Like `--segments` (ADR-1087) the extension adds no protocol handler, so `ExtensionCatalogue.cs`
  gets no line. Its two options, its description and its hook activator are one line each in
  `ExtensionOptionCatalogue`, `ExtensionDescriptionCatalogue` and `ExtensionTransferHookCatalogue`. With
  `CURL_EXTENSIONS=0` neither option is registered, so curl refuses them as unknown options (exit 2). Without them
  every transfer is unchanged.
- **Through curl's stack.** Each request is curl's own transfer with the URL, method and body replaced
  (`WithRequest`) and `Depth` and `Content-Type` added (`WithRequestHeaders`), so TLS, proxies, `--user`, `-H`,
  `-L` and the rest apply as to any transfer. Nothing is written by the extension over a raw connection.
- **`--webdav-list`.** One `PROPFIND` with `Depth: 1` and a `prop` body naming `resourcetype`, `getcontentlength`,
  `getlastmodified` and `getetag`, sent to the transfer's URL. Each `response` of the 207 body is one line on the
  output, in body order: `collection` or `file`, the length, the last-modified date (RFC 1123), and the
  percent-decoded href, separated by tabs, with `-` for a property not reported. Tab-separated lines are chosen so a
  script can cut them. `-i` and `-D` show the 207's headers.
- **`--webdav-sync <dir>`.** The same `PROPFIND`, planned by `WebDavSyncPlanner`: each file not current (length and
  last-write time, sections 15.4 and 15.7) is fetched with `GET` into `<name>.webdav-part` and renamed over the local
  copy once the answer is a 2xx, its last-write time set to `getlastmodified`; each sub-collection is listed and synced
  into its own folder, depth first after the files. The folder is created when missing. One line per item goes to the
  output: `refused` and the href, `current` or `downloaded` and the local path. A local file is never deleted, and an
  href that would leave the folder is refused, not followed.
- **Failures.** A transfer that is not HTTP fails with exit 1 without contacting the server. A `PROPFIND` answered with
  anything but 207, or a `GET` with anything but a 2xx, fails with exit 22 (as `--fail` would), and a 207 body the
  reader refuses with exit 8 (weird server reply). A transfer that fails keeps its own exit code. A local folder that
  cannot be written fails with exit 23. The first failure ends the sync; files already downloaded stay.
- **Option misuse.** Both options together, or `--webdav-sync` with an empty value, refuse the transfer with exit 2
  (`FailedInit`) and a message, without contacting the server.

## Left out, and why

- **Deleting local files the server no longer lists, and uploading.** A sync that deletes or writes to the server
  needs a conflict policy the task did not ask for; the one-way, never-deleting download is the safe default.
- **`Depth: infinity`.** Servers may refuse it (RFC 4918 section 9.1 lets them answer 403), so the tree is walked one
  `Depth: 1` listing per collection.
- **A depth limit on the walk.** Each sub-collection's path is strictly longer than its parent's, and an href that
  leaves the collection is refused, so the walk ends with the server's tree.
- **ETag comparison.** The planner judges a file current by length and last-modified time, which the local file system
  keeps; it has nowhere to keep an ETag between runs.
- **Bodies and methods given with the options.** `-X` and `-d` are replaced only where a request needs its own
  (`PROPFIND`, `GET`); a sync `GET` keeps a body given with `-d`, as `-X GET -d` would send it.

## Known gap

Measured on 2026-10-30 against a loopback HttpListener: `WithRequest` (BL-4303) puts the body in `PostData`, which
curl's HTTP handler does not send, so the `PROPFIND` currently goes out with no body. RFC 4918 section 9.1 treats an
empty `PROPFIND` body as `allprop`, which returns all four properties, so listing and sync behave the same. BL-4349
makes `WithRequest` send its body; nothing here changes when it lands.

## Consequences

`Curl.Extension.WebDavTools.UnitLibrary` references `Curl.Extension.Abstractions.UnitLibrary`. Its unit tests drive the
hooks with a fake handler that answers by method and path, and the sync tests write into a temporary folder they
create and delete.
