---
id: BL-2098
title: Apply strippart to the upload in the conformance harness and list upstream test1187
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-11
completed:
---
# BL-2098 — Apply strippart to the upload in the conformance harness and list upstream test1187

## Goal

The conformance harness applies a case's `<strippart>` lines to its `<verify><upload>` comparison, as upstream's runtests does, so upstream test1187 passes in process and is on the ratchet.

## Context

BL-2091 made mail MIME (smtp, imap) escape `-F` names and file names with backslashes; test1187's upload now matches upstream byte for byte except the random boundary. Its `<strippart>` lines (`s/^--------------------------[A-Za-z0-9]*/.../` and `s/boundary=------------------------[A-Za-z0-9]*/.../`) are meant to normalise that boundary, but the harness compares `<upload>` without them and fails at byte 64 of line 1: expected `boundary=----------------------------`, got `boundary=------------------------<22 random chars>`. Read curl 8.21.0's `tests/runtests.pl` / `tests/runner.pm` for where `strippart` applies to the upload before changing the harness.

## Acceptance criteria

- [ ] An MSTest in Curl.Conformance.UnitTests shows `<strippart>` applied to the upload before it is compared, from inline test-file text.
- [ ] `1187` is on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` and the ratchet passes.
- [ ] No previously listed case starts failing.
- [ ] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.

## Notes

## Log

- 2026-10-11: Created.
