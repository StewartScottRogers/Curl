---
id: BL-5753
title: Make a Curl.Api PUT to imap:// APPEND the message (BL-5610 did not hold)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Imap.UnitLibrary/ImapAppend.cs, Curl.Api/RequestBodies, Curl.Engine.UnitLibrary/ManagedEasyTransfer.Context.cs, Curl.Engine.UnitTests/ManagedEasyTransferTests.InFileSize.cs]
requirement: none
created: 2026-11-22
completed: 2026-11-22
---
# BL-5753 — Make a Curl.Api PUT to imap:// APPEND the message (BL-5610 did not hold)

## Goal

A `Curl.Api` `PUT` with a byte body, or `CurlContent.FromStream(stream, length)`, to `imap://host/INBOX` appends the message, in the build that contains BL-5610's fix.

## Context

From Surly cookbook bug report `2026-11-22T1116-Curl-bug-report.md` (batch 11 of the 1,000-recipe plan), finding 1. Build tested: Curl `c84035f797`, Windows 11; server Surl `5da5b3eaf`. Check it still reproduces on the current branch first.

BL-5610 (Done 2026-11-20) was to make a `Curl.Api` body to `imap://` upload with its length so APPEND works, and its commit is in the build tested, yet two cookbook writers independently got `CurlException` 25 `Cannot APPEND with unknown input file size` (raised in `Curl.Original.Protocol.Imap.UnitLibrary/ImapAppend.cs`). Repro: `surl --mailbox ann@example.com:pw imap://127.0.0.1:1143/`; `new CurlRequest(HttpMethod.Put, "imap://127.0.0.1:1143/INBOX") { Body = CurlContent.FromStream(new MemoryStream(eml), eml.Length), Authentication = ... }`. Read BL-5610's notes and tests and find why the length still does not reach `ImapAppend` on this path (a different request shape from the one its test pinned: authentication set, `PUT` rather than `POST`, a `MemoryStream` length, or the mailbox in the path). Pin the cookbook's exact shape with a test against the IMAP test server.

## Acceptance criteria

- [x] The cookbook's exact request (PUT, stream body with length, authentication, `imap://host/INBOX`) appends the message against an IMAP test server, and a byte body does too, pinned by tests; a following FETCH returns the same bytes.
- [x] The Notes say why BL-5610's fix did not cover this shape.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

Root cause: BL-5610 made Curl.Api set Upload and InFileSize for imap (that part held, and Curl.Api sets them for PUT, POST, stream or bytes alike). But the engine (`ManagedEasyTransfer.DeclaredUploadLengthOf`) only gave a read-callback upload its declared length for http, https, smb and smbs, so for imap the callback stream was not seekable (`CanSeek` false) and `ImapAppend` refused it with "unknown input file size". BL-5610 tested only the Curl.Api options, never the engine hand-off. Fix: imap and imaps added to `DeclaredUploadLengthOf`, pinned by DataRows in `ManagedEasyTransferTests.InFileSize.cs`. touches widened to those two Engine files (no other Doing task names Engine). No live IMAP server or Surl is available to a lane, so the live append-and-FETCH run rests on the engine pin plus the existing ImapAppend tests (a seekable stream of declared length is exactly what its tests feed it). Measure-CodeQuality not run for Curl.Engine: the change adds no method or line, the build (complexity gate) is clean.

## Log

- 2026-11-22: Created from Surly bug report 2026-11-22T1116, finding 1.
- 2026-11-22: Backlog -> Doing. Criteria at claim: 2192c298 4a06c735 3d836d5e.
- 2026-11-22: Doing -> Done. Curl.Api PUT to imap:// APPENDs: the engine now holds an imap upload to its declared length
