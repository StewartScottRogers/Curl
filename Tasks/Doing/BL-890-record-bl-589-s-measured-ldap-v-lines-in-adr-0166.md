---
id: BL-890
title: Record BL-589's measured LDAP -v lines in ADR-0166
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-589]
touches: [Documentation/Planning/Decisions/ADR-0166-ldap-is-hand-built-on-iconnection-and-answers-as-each-platforms-winldap-or-openldap-curl-build.md]
requirement: none
created: 2026-09-29
completed:
---
# BL-890 — Record BL-589's measured LDAP -v lines in ADR-0166

## Goal

ADR-0166 has a "Measured by BL-589" section stating the `-v` lines each LDAP build writes and the choices BL-589 made where it could not measure, and its follow-up list no longer names BL-589 as future work.

## Context

- BL-589 (Done) registered `ldap`/`ldaps` in `Curl.Console` and added `LdapVerboseLines` in `Curl.Protocol.Ldap.UnitLibrary`. Its Notes hold the measured stderr and the decisions; it could not edit ADR-0166 because BL-883 held `Documentation/Planning/Decisions` at the time.
- ADR-0166's `## Consequences` list (around line 330) still describes BL-589 as pending.

## Acceptance criteria

- [ ] ADR-0166 has a `## Measured by BL-589` section, after `## Measured by BL-845`, copying BL-589's measured `-v` lines for both builds (WinLDAP: vendor, URL and connection-kind lines, `shutting down connection #N`; OpenLDAP: URL line once bound, `Connection #N ... left intact` on success, `closing connection #N` on failure, `closing connection #-1` for a URL refused before connecting).
- [ ] The section records, marked "Decided by Claude under Stewart's delegation", the unmeasured choices from BL-589's Notes: the WinLDAP `ldaps` success lines, the URL-line normalization for untried URL forms, and which failure messages are not written as `-v` lines.
- [ ] The BL-589 line under `## Consequences` says the handler is registered, in the present tense.

## Notes

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
