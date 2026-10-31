# ADR-1000: A host character mapping outside the STD3 rules is refused only in a label that stays outside ASCII

Status: Accepted. Decided by Claude under Stewart's delegation (BL-4081, after BL-4076 and BL-4077).

## Context

UTS #46 maps some characters (`disallowed_STD3_mapped`) to text holding an ASCII character other than
a letter, digit, hyphen or dot: U+2100 to `a/c`, U+FE10 to `,`, U+00A0 to a space, U+FE70 to a space
and an Arabic mark. BL-4076 made `CurlUrlHostNameMapping.Map` refuse every such mapping. Measured with
curl 8.18.0 and libidn2 2.3.8 on WSL Ubuntu (`LC_ALL=C.utf8`, `--connect-to` a socat listener):

| Host | curl |
| --- | --- |
| `a℀b.test`, `a℁b.test`, `a℅b.test`, `a℆b.test` | sent, `Host: aa/cb.test`, `aa/sb.test`, `ac/ob.test`, `ac/ub.test` |
| `a︐b.test`, `a︳b.test`, `a⑴b.test`, `a／b.test` | sent, `Host: a,b.test`, `a_b.test`, `a(1)b.test`, `a/b.test` |
| `a b.test` (U+00A0, U+3000) | sent, `Host: a b.test` |
| U+FC5E..U+FC63, U+FDFA, U+FE70 between `a` and `b` | exit 3 |
| `a℀éb.test`, `a éb.test` (U+00A0), `a︐éb.test` | exit 3 |
| `a_éb.test` (a literal underscore) | sent, `Host: xn--a_b-cma.test` |
| `a℀.éb.test` | sent, `Host: aa/c.xn--b-9fa.test` |

libidn2 passes a label that maps to ASCII as it is, and refuses such a mapping only in a label it goes
on to convert to punycode; a literal ASCII character in that label is not refused.

## Decision

`Map` records where each mapping outside the STD3 rules lands and, once the whole name is mapped,
replaces its first character with U+FFFD (which `CurlUrlHost` refuses) only when the label it lands in
holds a character outside ASCII. Labels are split on `.` after mapping, as the punycode step splits them.

## Consequences

Hosts with U+2100, U+2101, U+2105, U+2106, U+FE10, U+FE33 and the other ASCII-only mappings now
connect as curl does; the Arabic ligatures and isolated forms, and any such mapping beside a character
outside ASCII, are still refused with exit 3. Through a proxy curl fails these hosts with exit 27;
that is BL-4080's.
