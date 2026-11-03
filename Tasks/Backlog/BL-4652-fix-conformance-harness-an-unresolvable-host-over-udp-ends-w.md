---
id: BL-4652
title: Fix conformance harness: an unresolvable host over UDP ends with exit 6
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.Extended.UnitLibrary/Harness/ReferenceDatagramConnector.cs, Curl.Conformance.Extended.UnitTests/Harness, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-02
completed:
---
# BL-4652 — Fix conformance harness: an unresolvable host over UDP ends with exit 6

## Goal

A UDP transfer to a host under .invalid in the conformance harness ends with exit 6 and `curl: (6) Could not resolve host: <host>`, as a TCP one does (BL-4619).

## Context

Found by BL-4612: `ReferenceDatagramConnector.OpenAsync` ignores its host, so `coap`, `coaps`, `dns` and `webtransport` to `nohost.invalid` end with `curl: (7) No reference server answers UDP port N`. The TCP dialer's resolver fails `.invalid` names (BL-4619); the datagram connector must do the same before it looks for a route. Listed in KnownFailures.txt as CurlSurface:unresolvable-host-{coap,coaps,dns,webtransport}-1.

## Acceptance criteria

- [ ] The four CurlSurface:unresolvable-host-{coap,coaps,dns,webtransport}-1 cases in `CurlSurface/Transfer/UrlErrors` pass and their KnownFailures.txt lines are removed.
- [ ] A focused unit test in Curl.Conformance.Extended.UnitTests/Harness pins the datagram connector's answer for an .invalid host (ADR-0482).
- [ ] Curl.Conformance.Extended.UnitLibrary stays at 100% line and branch coverage; build clean and fast tests green.

## Notes

## Log

- 2026-11-02: Created.
