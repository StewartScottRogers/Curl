---
id: BL-2718
title: Send ;type=i and ;type=a on an FTP URL through an HTTP proxy for CURLOPT_PROXY_TRANSFER_MODE (upstream cases 549 and 550)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/TransferContextFactory.cs, Curl.Engine.UnitLibrary/ProxyTransferModeUrl.cs, Curl.Engine.UnitLibrary/CLAUDE.md, Curl.Engine.UnitTests/ProxyTransferModeUrlTests.cs, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-18
completed: 2026-10-19
---
# BL-2718 — Send ;type=i and ;type=a on an FTP URL through an HTTP proxy for CURLOPT_PROXY_TRANSFER_MODE (upstream cases 549 and 550)

## Goal

Upstream cases 549 and 550 pass and are on the ratchet.

## Context

Filed by BL-2610, which made the harness report the ftp feature. Case 549 expects the proxied request line `GET ftp://www.example.com/moo/549;type=i HTTP/1.1` and 550 `...550;type=a`, but Curl sends the URL with no `;type=` suffix when CURLOPT_PROXY_TRANSFER_MODE is set. Run with `CURL_UPSTREAM_CASE=549` / `550`. 561 already passes.

## Acceptance criteria

- [x] The cases pass through the runner and are added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`, each fix with a focused unit test in the fixed library's `.UnitTests` (ADR-0482).
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.

## Notes

- Touches narrowed from the whole Curl.Engine.UnitLibrary and Curl.Engine.UnitTests projects to the files the change lands in (ADR-0694): `TransferContextFactory.Create` puts the `;type=` suffix on a forwarded ftp URL through a new `ProxyTransferModeUrl`. The narrowing was not published early: this lane may not push, so it lands with the task's commit.
- Fix: `ProxyTransferModeUrl.Of` follows curl 8.22.0's `http_target` - under `CURLOPT_PROXY_TRANSFER_MODE`, an `ftp` URL forwarded through an `Http`/`Http10` proxy without a tunnel gets `;type=a` (`CURLOPT_TRANSFERTEXT`) or `;type=i` at the end of its path unless the path already ends in `;type=` plus one of a, d or i. Default taken: the suffix goes on the context's URL, so the request line, and also anything else the HTTP handler derives from the forwarded URL (such as `%{url_effective}`), carry it; upstream keeps it off the effective URL, which no case here checks, and fixing that would need `Curl.Protocol.Http.UnitLibrary`, outside `touches`.
- Measured: `CURL_UPSTREAM_CASE=549` and `550` pass through the runner; both added to the ratchet. `ProxyTransferModeUrlTests` (21 tests). Fast tests all green (Conformance 3559 passed). `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary`: 0 failing members, after splitting `Of` (it measured complexity 14 at first) and covering the guessed-scheme branch.

## Log

- 2026-10-18: Created.
- 2026-10-19: Backlog -> Doing. Criteria at claim: e6f6390a 4272a6b9.
- 2026-10-19: Doing -> Done. Upstream cases 549 and 550 pass: CURLOPT_PROXY_TRANSFER_MODE sends ;type=i or ;type=a on an FTP URL forwarded through an HTTP proxy; effective URL follow-up filed as BL-2792
