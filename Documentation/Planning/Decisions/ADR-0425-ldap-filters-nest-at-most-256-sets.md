# ADR-0425 — An LDAP URL's filter nests at most 256 sets; a deeper one is refused as a bad filter

- Status: Accepted
- Date: 2026-10-07
- Task: BL-1661
- Decided by Claude under Stewart's delegation.

## Context

`LdapFilterEncoder` parses a filter by recursive descent: `ParseSet` -> `ParseParenthesized`
-> `ParseAfterParenthesis` -> `ParseSet`, three frames for each `(&`, `(|` or `(!` level,
with no limit. `"(&"` repeated a million times in an LDAP URL (easily passed through `-K`,
which has no command-line length limit) overflows the stack, and a .NET stack overflow ends
the process without any catch, exit code or message curl would give.

The reference builds' parsers recurse too: OpenLDAP's `ldap_pvt_put_filter` calls
`put_complex_filter`, which calls `ldap_pvt_put_filter` again, with no depth check, and
WinLDAP's parser is not documented to bound it. A filter deep enough to overflow their stacks
crashes them too, so there is no behaviour of real curl to measure and match for a
million-level filter: a crash is not a contract. No real directory filter nests more than a
handful of levels.

## Decision

`LdapFilterEncoder.MaximumSetDepth` is 256. `Encode` counts the sets that enclose the
position; opening a 257th refuses the filter (`null`), so the transfer fails exactly as it
does for any filter the build refuses. Depth, not the number of sets, is counted: any number
of shallow sibling sets still encodes. Both dialects share the limit.

## Why

- A limit keeps the parser and its tests as they are; an iterative parser would rewrite the
  whole encoder for no observable gain.
- 256 is past anything a real filter nests and past the 200 levels BL-1512's adversarial test
  pins, while 256 levels of three frames each stay far inside the smallest stack .NET gives a
  thread (512 KiB on macOS secondary threads), in Debug test hosts as well as the native build.
- Refusing as a bad filter gives the same exit code and message a filter the reference
  parser rejects gets, rather than a new failure mode.
