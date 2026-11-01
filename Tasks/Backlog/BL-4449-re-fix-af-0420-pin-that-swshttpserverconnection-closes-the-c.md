---
id: BL-4449
title: Re-fix AF-0420: pin that SwsHttpServerConnection closes the connection after a closing reply and keeps it open otherwise
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Conformance.UnitLibrary/SwsHttpServerConnection.cs, Curl.Conformance.UnitTests/SwsHttpServerConnectionTests.cs]
requirement: none
created: 2026-11-01
completed:
---
# BL-4449 — Re-fix AF-0420: pin that SwsHttpServerConnection closes the connection after a closing reply and keeps it open otherwise

## Goal

A test fails if `SwsHttpServerConnection.Send` stops ending the connection after a closing reply, or ends it after a reply that does not close.

## Context

Audit finding AF-0420 (accepted 2026-10-24, quality auditor, Medium): `Send`'s closing `SwsServerSend(..., true)` could become `false` with no test failing, so a closing reply (`swsclose`, close after the reply) never marked the close. BL-3469 rewrote `Send` (`Curl.Conformance.UnitLibrary/SwsHttpServerConnection.cs`, about line 424): it now takes `bool closes` and, under `if (closes)`, sets `readsRequests = false`, queues an empty `SwsServerSend([], serverBusyUntil)` and calls `RecordDisconnectOnce()`. The `true` literal is gone, so 8 re-audits in a row reported "site missing" and the finding stays open. Stewart chose a re-fix on 2026-11-01. On 2026-11-01 the mutant `readsRequests = false` -> `true` (line 444) timed out rather than failing a test, and no mutation operator reaches the empty close send at line 445, so nothing asserts the close itself.

## Acceptance criteria

- [ ] A test (in a new `Curl.Conformance.UnitTests/SwsHttpServerConnectionTests.cs`, or the existing connector tests if the connection is only reachable through them) sends a request whose reply closes the connection and asserts, within a bounded time and without hanging, that the server side ends the connection after the reply bytes and records the disconnect once.
- [ ] A second test sends a request whose reply does not close and asserts the connection stays open and a second request on it is answered.
- [ ] Deleting the `pendingSends.Add(new SwsServerSend([], serverBusyUntil));` line under `if (closes)` (a temporary local edit, reverted) makes the first test fail rather than hang; Notes record the failure message.
- [ ] `dotnet build` is clean, the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green, and the quality gates hold for `Curl.Conformance.UnitLibrary`.

## Notes

## Log

- 2026-11-01: Created.
