# ADR-0160 — The hand-built Kerberos reads krb5.conf as MIT's profile library does and locates KDCs from the file, then from SRV records

- **Status:** Accepted
- **Date:** 2026-09-28

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-689.

## Context

The hand-built Kerberos (ADR-0142's fallback route, ADR-0158's files) needs the user's
realm, the realm's KDCs and a few client defaults before it can send an AS or TGS
request. MIT Kerberos keeps them in `krb5.conf`, parsed by its profile library
(`prof_parse.c`), and locates KDCs in `locate_kdc.c`: the realm's `kdc` entries first,
then DNS SRV records (RFC 4120 section 7.2.3.2).

## Decision

- **Files as MIT finds them.** `KRB5_CONFIG` split on colons (empty parts dropped), else
  `/etc/krb5.conf`. A missing file is skipped and no file at all is an empty
  configuration, as `krb5_init_context` falls back to an empty profile. Earlier files
  win for single-valued relations. MIT for Windows' `krb5.ini` location is not
  reproduced: ADR-0142 never takes the hand-built route on Windows.
- **Parsed as `prof_parse.c` parses.** Lines before the first `[section]` (which must
  start in column one) are ignored. `#` and `;` start a comment only at the start of a
  line; text after a value is part of the value. `include FILE` and `includedir DIR`
  work only at the very start of a line, and each included file starts afresh, ignoring
  lines before its first section. `includedir` reads, in ordinal order, only names that
  end in `.conf` or are made wholly of letters, digits, `-` and `_`, and never a name
  starting with `.`. Quoted values unescape `\n`, `\t`, `\b` and any other escaped
  character as itself. A `*` final mark on a tag or section is dropped. A section or
  group that appears twice is merged.
- **A malformed line fails the whole file,** as MIT's does, with a typed
  `KerberosConfigurationError` named after MIT's code (`SectionSyntax`, `SectionNotTop`,
  `ExtraClosingBrace`, `RelationSyntax`, `MissingOpeningBrace`, `IncludeFileNotFound`,
  `IncludeDirectoryNotFound`) and the file and line in the message. Includes nest at most
  five deep (`TooManyIncludes`), which also ends an include loop.
- **Relations and their defaults.** `default_realm` (none); `dns_lookup_kdc`, else
  `dns_fallback`, else true; `udp_preference_limit` 1465 when unset, unparsable or
  negative, and at most 32700; `permitted_enctypes` `DEFAULT` when unset;
  `default_tkt_enctypes` the permitted list when unset, as MIT 1.18 and later default
  it. Booleans take MIT's words case-insensitively (`y yes true t 1 on`,
  `n no false nil 0 off`), anything else being unset; integers are `strtol` base 0 in
  `int` range. Encryption type lists are split on whitespace and commas and kept as
  written: expanding `DEFAULT`, family names and `-`/`+` edits belongs with the
  encryption types themselves.
- **Host to realm.** The host, lower-cased and without a trailing dot, is looked up in
  `[domain_realm]`, then each suffix from each dot (`.example.com`, `example.com`, ...),
  as MIT's profile host-realm module does; failing that, `default_realm`. MIT's
  referral realm, `realm_try_domains` and DNS `TXT` lookup are not reproduced here: the
  client asks its own realm's KDC, which refers it, as `curl` built against MIT does in
  practice.
- **KDC entries.** An optional `udp/`, `tcp/` or `https://` (MS-KKDCP, port 443, with an
  optional `/path`) prefix, then `host`, `host:port`, `[address]`, `[address]:port` or a
  bare IPv6 address (more than one colon); the port defaults to 88 and must be 1 to 65535
  in decimal. A malformed entry fails the whole lookup (`InvalidKdcAddress`), as MIT's
  `EINVAL` does. An entry without a prefix is `UdpOrTcp`, left to the sender and
  `udp_preference_limit`.
- **SRV records through a seam.** When the realm has no `kdc` entry and
  `dns_lookup_kdc` allows, `IKerberosSrvLookup` is asked for `_kerberos._udp.REALM`, then
  `_kerberos._tcp.REALM`; UDP records come first. Each set is ordered by priority
  ascending, then weight descending, deterministically rather than RFC 2782's weighted
  random pick, so a recorded answer always gives the same order. A target of `.` means
  no service and is dropped. MIT 1.15's URI records (`_kerberos.REALM`) are not queried;
  SRV is what RFC 4120 names and every KDC deployment publishes.

## Consequences

- A distribution's stock `krb5.conf` with `includedir /etc/krb5.conf.d/` reads the same
  relations MIT reads, and a file MIT rejects is rejected with the same reason.
- `IKerberosFileReader` gained `ListFileNames` for `includedir`; the SRV implementation
  over the hand-built DNS client (BL-694) is composed where BL-527 wires the route.

## Alternatives considered

- **Skip malformed lines and read the rest.** Rejected: MIT fails the context, so a
  lenient parse would authenticate where `kinit` and system-GSS `curl` refuse.
- **Weighted random SRV ordering.** Rejected for now: it needs an injected random source
  for a benefit (load spreading) that a single client's handful of requests does not
  show, and it makes recorded tests order-dependent.
