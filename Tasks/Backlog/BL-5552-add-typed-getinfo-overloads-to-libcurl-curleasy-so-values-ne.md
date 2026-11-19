---
id: BL-5552
title: Add typed GetInfo overloads to Libcurl CurlEasy so values need no unboxing
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Libcurl/Easy/CurlEasy.cs, Libcurl/Info/EasyInfoDispatch.cs]
requirement: none
created: 2026-11-19
completed:
---
# BL-5552 — Add typed GetInfo overloads to Libcurl CurlEasy so values need no unboxing

## Goal

A `Libcurl` port reads `curl_easy_getinfo` values into typed variables, as C does, without unboxing an `object?`.

## Context

From bug report `2026-11-19T0642-Curl-bug-report.md`, finding 3 (Low), second bullet. Build tested: commit `0afbd10e3b`, `Libcurl 0.2.0`.

`CurlEasy.GetInfo(CurlInfo info, out object? value)` (`Libcurl/Easy/CurlEasy.cs:93`) returns the value boxed, so every read needs a cast such as `(long)code!`. C reads `curl_easy_getinfo` into a typed variable (`long`, `double`, `char *`, `curl_off_t`, `struct curl_slist *`, `curl_socket_t`). `CurlMulti.GetInfo` already has a typed `out long` form.

Add typed overloads (for example `out long`, `out double`, `out string?`, and the slist and certinfo types `Libcurl` uses), each returning `CurlCode.BadFunctionArgument` when the info's type does not match, as libcurl does for a wrong `CURLINFO_*` type. A generic `GetInfo<T>` is the alternative; choose one and say why in the Notes. Keep the `object?` overload. The dispatch is in `Libcurl/Info/EasyInfoDispatch.cs`.

## Acceptance criteria

- [ ] `CurlEasy` has typed `GetInfo` overloads (or `GetInfo<T>`) covering every `CurlInfo` value type, and a port reads `CurlInfo.ResponseCode` into a `long` with no cast.
- [ ] A typed call whose type does not match the info returns `CurlCode.BadFunctionArgument` and does not throw.
- [ ] The `object?` overload still works unchanged.
- [ ] Focused unit tests in `Libcurl.UnitTests` pin each value type and the mismatch case; `Libcurl` stays at the four quality gates.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-19: Created from bug report 2026-11-19T0642-Curl-bug-report.md, finding 3 (GetInfo).
