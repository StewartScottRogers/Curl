# ADR-1346: An Azure Storage numeric host is an emulator IP only when it is a routable IPv4 spelling (BL-5867)

Status: Accepted. Decided by Claude under Stewart's delegation.

## Context
EXT-105 made every `0x...` and all-digit host an emulator (path-style) host, so valid account names such as `0xstore`, `12345678` and `123` were sent bare, with the bearer token, to a host that resolves by DNS search suffix (EXT-117).

## Decision
A host is a numeric IPv4 spelling only when it parses as one to four dot-separated parts (decimal, `0x` hex or leading-0 octal), each leading part at most 255, the last part filling the remaining bytes, and the first octet is not 0. `2130706433`, `0x7f000001` and `0177.0.0.1` stay emulator hosts; `0xstore`, `12345678` (0.188.97.78) and `123` map to the public suffix. The first-octet rule is what separates `12345678`, which the finding requires to be an account, from `2130706433`, which EXT-105 pins as an address; 0.x.x.x is never a host a token should go to.

## Consequences
A real account whose digits spell a routable address (such as 2130706433) is read as an address; that is the same ambiguity real DNS has.
