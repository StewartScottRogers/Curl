---
id: BL-4343
title: Catch only the extension hook conflict, not every ArgumentException, in CurlComposition
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Console/CurlComposition.cs, Curl.Console/ExtensionOptionConflictException.cs, Curl.Extension.Abstractions.UnitLibrary/Connection/ConnectionHooks.cs, Curl.Extension.Abstractions.UnitLibrary/Resolution/ResolverHooks.cs, Curl.Extension.Abstractions.UnitLibrary/Scheme/SchemeHooks.cs, Curl.Console.UnitTests, Curl.Extension.Abstractions.UnitTests]
requirement: none
created: 2026-10-30
completed:
---
# BL-4343 — Catch only the extension hook conflict, not every ArgumentException, in CurlComposition

## Goal

Only a real extension hook conflict is reported as `curl: <conflict>` with exit 2. Any other `ArgumentException` an extension's activator throws stays a failure of its own, with its message and parameter name intact.

## Context

Found by the silent-failure-hunter and type-design-analyzer reviewers.

- `Curl.Console/CurlComposition.cs:743`, `:787` and `:819` catch every `ArgumentException` from `SchemeHooks.ActivatedBy`, `ConnectionHooks.ActivatedBy` and `ResolverHooks.ActivatedBy`.
- They rethrow it through `OptionConflictOf` (`:831`), which strips the `(Parameter 'x')` text.
- Those calls also run every extension's activator delegate. An `ArgumentNullException` or `ArgumentOutOfRangeException` from an extension's own bug is therefore reported to the user as an option conflict with exit 2, and its parameter name is lost.
- The deliberate conflicts are thrown as plain `ArgumentException` at:
  - `Curl.Extension.Abstractions.UnitLibrary/Connection/ConnectionHooks.cs:28`
  - `Resolution/ResolverHooks.cs:30`
  - `Scheme/SchemeHooks.cs:37`
- Separately, the doc on `Curl.Console/ExtensionOptionConflictException.cs` names only connection conflicts (ADR-1010). It is also thrown for scheme conflicts (ADR-1048) and resolver conflicts (ADR-1012).

## Acceptance criteria

- [ ] The three hook classes throw a dedicated conflict exception type, not a bare `ArgumentException`, and `CurlComposition` catches only that type.
- [ ] A unit test in `Curl.Console.UnitTests` shows that an activator throwing `ArgumentNullException` is not turned into an `ExtensionOptionConflictException`.
- [ ] The existing conflict tests (connection, resolver and scheme) still pass with the same `curl:` message and exit code.
- [ ] The doc comment on `ExtensionOptionConflictException` names connection, scheme and resolver conflicts.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-30: Created by the shift-end review (ADR-0710) of a9f1a8149..b79175cc5.
