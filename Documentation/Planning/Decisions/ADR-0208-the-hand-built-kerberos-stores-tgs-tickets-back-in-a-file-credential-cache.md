# ADR-0208 — The hand-built Kerberos stores TGS tickets back in a file credential cache as MIT does

- **Status:** Accepted
- **Date:** 2026-09-29

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-825.

## Context

ADR-0168's `KerberosKdcClient` reads the credential cache and, when it holds no live
ticket for the service, gets one by a TGS exchange with the cache's ticket-granting
ticket. It never wrote that ticket back, so every request repeated the exchange. MIT's
`gss_init_sec_context` goes through `krb5_get_credentials`, whose `tkt_creds` state
machine stores the new ticket with `krb5_cc_store_cred` so the next request finds it.
For a `FILE:` cache, MIT's `cc_file.c` stores by appending the credential, in the
file's version 4 layout, after the ones already there.

## Decision

- **Append, never rewrite.** `CredentialCacheStore.Store` marshals the credential with
  `CredentialCacheWriter` (the exact mirror of `CredentialCacheReader`) and appends it to
  the cache file through a new injected `IKerberosFileWriter`, as `cc_file.c` does. The
  file must exist; a missing file is `NotFound` and is not created (MIT's
  `KRB5_FCC_NOFILE`).
- **Which caches.** `FILE:` (or a bare path) and `DIR:` (the collection's primary cache or
  `DIR::path`, resolved as reading resolves it) are files and are written. `KCM:` and any
  other type are `UnsupportedType`. A store with no writer is the new
  `KerberosFileError.NotWritable`, so the writer stays an optional constructor parameter
  and existing callers compile unchanged.
- **What is stored**, as MIT's `krb5_kdcrep2creds` fills it: client, server, session key,
  authentication, start, end and renew-until times, flags and the reply's client
  addresses; no authorization data, no second ticket, not user-to-user. A start time the
  KDC left out is stored as the authentication time; a ticket that is not renewable
  renews until the Unix epoch. The ticket is the DER encoding of the ticket received.
- **Where it happens.** A new `KerberosKdcClient.GetServiceTicketAsync(server, store,
  cacheName, ...)` reads the cache, answers from a live cached ticket without writing, and
  stores a ticket got by TGS before returning it. The overload taking an in-memory
  `CredentialCache` keeps its read-only behaviour. Tickets from a password (the SSPI-like
  explicit-credential path) are not stored: no cache was named.
- **A failed store is ignored.** MIT's `tkt_creds` calls `(void) krb5_cc_store_cred`:
  the ticket is good whether or not the cache takes it. Every `KerberosFileException`
  from the store is swallowed and the ticket returned.
- **No leftover secrets.** The marshalled bytes are measured first and written into one
  exactly-sized array, zeroed after the append, so no grown buffer holding a session key
  is left behind.

## Consequences

- A second request for the same service answers from the cache with no KDC exchange,
  and `klist` shows the service ticket as it would after MIT's own client.
- The `Curl.Authentication.UnitLibrary` source and `Curl.Console` still call the
  read-only overload; switching them to the storing one with a disk-backed writer is
  follow-up work. Storing in a `KCM:` cache (`KCM_OP_STORE`) is follow-up work too.

## Alternatives considered

- **Rewrite the whole file.** Simpler to reason about, but not what MIT does, and a
  concurrent `kinit` could be lost between read and write. Rejected.
- **Fail the request when the store fails.** Stricter than MIT and turns a read-only
  cache into an authentication failure. Rejected.
