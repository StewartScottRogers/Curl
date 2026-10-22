---
id: BL-3012
title: Amend ADR-0738 with curl 8.22.0's measured DoH decode and trace behaviour (CNAME skipped, NXDOMAIN, empty answer, HTTP 500)
priority: Normal
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions/ADR-0738-doh-answers-decode-nxdomain-and-cap-ttls-as-curl-8-22-0-does.md]
model: sonnet
requirement: none
created: 2026-10-21
completed:
---
# BL-3012 — Amend ADR-0738 with curl 8.22.0's measured DoH decode and trace behaviour (CNAME skipped, NXDOMAIN, empty answer, HTTP 500)

## Goal

ADR-0738 records curl 8.22.0's measured `-v --trace-config doh` lines and exit codes for the DoH answers Curl now decodes the same way, so the code and its tests can cite one decision.

## Context

Split from BL-2970. The measurements were already taken with the local curl 8.22.0 (Schannel, debug build) through `Record-CurlExchange.ps1` and are written in the Notes of `Tasks/Deferred/BL-2970-*.md` ("Measured" and "Decided"). This task only turns them into an amendment of `Documentation/Planning/Decisions/ADR-0738-doh-answers-decode-nxdomain-and-cap-ttls-as-curl-8-22-0-does.md`; it changes no code. The code that follows the amendment is BL-3011.

State in the amendment, marked "Decided by Claude under Stewart's delegation", for curl 8.22.0 (tag `curl-8_22_0`, `lib/vdns/doh.c`):

- After `* [DoH] Connection #1 to host 127.0.0.1:P left intact`: an A answer gives `* [DoH] DoH request complete, 0 to go` and `* Host example.test:18454 was resolved.`; an NXDOMAIN answer gives `* [DoH] [DoH] [A] error decoding response: No such name` and exit 6; a CNAME-only answer and an empty answer give `* [DoH] DoH request complete, 0 to go` and exit 6 (`Could not resolve host`); a 500 with an empty body gives `* [DoH] [DoH] [A] error: HTTP status 500` and exit 6.
- Without `-4` the AAAA probe is queried first (`Connection #1 is not open enough, cannot reuse`, then the `[AAAA]` error, then connection #2 and the `[A]` error). Plain `-v` writes no DoH line.
- The decisions: the decoder skips CNAME and DNAME data unread and never returns `NoContent` (the enum value stays, documented as unused, so `DnsMessageFailureText`'s value-indexed table keeps its places); `DnsAnswer.CanonicalNames` and `DohQueryResult`'s CNAME room are removed; `DohTraceLines.FailureLine` writes the decode and transfer errors on the DoH trace sink; the `[DoH] ` prefix, `DoH request complete` wording and AAAA-first order are BL-2973, and the non-2xx `HTTP status N` failure is BL-2972.

## Acceptance criteria

- [ ] ADR-0738 has a dated amendment section stating each measured line and exit code listed above, the curl version (8.22.0), how it was measured, and the decisions, and it names BL-3011, BL-2972 and BL-2973 as the tasks that apply it.
- [ ] The ADR still says "Decided by Claude under Stewart's delegation", and intent that BL-3011 has not yet landed is written as intent, so no statement contradicts the code as it is now.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean (no code changed).

## Notes

## Log

- 2026-10-21: Created.
