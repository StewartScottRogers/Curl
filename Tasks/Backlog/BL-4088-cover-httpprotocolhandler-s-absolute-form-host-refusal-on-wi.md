---
id: BL-4088
title: Cover HttpProtocolHandler's absolute-form host refusal on Windows
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Protocol.Http.UnitTests/HttpProtocolHandlerAbsoluteFormHostTests.cs, Curl.Protocol.Abstractions.UnitLibrary/Curl.Protocol.Abstractions.UnitLibrary.csproj]
requirement: none
created: 2026-10-29
completed:
---
# BL-4088 — Cover HttpProtocolHandler's absolute-form host refusal on Windows

## Goal

`HttpProtocolHandler.AbsoluteFormTargetUnbuilt` and the branch in `ExchangeOnConnectionAsync` that calls it are covered by a fast test that runs on Windows too.

## Context

BL-4080 made a proxied transfer whose wire host holds a character `curl_url_set` refuses (`a℀b.test` is `aa/cb.test` off Windows) fail with exit 27. On Windows `CurlUrlHost` converts a non-ASCII host from its bytes read as Windows-1252 (`HostAsWindows1252`), which leaves no slash (`xn--ab-pia8903akua.test`), so the end-to-end test `ExecuteAsync_ProxiedHostHoldingASlash_FailsWithOutOfMemoryAndSendsNothing` is excluded on Windows and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Http.UnitLibrary` on Windows reports `AbsoluteFormTargetUnbuilt` at 0% and `ExchangeOnConnectionAsync` lines 1087-1088 uncovered. One way: let `Curl.Protocol.Http.UnitTests` build a `CurlUrl` read with UTF-8 host rules on any platform (an `InternalsVisibleTo` for the internal `TryParse` overloads, or a public test-facing parse that names the platform rule), then drop the `OSCondition` or add a Windows row.

## Acceptance criteria

- [ ] On Windows, `Measure-CodeQuality.ps1 -Library Curl.Protocol.Http.UnitLibrary` reports no uncovered line in `AbsoluteFormTargetUnbuilt` or `ExchangeOnConnectionAsync`.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Filed by BL-4080's run.

## Log

- 2026-10-29: Created.
