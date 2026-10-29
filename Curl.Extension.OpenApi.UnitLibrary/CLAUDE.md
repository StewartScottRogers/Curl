# Curl.Extension.OpenApi.UnitLibrary

The OpenAPI extension (ADR-0999, ADR-1083), tested against the OpenAPI Specification 3.1
(spec.openapis.org/oas/v3.1.1). Namespace `Curl.Extension.OpenApi`. It references only `Curl.Extension.Abstractions`
(and through it `Curl.Protocol.Abstractions`) and the base class library; descriptions are read with
`System.Text.Json`'s `JsonDocument` (no serializer, so AOT-safe).

- `OpenApiOptions` declares `--openapi`, `--operation` and `--openapi-server` and the extension's `Description`;
  `Curl.Extension.Composition`'s option and description catalogues list them.
- `OpenApiArgumentExpander` is a trailing argument expander (ADR-1040, ADR-1051) the expander catalogue lists: it
  replaces `--openapi <spec>`, its companions and its `name=value` arguments with the operation's curl arguments
  before Curl.Console parses the command line. It never touches a transfer.
- `OpenApiDocument` reads the description, finds the operation and follows local `$ref`s into an `OpenApiOperation`
  (`OpenApiParameter`s, an `OpenApiRequestBody` with `OpenApiPropertyType`s); `OpenApiRequest` builds the arguments,
  with `OpenApiParameterSerializer` (styles) and `OpenApiPercentEncoding`; `OpenApiJson` reads members and
  `OpenApiFailure` makes the exit 2 failure.

Held to the four quality gates in the root `CLAUDE.md`. Its tests are in `Curl.Extension.OpenApi.UnitTests`.
