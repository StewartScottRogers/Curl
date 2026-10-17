---
id: BL-2616
title: Add curl_mprintf, curl_msprintf, curl_msnprintf, curl_maprintf and curl_mfprintf to Libcurl
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Libcurl, Libcurl.UnitTests]
requirement: none
created: 2026-10-17
completed:
---
# BL-2616 — Add curl_mprintf, curl_msprintf, curl_msnprintf, curl_maprintf and curl_mfprintf to Libcurl

## Goal

Libcurl exposes libcurl's `curl_m*printf` family (`curl_mprintf`, `curl_mfprintf`, `curl_msprintf`, `curl_msnprintf`, `curl_maprintf`, and the `v` variants where a C# shape makes sense), formatting exactly as curl 8.22.0's `lib/mprintf.c` does.

## Context

libtest program `lib557.c` (curl 8.22.0 `tests/libtest/lib557.c`, case 557) exercises the whole of `lib/mprintf.c`: `%hu %hd %u %d %lu %ld %zu`, `CURL_FORMAT_CURL_OFF_T`, widths and precisions from `*`, positional arguments `%1$d`, `%+u`, negative widths, string truncation by precision, `%f %e %g` with widths and precisions, `%p`, `%%`, and `curl_msnprintf`'s truncation to the buffer size (returns the length written, NUL-terminated). BL-2612 ports lib557 over this API, so the API must accept the C argument kinds lib557 passes (signed and unsigned 16-, 32- and 64-bit integers, double, string, pointer) with libcurl's semantics, not .NET's. Hand-written over the BCL (CLAUDE.md); new public API gets XML docs and an entry in `Libcurl/CLAUDE.md`.

## Acceptance criteria

- [ ] `Libcurl` has public `curl_m*printf` equivalents (`mprintf`, `mfprintf`, `msprintf`, `msnprintf`, `maprintf`) implementing `lib/mprintf.c` 8.22.0's conversions, flags, widths, precisions and positional arguments.
- [ ] `Libcurl.UnitTests` pins each conversion family with focused tests, including every `curl_msnprintf` check in `lib557.c`'s `test_string_formatting`, `test_pos_arguments`, `test_weird_arguments` and `test_float_formatting`.
- [ ] `Measure-CodeQuality.ps1 -Library Libcurl` reports 0 failing members for the new code.
- [ ] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.

## Notes

- Filed by BL-2531 (lane 1, 2026-10-17): lib557 cannot be ported until Libcurl has this API.

## Log

- 2026-10-17: Created.
