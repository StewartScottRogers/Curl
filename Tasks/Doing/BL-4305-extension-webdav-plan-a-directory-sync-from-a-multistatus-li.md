---
id: BL-4305
title: Extension WebDAV: plan a directory sync from a Multistatus listing and a local folder
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4304]
touches: [Curl.Extension.WebDavTools.UnitLibrary/Sync, Curl.Extension.WebDavTools.UnitTests/Sync]
requirement: none
created: 2026-10-29
completed:
---
# BL-4305 — Extension WebDAV: plan a directory sync from a Multistatus listing and a local folder

## Goal

Given a Depth 1 Multistatus listing of a remote collection and a local folder, a planner says which files to download, which sub-collections to list next and which local files are already current, so `--webdav-sync` (BL-4131) fetches only what changed.

## Context

Split from BL-4131 for size. Builds on BL-4304's Multistatus entries. A file is current when the local file exists with the same length and a last-write time no older than `getlastmodified` (RFC 4918 15.7, 15.4); otherwise it is downloaded and its last-write time set to `getlastmodified`. Collections (15.9 resourcetype) are listed recursively; the entry for the requested collection itself is skipped. Hrefs that leave the collection, contain `..` or a path separator in a segment, or name an invalid file name are refused (no path traversal). Local deletion is out of scope (sync never deletes). Use injected file-system access (an interface in this library) so unit tests use a temporary directory or an in-memory fake.

## Acceptance criteria

- [ ] The planner returns downloads, sub-collections and current files for a listing and a local folder; unit tests citing RFC 4918 15.4, 15.7 and 15.9.
- [ ] An href outside the collection or with a traversal segment is refused; unit tests.
- [ ] The tests pass on Windows, Linux and macOS (no drive letters or Windows-only paths).
- [ ] The library meets the four quality gates by `Measure-CodeQuality.ps1`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: d37a7407 15968d71 14a572e7 3180db99 3d836d5e.
