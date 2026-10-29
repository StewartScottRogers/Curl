# ADR-1034: The OAuth2 extension keeps tokens in a JSON cache file beside curl's per-user files

- Status: Accepted
- Date: 2026-10-29
- Decided by Claude under Stewart's delegation (BL-4209)

## Context

The OAuth2 extension (ADR-1025) must not run a flow for every command line: it keeps the tokens it
gets on disk, gives back one that has not expired and refreshes one that has (RFC 6749 §6). Upstream
curl has no such cache, so there is no curl behaviour to match; the location, the format and the key
are ours to choose, using only the base class library.

## Decision

- **Location.** The file is `curl-oauth2-tokens.json` in a directory the caller names. The default
  directory (`OAuth2TokenCacheLocation.FindDefaultDirectory`) is the first set, non-empty one of
  `CURL_HOME`, `XDG_CONFIG_HOME`, `HOME`, `APPDATA`, `USERPROFILE`: the variables curl searches for
  its own per-user `.curlrc`, so the cache sits beside curl's own per-user files. The directory is
  created when missing.
- **Format.** UTF-8 JSON written with `Utf8JsonWriter` and read with `JsonDocument` (no reflection,
  so Native AOT holds): `{"version":1,"entries":[...]}`, each entry with every member present,
  `null` where it has no value: `token_endpoint`, `client_id`, `scope`, `access_token`,
  `token_type`, `expires_at` (Unix seconds) and `refresh_token` and `granted_scope`. A missing,
  unreadable or corrupt file, another version, or any entry with a member missing or of the wrong
  type reads as an empty cache: a cache is never a reason to fail a transfer, the flow simply runs.
- **Writing.** The file is written whole to a temporary file beside it and moved over it
  (`File.Move` with overwrite), so a reader never sees half a file. Off Windows the temporary file
  is created `0600` (`FileStreamOptions.UnixCreateMode`), as tokens are secrets; on Windows the
  per-user directory's inherited ACL already keeps other users out. A failed write returns `false`,
  leaves the old file and deletes the temporary one.
- **Key.** Token endpoint URI, client ID and the scope asked for (not the scope granted), so the
  same command line finds its own token and a different scope or client never borrows one.
- **Expiry.** `expires_in` (§5.1) is turned into an absolute `expires_at` by the injected
  `TimeProvider` when stored, capped at 100 years; a token counts as expired 30 seconds early, so
  it is not sent as it lapses. A token the server gave no lifetime lasts until the caller removes
  it, such as after a resource server refuses it.
- **Refresh.** An expired token with a refresh token is refreshed (§6) with the scope of its key
  through the extension's own HTTP client (ADR-1030). A 200 with a token response replaces the
  entry, keeping the old refresh token unless the response sends a new one. An `invalid_grant`
  error, or an expired token with no refresh token, drops the entry so the flow runs again. Any
  other failure (a transport failure, another error, an unreadable body) gives no token and keeps
  the entry, as the next run may refresh it.

## Consequences

The flows (BL-4161) look a token up before running and store what they get. Tokens sit in plain
text in an owner-only file, as curl's own `.netrc` credentials do; encrypting them would need a key
the same user can read, so it would add nothing.
