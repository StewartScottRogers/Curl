# ADR-1026 — GraphQL is a transfer hook extension with four options

- **Status:** Accepted
- **Date:** 2026-10-29

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-4105.

## Context

BL-4105 adds GraphQL queries as an extension (ADR-0999), `Curl.Extension.GraphQl.UnitLibrary`.
Upstream curl has no GraphQL support, so there is no curl behaviour to match; the specification,
GraphQL over HTTP (graphql.github.io/graphql-over-http, the working draft as published in 2026),
decides. A GraphQL request is an ordinary HTTP transfer, so the extension adds no scheme: it is a
transfer hook (ADR-1007, ADR-1019) activated by its options.

## Decision

- Options: `--graphql <file>` (the GraphQL document), `--graphql-variables <json>` (a JSON object, or
  `@file` holding one), `--graphql-operation <name>` (`operationName`) and the flag
  `--graphql-introspect` (sends the standard introspection query in place of a document; it ignores
  `--graphql-operation`). Either `--graphql` or `--graphql-introspect` activates the hook; the other two
  alone do nothing.
- Request ("Request Parameters", "POST"): always a POST whose body is the JSON object
  `{"query":..., "operationName":..., "variables":...}`, members present only when given, with
  `Content-Type: application/json`. The `Accept` header ("Accept") is
  `application/graphql-response+json, application/json;q=0.9`, added after curl's headers unless the
  user gave an `Accept` header with `-H`. The body replaces any `-d`/`-F` body; `-X` still applies.
- A document or variables file that cannot be read, and variables that are not a JSON object, fail the
  transfer with exit 26 (curl's read error) before anything is sent.
- Response ("Response"): the body reaches curl's output byte for byte. When it is JSON with an
  `errors` list, each error is listed on stderr as one line,
  `GraphQL error: <message> (line L, column C; ...) at path a.0.b`. The exit code stays curl's: a
  GraphQL error is not a transfer failure, as the specification has servers answer 200 for it with
  `application/json`.
- A transfer that is not HTTP runs unchanged.

## Left out, and why

- GET requests ("GET"): POST is valid for every operation, including mutations, so one method keeps
  the behaviour simple; a user can still send a GET query with curl's own `-G` and `--data-urlencode`.
- Reading the document or variables from stdin (`-`): stdin may already carry `-d @-` or
  `--upload-file -`; a file keeps the two apart.
- Batched requests and subscriptions over HTTP (incremental delivery): not part of the GraphQL over
  HTTP specification's core.
- The listing goes to the process's stderr (`Console.Error`), not to curl's `--stderr` target, which
  extensions cannot reach through the transfer hook contract.

## Consequences

The hook is listed by `curl --extensions` and `curl --ai-help extensions`, and with
`CURL_EXTENSIONS=0` its options are unknown options (exit 2) like any other extension's.
