using Curl.Extension.Abstractions;
using Curl.Extension.Abstractions.Description;

namespace Curl.Extension.GraphQl;

/// <summary>The command-line options the GraphQL extension adds, and its description (ADR-1026).</summary>
public static class GraphQlOptions
{
    /// <summary>Gets <c>--graphql &lt;file&gt;</c>: sends the GraphQL document in the file as a GraphQL-over-HTTP POST.</summary>
    public static ExtensionOption Document { get; } = new(
        "graphql",
        ExtensionOptionArgument.Value,
        "Send the GraphQL document in <file> as a GraphQL-over-HTTP POST");

    /// <summary>Gets <c>--graphql-variables &lt;json&gt;</c>: the operation's variables, a JSON object or <c>@file</c> holding one.</summary>
    public static ExtensionOption Variables { get; } = new(
        "graphql-variables",
        ExtensionOptionArgument.Value,
        "GraphQL variables: a JSON object, or @file holding one");

    /// <summary>Gets <c>--graphql-operation &lt;name&gt;</c>: which operation of the document to run.</summary>
    public static ExtensionOption Operation { get; } = new(
        "graphql-operation",
        ExtensionOptionArgument.Value,
        "Name of the operation in the GraphQL document to run");

    /// <summary>Gets <c>--graphql-introspect</c>: sends the standard introspection query in place of a document.</summary>
    public static ExtensionOption Introspect { get; } = new(
        "graphql-introspect",
        ExtensionOptionArgument.None,
        "Send the GraphQL introspection query for the server's schema");

    /// <summary>Gets what <c>curl --extensions</c> and <c>curl --ai-help extensions</c> say about the extension.</summary>
    public static ExtensionDescription Description { get; } = new(
        "graphql",
        "Sends GraphQL queries over HTTP, with variables and introspection, and lists the errors a response reports on stderr.",
        [],
        [Document, Variables, Operation, Introspect],
        new Uri("https://graphql.github.io/graphql-over-http/draft/"));
}
