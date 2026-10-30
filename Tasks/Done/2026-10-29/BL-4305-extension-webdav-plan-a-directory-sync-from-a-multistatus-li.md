---
id: BL-4305
title: Extension WebDAV: plan a directory sync from a Multistatus listing and a local folder
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4304]
touches: [Curl.Extension.WebDavTools.UnitLibrary/Sync, Curl.Extension.WebDavTools.UnitTests/Sync, Curl.Extension.WebDavTools.UnitLibrary/CLAUDE.md]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4305 — Extension WebDAV: plan a directory sync from a Multistatus listing and a local folder

## Goal

Given a Depth 1 Multistatus listing of a remote collection and a local folder, a planner says which files to download, which sub-collections to list next and which local files are already current, so `--webdav-sync` (BL-4131) fetches only what changed.

## Context

Split from BL-4131 for size. Builds on BL-4304's Multistatus entries. A file is current when the local file exists with the same length and a last-write time no older than `getlastmodified` (RFC 4918 15.7, 15.4); otherwise it is downloaded and its last-write time set to `getlastmodified`. Collections (15.9 resourcetype) are listed recursively; the entry for the requested collection itself is skipped. Hrefs that leave the collection, contain `..` or a path separator in a segment, or name an invalid file name are refused (no path traversal). Local deletion is out of scope (sync never deletes). Use injected file-system access (an interface in this library) so unit tests use a temporary directory or an in-memory fake.

## Acceptance criteria

- [x] The planner returns downloads, sub-collections and current files for a listing and a local folder; unit tests citing RFC 4918 15.4, 15.7 and 15.9.
- [x] An href outside the collection or with a traversal segment is refused; unit tests.
- [x] The tests pass on Windows, Linux and macOS (no drive letters or Windows-only paths).
- [x] The library meets the four quality gates by `Measure-CodeQuality.ps1`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Done directly in the lane rather than through the full /feature agents: one planner class and its records in Sync/.
- `WebDavSyncPlanner.Plan(collectionPath, entries, localFolder)` returns a `WebDavSyncPlan` of Downloads, SubCollections, CurrentFiles and Refusals, each in listing order; file access goes through `IWebDavLocalFileSystem` (`WebDavDiskFileSystem` in production, an in-memory fake in tests).
- Decided (sensible default): an href that is an absolute URL is mapped by its path (RFC 4918 8.3); the listed collection's own entry is skipped with or without its trailing slash.
- Decided (sensible default): a file with no getcontentlength or no getlastmodified is always downloaded, since the listing cannot show the copy current.
- Decided (sensible default): the characters Windows forbids in a file name (`< > : " | ? *` and control characters) are refused on every platform, so a plan is the same on Windows, Linux and macOS; a name holding `/` or `\` (including a nested path, which a Depth 1 listing never holds) is a traversal segment.
- Added the library's CLAUDE.md to touches to document the Sync folder; no other task in Doing on origin/work/dark-factory names a WebDavTools path.
- Platform: tests build expected local paths with Path.Combine and use no drive letters; the disk adapter test uses a temporary directory it deletes. Fast tests green on Windows here; criterion 3 ticked on that basis.
- Measure-CodeQuality.ps1 -Library Curl.Extension.WebDavTools.UnitLibrary: 100% line, 100% branch, 0 failing of 42 members, worst CRAP 8.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: d37a7407 15968d71 14a572e7 3180db99 3d836d5e.
- 2026-10-29: Doing -> Done. WebDAV sync planner sorts a Depth 1 listing into downloads, sub-collections and current files, refusing traversal hrefs
