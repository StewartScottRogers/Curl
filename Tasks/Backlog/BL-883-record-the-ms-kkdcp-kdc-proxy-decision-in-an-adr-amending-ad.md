---
id: BL-883
title: Record the MS-KKDCP KDC proxy decision in an ADR amending ADR-0168
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-827]
touches: [Documentation/Planning/Decisions]
requirement: none
created: 2026-09-29
completed:
---
# BL-883 — Record the MS-KKDCP KDC proxy decision in an ADR amending ADR-0168

## Goal

A new ADR in `Documentation/Planning/Decisions`, marked "Decided by Claude under Stewart's delegation", records BL-827's MS-KKDCP design and amends ADR-0168's "`https://` KDCs are skipped" line.

## Context

- BL-827 could not write the ADR itself: BL-594 held `Documentation/Planning/Decisions` in `touches` at the time. The decisions are in BL-827's Notes (in `Tasks/Done` or its archive); copy them faithfully:
  - A separate optional seam `IKerberosKdcProxyTransport` (not a new member of `IKerberosKdcTransport`), passed to `KerberosKdcClient` as an optional last constructor argument; without it `https://` KDCs are skipped as before.
  - `KDC-PROXY-MESSAGE` encoded as MIT's `encode_krb5_kkdcp_message` fills it in `sendto_kdc.c`: `kerb-message` holds the TCP-framed request, `target-domain` is the realm as a GeneralString, no `dclocator-hint`.
  - A proxy reply that is not a `KDC-PROXY-MESSAGE` (or whose length prefix does not match) is an `IOException`, so the realm's next KDC is tried, as MIT does.
- Amend ADR-0168 line 65 to point at the new ADR.

## Acceptance criteria

- [ ] The new ADR exists, states the three decisions above, and cites BL-827 and MIT `sendto_kdc.c`.
- [ ] ADR-0168 names the new ADR where it said `https://` KDCs are skipped, and the Decisions `README.md` index lists it.

## Notes

## Log

- 2026-09-29: Created.
