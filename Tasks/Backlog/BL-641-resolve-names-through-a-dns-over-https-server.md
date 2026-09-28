---
id: BL-641
title: Resolve names through a DNS-over-HTTPS server
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-640]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-641 — Resolve names through a DNS-over-HTTPS server

## Goal

A `DohDnsResolver` implementing `IDnsResolver` resolves a host by POSTing BL-640's queries to the DoH URL over the connector and TLS provider as BL-639's ADR decides, and returns the addresses (or curl 8.21.0's failure, exit 6 with its message) without ever using the system resolver for the target host.

## Context

- Conformance audit 2026-09-28, row 27. Design: BL-639's ADR; codec: BL-640.
- The DoH server's own host name is resolved with the system resolver (as curl does); inject both so tests need no network.

## Acceptance criteria

- [ ] `Curl.Networking.UnitTests` with fake connector and TLS provider pin the HTTP request bytes BL-639 recorded, the addresses returned for A and AAAA answers, and exit 6 with the measured message for a `500`, an NXDOMAIN and a malformed answer.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
