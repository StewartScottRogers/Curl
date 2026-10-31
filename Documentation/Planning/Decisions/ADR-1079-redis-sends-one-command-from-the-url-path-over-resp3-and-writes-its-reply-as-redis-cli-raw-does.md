# ADR-1079: Redis sends one command from the URL path over RESP3 and writes its reply as redis-cli --raw does

- Status: Accepted
- Date: 2026-10-29
- Decided by Claude under Stewart's delegation (BL-4122)

## Context

BL-4122 adds Redis as an extension (ADR-0999), tested against the RESP3 specification
(https://github.com/redis/redis-specifications/blob/master/protocol/RESP3.md) and the IANA-registered `redis` and
`rediss` URI schemes (https://www.iana.org/assignments/uri-schemes/prov/redis), not against curl, which has no Redis.
It needs a way to name the command in a URL, a mapping from curl's options, an output form for replies and exit
codes for the failures.

## Decision

- Schemes `redis` and `rediss` (TLS from curl's connector, so `-k`, `--cacert` and the other TLS options apply), both
  on port 6379 when the URL names none: the URI scheme registrations give 6379 and name no separate TLS port. The
  library is `Curl.Extended.Protocol.Redis.UnitLibrary`; it adds no command-line option.
- The URL is `redis://[user:password@]host[:port]/[db]/command/argument/...`. An all-digit first path segment is the
  URI scheme's `db-number`, sent as `SELECT db` before the command. Every later segment, URL-decoded and sent as UTF-8,
  is one argument, so `%2F` puts a `/` inside an argument and an empty segment is an empty argument. `-d` (and its
  friends) appends the data, byte for byte, as one more last argument, which is how binary values are sent
  (`curl redis://host/SET/key --data-binary @file`). A URL that names no command ends with exit 3 before connecting.
- The flow sends `HELLO 3` first, to speak RESP3 ("The HELLO command and connection handshake"). With `--user` or the
  URL's user information it sends `HELLO 3 AUTH user password`, with the user `default` when only a password is given
  (as `redis://:password@host` means). Commands are arrays of bulk strings ("Sending commands to a Redis server").
  Each command waits for its reply before the next is sent, so an error names the command it answered.
- The reader takes every RESP3 type: simple strings and errors, integers, bulk strings and errors, verbatim strings,
  nulls, booleans, doubles, big numbers, arrays, sets, pushes, maps, and attributes, which are read and dropped before
  the reply they describe. RESP2's `$-1` and `*-1` are read as null. Aggregates nested more than 128 deep are refused,
  so a hostile server cannot exhaust the stack.
- The reply is written as `redis-cli --raw` writes it: each string, number, verbatim string's text and nested error on
  its own line ending LF, a null as an empty line, a boolean as `true` or `false`, an aggregate as its elements in
  order and a map as its keys and values alternating. That keeps `curl redis://host/GET/k` printing the value the way
  scripts using `redis-cli --raw` already expect.
- Exit codes: an error reply to `HELLO` is 67 when credentials were sent or the error is `NOAUTH`, and 8 otherwise;
  an error reply to `SELECT` or the command, or a malformed reply, is 8 (`Redis server answered GET with error: ...`,
  nothing written); the connection ending before or inside a reply, or a failed read, is 56; a failed write to the
  server is 55; a failed write to the output is 23. Exit 8 for an error reply follows ADR-1059's choice for STOMP's
  `ERROR` frame, and lets a script tell a refused command from a printed reply.

## Left out, and why

- Streamed strings and aggregates (`$?` and `*?`) are not read: the specification reserves them and no Redis server
  sends them to a client.
- RESP2-only servers (before Redis 6) refuse `HELLO` and end with exit 8: the task follows RESP3, and falling back to
  `AUTH` and RESP2 would be a second protocol to test.
- More than one command per transfer, `SUBSCRIBE` streams (only the first reply is written) and the URI's query
  parameters (such as `?db=`) are not offered: "one command, reply printed" is the scope, and no curl option maps onto
  the rest without inventing new ones. A later task can add options through the extension option catalogue.

## Consequences

`curl redis://cache/GET/key` prints a value, `curl redis://:pw@cache/2/SET/key -d hi` stores one in database 2, and
`rediss://` does the same over TLS, with `CURL_EXTENSIONS=0` failing both schemes as curl fails an unsupported scheme
(exit 1).
