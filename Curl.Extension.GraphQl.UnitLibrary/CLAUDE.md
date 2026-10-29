# Curl.Extension.GraphQl.UnitLibrary

The GraphQL extension (ADR-0999, ADR-1026), tested against GraphQL over HTTP
(graphql.github.io/graphql-over-http). Namespace `Curl.Extension.GraphQl`. It references only
`Curl.Extension.Abstractions` (and through it `Curl.Protocol.Abstractions`) and the base class library;
JSON is read and written with `System.Text.Json`'s `JsonDocument` and `Utf8JsonWriter` (no serializer,
so AOT-safe).

- `GraphQlOptions` declares `--graphql`, `--graphql-variables`, `--graphql-operation` and
  `--graphql-introspect` and the extension's `Description`; `Curl.Extension.Composition`'s option and
  description catalogues list them.
- `GraphQlTransferHook.ActivatedBy` gives the hook when `--graphql` or `--graphql-introspect` was given;
  the transfer hook catalogue lists it. Its `ExecuteAsync` replaces an HTTP transfer's body with the JSON
  request `GraphQlRequestEncoding` writes, adds the `Accept` header unless `-H` gave one, runs curl's
  handler on a `GraphQlTransferContext` whose output is a `CopyingStream`, and lists the response's
  errors (`GraphQlErrorListing`) on stderr. An unreadable file or variables that are not a JSON object
  fail with exit 26 before anything is sent.
- `GraphQlIntrospectionQuery` holds the introspection query.

Held to the four quality gates in the root `CLAUDE.md`. Its tests are in
`Curl.Extension.GraphQl.UnitTests`.
