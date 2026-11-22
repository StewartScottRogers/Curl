---
id: BL-5753
title: Make a Curl.Api PUT to imap:// APPEND the message (BL-5610 did not hold)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Imap.UnitLibrary/ImapAppend.cs, Curl.Api/RequestBodies]
requirement: none
created: 2026-11-22
completed:
---
# BL-5753 — Make a Curl.Api PUT to imap:// APPEND the message (BL-5610 did not hold)

## Goal

A `Curl.Api` `PUT` with a byte body, or `CurlContent.FromStream(stream, length)`, to `imap://host/INBOX` appends the message, in the build that contains BL-5610's fix.

## Context

From Surly cookbook bug report `2026-11-22T1116-Curl-bug-report.md` (batch 11 of the 1,000-recipe plan), finding 1. Build tested: Curl `c84035f797`, Windows 11; server Surl `5da5b3eaf`. Check it still reproduces on the current branch first.

BL-5610 (Done 2026-11-20) was to make a `Curl.Api` body to `imap://` upload with its length so APPEND works, and its commit is in the build tested, yet two cookbook writers independently got `CurlException` 25 `Cannot APPEND with unknown input file size` (raised in `Curl.Original.Protocol.Imap.UnitLibrary/ImapAppend.cs`). Repro: `surl --mailbox ann@example.com:pw imap://127.0.0.1:1143/`; `new CurlRequest(HttpMethod.Put, "imap://127.0.0.1:1143/INBOX") { Body = CurlContent.FromStream(new MemoryStream(eml), eml.Length), Authentication = ... }`. Read BL-5610's notes and tests and find why the length still does not reach `ImapAppend` on this path (a different request shape from the one its test pinned: authentication set, `PUT` rather than `POST`, a `MemoryStream` length, or the mailbox in the path). Pin the cookbook's exact shape with a test against the IMAP test server.

## Acceptance criteria

- [ ] The cookbook's exact request (PUT, stream body with length, authentication, `imap://host/INBOX`) appends the message against an IMAP test server, and a byte body does too, pinned by tests; a following FETCH returns the same bytes.
- [ ] The Notes say why BL-5610's fix did not cover this shape.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-22: Created from Surly bug report 2026-11-22T1116, finding 1.
- 2026-11-22: Backlog -> Doing. Criteria at claim: 2192c298 4a06c735 3d836d5e.
