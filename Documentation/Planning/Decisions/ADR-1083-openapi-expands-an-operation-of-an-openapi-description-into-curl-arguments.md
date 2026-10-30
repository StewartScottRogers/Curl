# ADR-1083 — --openapi expands an operation of an OpenAPI description into curl arguments

- **Status:** Accepted
- **Date:** 2026-10-29

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-4107.

## Context

BL-4107 adds the extension `Curl.Extension.OpenApi.UnitLibrary`: build a request from an OpenAPI description with
`--openapi <spec> --operation <id> name=value...`. Upstream curl has no such option, so there is no curl output to
match; the behaviour follows the OpenAPI Specification 3.1 (3.1.1, spec.openapis.org/oas/v3.1.1), whose parameter,
server and request body rules 3.0 shares, so 3.0 descriptions are read too. A description supplies a whole request,
so the extension is a trailing argument expander (ADR-1040, ADR-1051), like `--http-file` (ADR-1073): curl's own
option parser and transfers do the rest, and no transfer changes.

## Decision

- **Option names.** `--openapi <spec>` names the description file. `--operation <id>` (companion option) names the
  operation by its `operationId`; exactly one per `--openapi`, or exit 2. `--openapi-server <url>` (companion
  option, last wins) replaces the description's server URL, needed when that URL is relative. None is a curl option.
- **`name=value` arguments.** The expander claims each positional argument after `--openapi` in its `--next` group
  whose text before the first `=` is letters, digits and `_ - . [ ]`; a URL has `:` or `/` there, so it stays a URL.
  A name given more than once is an array.
- **Operation.** Found by `operationId` in `paths` across the eight method members (§4.8.9.1). Path Item parameters
  are overridden by the operation's of the same `name` and `in` (§4.8.10.1); header parameters named `Accept`,
  `Content-Type` or `Authorization` are ignored (§4.8.12.1). Local `$ref`s are followed with JSON Pointer (§4.8.23,
  RFC 6901), with a depth limit of 32 for cycles.
- **Server.** The first Server Object of the operation's, else the Path Item's, else the root `servers`, else `/`
  (§4.8.1); variables take their `default` (§4.8.6). The path is appended to the URL without its trailing `/`
  (§4.8.8.1). A URL without `://` fails, exit 2, asking for `--openapi-server`.
- **Parameters.** Serialized by `style` and `explode` with their defaults (§4.8.12.3, §4.8.12.4) as the Style
  Examples table shows: path `simple`, `label`, `matrix`; query `form`, `spaceDelimited`, `pipeDelimited`; header
  `simple`; cookie `form`. Path and query names and values are percent-encoded (RFC 3986 unreserved kept, and
  reserved too with `allowReserved`); header and cookie values go as given. Query pieces join with `&`; headers go
  as `-H "name: value"`; cookies join with `; ` into one `-b`. A missing `required` parameter (every path one) fails
  with exit 2. A URL holding `[ ] { }` adds `--globoff`, so curl does not glob it.
- **Body.** Every `name=value` that names no parameter is a request body property (§4.8.13, §4.8.14). The media
  type is the first JSON (`application/json` or `+json`), `application/x-www-form-urlencoded` or
  `multipart/form-data` one in `content`. JSON goes as `-H "Content-Type: <type>" --data-binary <object>`, each
  value written as its schema property's `type` (integer, number, boolean, null, object, array with `items`) when
  it is a JSON literal of that type, else as a string; form values go as `--data-urlencode name=value`, multipart
  ones as `--form-string name=value`. A value for no parameter when the operation has no body fails with exit 2.
- **Method.** Left to curl where its default is the same (GET without a body, POST with one); `head` is `-I`; every
  other is `-X METHOD`.
- **Failures.** An unreadable description gives exit 26 (`ReadError`), as `-K` does; every other problem gives
  `curl: openapi: <what>` and exit 2.
- **Off switch.** With `CURL_EXTENSIONS=0` the three options are not registered, so curl refuses them as unknown
  (exit 2), as it does any unknown option; without them nothing is expanded.

## Left out, and why

- **YAML anchors, aliases and tags.** A YAML description (OpenAPI §4.3) is read by `OpenApiYaml`, a hand-written
  reader (BL-4287) of the YAML 1.2.2 constructs descriptions use; a file whose first character that is not white space
  is not `{` is read as YAML. Anchors, aliases and tags fail with exit 2, and so does YAML it cannot read ("is not a
  YAML OpenAPI description"). Decided by Claude under Stewart's delegation.
- **Remote descriptions and non-local `$ref`s.** An expander runs before any transfer and reads only files
  (ADR-1040); fetching first is `curl -o spec.json <url>`. A `$ref` to another document fails with exit 2.
- **`deepObject` and object-valued parameters.** A `name=value` argument gives a primitive or an array; a style
  that needs an object fails with exit 2 naming it.
- **Security schemes.** Credentials stay curl's own options (`-u`, `-H "Authorization: ..."`, `--oauth2-bearer`).
- **Swagger 2.0.** A different format; a description without an `openapi` 3.x member fails with exit 2.

## Consequences

`curl --extensions` and `curl --ai-help extensions` list the extension and its three options from
`OpenApiOptions.Description`. Every behaviour is unit tested against the sections above in
`Curl.Extension.OpenApi.UnitTests`, from recorded description text to curl arguments, with no network.
