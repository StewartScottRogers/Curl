# ADR-1238 — LDAP values follow curl 8.22.0's base64 rule

- Status: Accepted
- Date: 2026-11-11
- Task: BL-4913
- Decided by Claude under Stewart's delegation.

## Context

`LdapEntryFormatter` wrote a value in base64 only when it started or ended with a blank or held
a control byte other than tab, line feed, vertical tab, form feed and carriage return, as BL-588
measured against curl 8.21.0 (WinLDAP) and 8.18.0 (OpenLDAP). The fitness review of 2026-11-11
(STD-010) found that curl 8.22.0 writes `a\nb`, `:colon` and `<lt` in base64, so a value holding
a line feed could no longer break the output or fake a `DN:` line.

BL-4913 measured curl 8.22.0 (Schannel, WinLDAP) with `Record-CurlExchange.ps1 -Script`
answering a bind and one entry: `a\nb` -> `:: YQpi`, `:colon` -> `:: OmNvbG9u`, `<lt` ->
`:: PGx0`, `a\tb` -> `:: YQli`, `a\rb` -> `:: YQ1i`, `a\vb` -> `:: YQti`, ` lead` ->
`:: IGxlYWQ=`, `trail ` -> `:: dHJhaWwg`, while `plain`, `a:b<` and the empty value stay as
they are.

## Decision

A value is written in base64 when its attribute name ends with `;binary` (unchanged), or when it
is not empty and starts with a space, a tab, `:` or `<`, ends with a space or a tab, or holds any
byte outside printable ASCII (0x20 to 0x7E). The rule is that of curl 8.22.0's
`ldap_value_needs_base64`, and both dialects apply it: the helper is curl's one rule for its LDAP
output, and the OpenLDAP build could not be measured from a Windows lane. Should a later
measurement of the OpenLDAP build differ, its dialect gets its own rule then.

## Consequences

Values with tab, line feed or carriage return no longer pass through raw, and output that LDIF
readers would misread (a leading `:` or `<`) is encoded, matching the reference build.
`LdapValueBase64RuleTests` pins the measured cases.
