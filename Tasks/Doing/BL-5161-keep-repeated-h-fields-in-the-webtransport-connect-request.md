---
id: BL-5161
title: Keep repeated -H fields in the WebTransport CONNECT request
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Extended.Protocol.WebTransport.UnitLibrary/Session/WebTransportConnectHeaderFields.cs, Curl.Extended.Protocol.WebTransport.UnitTests]
requirement: none
created: 2026-11-13
completed:
---
# BL-5161 — Keep repeated -H fields in the WebTransport CONNECT request

## Goal

A WebTransport CONNECT request sends every `-H` field the user gave, even when a name repeats. A `-H` entry replaces or removes only the fields Curl adds itself.

## Context

Found by the shift-end review of 2026-11-13 (ADR-0710), type-design-analyzer, confirmed against the code.

`Curl.Extended.Protocol.WebTransport.UnitLibrary/Session/WebTransportConnectHeaderFields.cs:85-104`: `ApplyCustomHeader` sends every `Name: value` through `ReplaceOrRemove`, which runs `fields.RemoveAll(field => field.Name == name)`. That is meant to replace a field Curl adds (`authorization`, `user-agent`, `cookie`), but it also drops an earlier `-H` with the same name:

- `-H "X-Trace: a" -H "X-Trace: b"` sends only `x-trace: b`. Curl's HTTP path, like upstream curl, sends both.
- `-H "User-Agent;"` adds an empty `user-agent` and keeps the default `curl/...` one, so two `user-agent` fields go out. `Name;` should replace Curl's own field of that name, as it does on the HTTP path.

## Acceptance criteria

- [ ] Two `-H` values with the same name both appear in the fields `Build` returns, in the order given.
- [ ] `-H "Name: value"` and `-H "Name:"` still replace or remove a field Curl adds itself (`authorization`, `user-agent`, `cookie`).
- [ ] `-H "User-Agent;"` gives exactly one `user-agent` field, and it is empty.
- [ ] Unit tests in `Curl.Extended.Protocol.WebTransport.UnitTests` cover each case above. `dotnet build` is clean, the fast tests pass, and the library keeps 100% line and branch coverage.

## Notes

## Log

- 2026-11-13: Created.
- 2026-11-13: Backlog -> Doing. Criteria at claim: 8c6ba8c0 547a9b1f b96f9514 533cf9fa.
