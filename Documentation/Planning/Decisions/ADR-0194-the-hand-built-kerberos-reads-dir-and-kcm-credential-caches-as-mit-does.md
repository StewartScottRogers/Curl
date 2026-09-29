# ADR-0194 — The hand-built Kerberos reads DIR and KCM credential caches as MIT does

- **Status:** Accepted
- **Date:** 2026-09-29

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-789.
Extends ADR-0158 (version 4 `FILE:` caches) to the other cache types ADR-0142 assigns to
the hand-built route.

## Context

ADR-0142 sends Negotiate and Kerberos off Windows to the system GSS-API library when it
answers and to Curl's own code when it answers `Unsupported`; that code must find a
`kinit` ticket in a `FILE:`, `DIR:` or `KCM:` cache. BL-789 taught `CredentialCacheStore`
(`Curl.Kerberos.UnitLibrary`) the last two. MIT's `cc_dir.c` and `cc_kcm.c` are the
reference; several edges had to be settled.

## Decision

- **`DIR:/d`** reads `/d/primary` as MIT's `read_primary_file` does: the first line must
  end in a newline and name a file starting `tkt`. A `/` in it is refused as well, so
  `primary` cannot point outside the directory. Anything else is
  `KerberosFileError.DirectoryPrimaryMalformed` (MIT's `KRB5_CC_FORMAT`). No `primary`
  file means `tkt`, as in MIT. **`DIR::/d/tkt2`** reads that file directly. The chosen
  file is read as a `FILE:` cache.
- **`KRB5CCNAME` unset** falls back to `krb5.conf`'s `[libdefaults] default_ccache_name`,
  with `%{uid}` and `%{euid}` expanded to the user id (curl never runs setuid, so they are
  equal), then to `FILE:/tmp/krb5cc_<uid>`. Other MIT path tokens are left as written;
  none is common in a `default_ccache_name`.
- **`KCM:`** connects to `[libdefaults] kcm_socket`, else
  `/var/run/.heim_org.h5l.kcm-socket`; a `kcm_socket` of `-` turns KCM off, as MIT. It
  sends MIT's `GET_DEFAULT_CACHE` (for an empty residual), `GET_PRINCIPAL`,
  `GET_KDC_OFFSET` (a refusal means no offset), `GET_CRED_UUID_LIST` and
  `GET_CRED_BY_UUID`, framed as `cc_kcm.c` frames them: a 4-byte big-endian length,
  protocol version 2.0 and a 16-bit opcode.
- **KCM failures are typed, never thrown past the store:** unreachable, `-` or no
  connector is `KcmNotRunning`; a non-zero status is `KcmFailed` carrying `KcmStatus`; a
  malformed frame, including a reply over MIT's 10 MiB `MAX_REPLY_SIZE`, is
  `KcmReplyMalformed`.
- The KCM connector and the configuration are optional constructor parameters, so
  existing callers compile unchanged; BL-527 composes the Unix-socket connector
  (`Curl.Networking.UnitLibrary`) and passes `krb5.conf`. The library opens no socket or
  file itself (ADR-0120).

## Consequences

- A ticket from `kinit` is found in every cache type ADR-0142 gives the hand-built route,
  and every path is tested with fakes: no disk, no daemon.
- `KEYRING:` and macOS `API:` still go to the system library, as ADR-0142 says.

## Alternatives considered

- **Trust `primary` as written, `/` included.** MIT does not check for `/`, but a name
  with one leaves the collection; refusing it costs nothing for a cache `kinit` wrote.
  Rejected.
- **Expand every MIT path token.** More code for tokens (`%{TEMP}`, `%{LIBDIR}` and the
  like) that do not appear in real `default_ccache_name` settings. Rejected until a case
  needs one.
