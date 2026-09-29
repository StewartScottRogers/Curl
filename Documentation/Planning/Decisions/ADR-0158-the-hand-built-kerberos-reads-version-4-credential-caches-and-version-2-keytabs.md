# ADR-0158 — The hand-built Kerberos reads version 4 credential caches and version 2 keytabs, through a file seam, and fails with a typed error

- **Status:** Accepted
- **Date:** 2026-09-28

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-688.

## Context

ADR-0142 routes Negotiate, SASL `GSSAPI` and SOCKS5 GSS-API off Windows to the system
GSS-API library, and to the hand-built Kerberos in `Curl.Kerberos.UnitLibrary` only when
that library answers `Unsupported`. The hand-built route must then find the ticket the
user got with `kinit` as MIT does: `KRB5CCNAME`, else `FILE:/tmp/krb5cc_<uid>`. BL-688
reads the `FILE:` cache and keytab files; `DIR:` and `KCM:` are BL-789's.

MIT's formats have versions: credential cache 1 to 4 (1 and 2 in the host's byte order,
3 big-endian without a header, 4 big-endian with a tagged header), keytab 1 (host byte
order) and 2 (big-endian). Recorded on 2026-09-28 with MIT Kerberos 1.22.1 against a
local KDC (`Curl.Kerberos.UnitTests\RecordedKerberosFiles.cs`): `kinit` writes cache
version 4 (`05 04`) with a `DeltaTime` header tag and a `fast_avail` configuration entry
(server realm `X-CACHECONF:`) before the TGT, and `kadmin.local ktadd` writes keytab
version 2 (`05 02`) with the trailing 32-bit key version number.

## Decision

- **Cache version 4 only, keytab version 2 only.** Every MIT release since 1.2 writes
  cache version 4 by default and every release since 1.0 writes keytab version 2;
  Heimdal writes the same two. Anything else is `KerberosFileError.UnknownVersion`. A
  host-byte-order file cannot be told apart from a big-endian one without guessing, and
  no current tool writes one.
- **Configuration entries are kept, and marked.** `CachedCredential.IsConfigurationEntry`
  is true for MIT's `X-CACHECONF:` entries; the reader returns every credential in file
  order and leaves choosing a ticket to its caller, as MIT's cursor does.
- **MIT's keytab reading rules.** An entry size of zero, or fewer than four bytes left,
  ends the keytab; a negative size is a hole, skipped; the trailing 32-bit key version
  number replaces the 8-bit one when present and not zero.
- **Names split as MIT splits them.** The type is everything before the first colon,
  matched case-sensitively; a name with no colon is a `FILE` path. The single-letter
  drive-letter rule MIT applies on Windows is not reproduced, because ADR-0142 never
  takes the hand-built route on Windows. The credential cache reads only `FILE`, the
  keytab `FILE` and `WRFILE`; any other type is `KerberosFileError.UnsupportedType` until
  its task adds it (BL-789 for `DIR` and `KCM`).
- **Defaults.** The cache is `KRB5CCNAME` when set and not empty, else
  `FILE:/tmp/krb5cc_<uid>` with the uid from an injected `Func<uint>`; the keytab is
  `KRB5_KTNAME`, else `FILE:/etc/krb5.keytab`. `krb5.conf`'s `default_ccache_name` and
  `default_keytab_name` are not read here: `krb5.conf` is BL-689's, and its caller passes
  its answer in by setting the name to read.
- **Seams and failures.** Files come through `IKerberosFileReader.ReadAllBytes`, which
  returns `null` for a missing file (`KerberosFileError.NotFound`); environment variables
  through `Func<string, string?>`, as `DefaultConfigFileSearch` takes them. Every failure
  is a `KerberosFileException` whose `Error` names it (`Truncated`, `UnknownVersion`,
  `NotFound`, `UnsupportedType`), so the caller maps it to curl's exit code. The file
  bytes are zeroed once parsed, and `CredentialCache` and `Keytab` zero their keys on
  `Dispose`.

## Consequences

- A cache written by `kinit` or a keytab written by `ktutil` or `kadmin` on any current
  MIT or Heimdal installation reads; a pre-2000 file does not, with a typed failure.
- The Kerberos client that picks a ticket from the cache skips configuration
  entries itself.

## Alternatives considered

- **Read cache versions 1 to 3 as well.** Rejected: nothing current writes them, and
  versions 1 and 2 need a byte-order guess.
- **Drop configuration entries while reading.** Rejected: MIT's own cursor returns them,
  and a later task may want `fast_avail` or `pa_type`.
