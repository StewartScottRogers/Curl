---
id: BL-2442
title: Libcurl: CurlForm (formadd, formget, formfree) and CURLOPT_HTTPPOST
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2441]
touches: [Libcurl/Mime/Form, Libcurl.UnitTests/Mime/Form, Libcurl/CLAUDE.md, Libcurl.UnitTests/Options/Http/EasyHttpOptionSettersTests.cs]
requirement: none
created: 2026-10-15
completed: 2026-10-15
---
# BL-2442 — Libcurl: CurlForm (formadd, formget, formfree) and CURLOPT_HTTPPOST

## Goal

Libcurl has `CurlForm.Add/Get/Free` and `CURLOPT_HTTPPOST`, marked obsolete as upstream deprecates them.

## Context

ADR-0506; phase 3 of `Documentation/Planning/Curl-Api-And-libcurl.md`. Read the seam as it is in `Curl.Api.UnitLibrary` (EngineContracts, Options, Callbacks, Info, Codes) and the managed engine as it is in `Curl.Engine.UnitLibrary` now, not the plan's guesses, and correct the plan where they differ. Libcurl code and tests live in the area sub-folders named in `touches` (`Libcurl/<Area>` with its tests in `Libcurl.UnitTests/<Area>`) so areas run in parallel lanes; Libcurl tests use fakes of the seam interfaces, never the network. `--ai-help` is untouched (no option changes).

## Acceptance criteria

- [x] Fake-engine tests pin each `CURLFORM_*` argument.
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.
- [x] `Measure-CodeQuality.ps1 -Library Libcurl` reports 0 failing members (100% line and branch coverage, complexity at most 10, CRAP at most 30).
- [x] Every new public member has an XML doc comment, and `Libcurl/CLAUDE.md` or `Curl.Engine.UnitLibrary`'s CLAUDE.md is kept true.

## Notes

- Design (ADR-0586): `CurlForm` (`[Obsolete]`, pointing at `MimeInit`) wraps the seam's
  `ICurlFormBuilder`. `CurlForm.Add(library, ref form, params CurlFormArgument[])` mirrors
  `curl_formadd(&httppost, &last_post, ...)`: a `null` form starts one, set only on `Ok`. `Get` is
  `curl_formget`, `Free`/`Dispose` `curl_formfree`. `CURLOPT_HTTPPOST` stores the form's body when
  set, as `CURLOPT_MIMEPOST` does. The engine owns the argument checks; Libcurl passes each
  `CURLFORM_*` through, which `CurlFormTests.Add_EachFormOption_ReachesTheEngineWithItsValueAsGiven`
  pins for all 21 options.
- `touches` widened (no other Doing task names them): `Libcurl/CLAUDE.md` to describe `Mime/Form`,
  and `Libcurl.UnitTests/Options/Http/EasyHttpOptionSettersTests.cs`, whose
  `SetOption_Httppost_IsLeftToTheFormTask` asserted `NotBuiltIn`; it is now
  `SetOption_Httppost_IsSetByTheFormArea` asserting `Ok`.
- Library-path tests use their own `FakeFormEngine`, since `FakeCurlEngine`'s inert form builder
  cannot return a `CurlFormAddCode`.
- Measured: Libcurl.UnitTests 743 passed; quality report 0 failing members; full fast run green.

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
- 2026-10-15: Doing -> Done. Libcurl CurlForm (curl_formadd/formget/formfree, obsolete) and CURLOPT_HTTPPOST work over the engine's form builder
