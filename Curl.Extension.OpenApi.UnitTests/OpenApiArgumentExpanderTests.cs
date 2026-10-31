using System.Text;
using Curl.Extension.Abstractions;
using Curl.Extension.Abstractions.Arguments;
using Curl.Extension.Composition;

namespace Curl.Extension.OpenApi;

/// <summary>
/// Checks <c>--openapi</c>, <c>--operation</c> and <c>--openapi-server</c> end to end against OpenAPI Specification
/// 3.1's sections, from a recorded description to curl arguments, and the extension's registration in
/// <c>Curl.Extension.Composition</c>.
/// </summary>
[TestClass]
public sealed class OpenApiArgumentExpanderTests
{
    private const string PetStore = """
        {
          "openapi": "3.1.0",
          "info": { "title": "Pet store", "version": "1" },
          "servers": [ { "url": "https://{region}.pets.example/v{version}", "variables": { "region": { "default": "eu" }, "version": { "default": "2" } } } ],
          "paths": {
            "/pets/{petId}": {
              "parameters": [ { "$ref": "#/components/parameters/PetId" } ],
              "get": {
                "operationId": "getPet",
                "parameters": [
                  { "name": "fields", "in": "query", "explode": false },
                  { "name": "X-Trace", "in": "header" },
                  { "name": "session", "in": "cookie" }
                ]
              },
              "delete": { "operationId": "deletePet" },
              "head": { "operationId": "petExists" }
            },
            "/pets": {
              "post": {
                "operationId": "addPet",
                "requestBody": { "$ref": "#/components/requestBodies/Pet" }
              }
            }
          },
          "components": {
            "parameters": { "PetId": { "name": "petId", "in": "path", "required": true } },
            "requestBodies": { "Pet": { "content": { "application/json": { "schema": { "$ref": "#/components/schemas/Pet" } } } } },
            "schemas": { "Pet": { "type": "object", "properties": { "name": { "type": "string" }, "age": { "type": "integer" }, "tags": { "type": "array", "items": { "type": "string" } } } } }
          }
        }
        """;

    [TestMethod]
    public void ArgumentsFor_GetWithPathQueryHeaderAndCookieParameters_PutsEachInItsPlace()
    {
        IReadOnlyList<string> arguments = new OpenApiArgumentExpander().ArgumentsFor(
            Claimed("pets.json", "getPet", "petId=7", "fields=name", "fields=age", "X-Trace=abc", "session=s1"),
            ContextOf(("pets.json", PetStore)));

        CollectionAssert.AreEqual(
            new[] { "https://eu.pets.example/v2/pets/7?fields=name,age", "-H", "X-Trace: abc", "-b", "session=s1" },
            arguments.ToArray());
    }

    [TestMethod]
    public void ArgumentsFor_PostWithJsonRequestBody_SendsTheOtherValuesAsTypedJson()
    {
        IReadOnlyList<string> arguments = new OpenApiArgumentExpander().ArgumentsFor(
            Claimed("pets.json", "addPet", "name=Rex", "age=3", "tags=good"),
            ContextOf(("pets.json", PetStore)));

        CollectionAssert.AreEqual(
            new[] { "https://eu.pets.example/v2/pets", "-H", "Content-Type: application/json", "--data-binary", """{"name":"Rex","age":3,"tags":["good"]}""" },
            arguments.ToArray());
    }

    [TestMethod]
    public void ArgumentsFor_DeleteAndHead_SetTheMethod()
    {
        OpenApiArgumentExpander expander = new();

        CollectionAssert.AreEqual(new[] { "-X", "DELETE", "https://eu.pets.example/v2/pets/1" }, expander.ArgumentsFor(Claimed("pets.json", "deletePet", "petId=1"), ContextOf(("pets.json", PetStore))).ToArray());
        CollectionAssert.AreEqual(new[] { "-I", "https://eu.pets.example/v2/pets/1" }, expander.ArgumentsFor(Claimed("pets.json", "petExists", "petId=1"), ContextOf(("pets.json", PetStore))).ToArray());
    }

    [TestMethod]
    public void ArgumentsFor_OpenApiServer_ReplacesTheDescriptionsServer()
    {
        ClaimedArguments claimed = new("pets.json", [new(OpenApiOptions.Operation, "deletePet"), new(OpenApiOptions.Server, "http://a/"), new(OpenApiOptions.Server, "http://localhost:8080/")], ["petId=1"]);

        IReadOnlyList<string> arguments = new OpenApiArgumentExpander().ArgumentsFor(claimed, ContextOf(("pets.json", PetStore)));

        CollectionAssert.AreEqual(new[] { "-X", "DELETE", "http://localhost:8080/pets/1" }, arguments.ToArray());
    }

    [TestMethod]
    public void ArgumentsFor_WithoutOperation_FailsWithExit2()
    {
        ArgumentExpansionException failure = Assert.ThrowsExactly<ArgumentExpansionException>(() => new OpenApiArgumentExpander().ArgumentsFor(new ClaimedArguments("pets.json", [], []), ContextOf(("pets.json", PetStore))));

        Assert.AreEqual(CurlExitCode.FailedInit, failure.ExitCode);
        Assert.AreEqual("openapi: --operation <id> names the operation to call", failure.Message);
    }

    [TestMethod]
    public void ArgumentsFor_TwoOperations_FailsWithExit2()
    {
        ClaimedArguments claimed = new("pets.json", [new(OpenApiOptions.Operation, "getPet"), new(OpenApiOptions.Operation, "addPet")], []);

        ArgumentExpansionException failure = Assert.ThrowsExactly<ArgumentExpansionException>(() => new OpenApiArgumentExpander().ArgumentsFor(claimed, ContextOf(("pets.json", PetStore))));

        Assert.AreEqual("openapi: one --operation per --openapi", failure.Message);
    }

    [TestMethod]
    public void ArgumentsFor_UnreadableDescription_FailsWithReadError()
    {
        ArgumentExpansionException failure = Assert.ThrowsExactly<ArgumentExpansionException>(() => new OpenApiArgumentExpander().ArgumentsFor(Claimed("missing.json", "getPet"), ContextOf()));

        Assert.AreEqual(CurlExitCode.ReadError, failure.ExitCode);
        Assert.AreEqual("openapi: cannot read missing.json", failure.Message);
    }

    [TestMethod]
    public void ArgumentsForValue_WithoutCompanions_AsksForTheOperation() =>
        Assert.ThrowsExactly<ArgumentExpansionException>(() => ((IArgumentExpander)new OpenApiArgumentExpander()).ArgumentsFor("pets.json", ContextOf(("pets.json", PetStore))));

    [TestMethod]
    [DataRow("petId=5", true)]
    [DataRow("page[size]=10", true)]
    [DataRow("X-Request_ID.v=1", true)]
    [DataRow("a==b", true)]
    [DataRow("=5", false)]
    [DataRow("petId", false)]
    [DataRow("http://h/?a=b", false)]
    [DataRow("h/x?a=b", false)]
    public void ClaimsPositionalArgument_TakesOnlyNameEqualsValue(string argument, bool claimed) =>
        Assert.AreEqual(claimed, new OpenApiArgumentExpander().ClaimsPositionalArgument(argument));

    [TestMethod]
    public void ArgumentsFor_ValueHoldingAnEqualsSign_KeepsEverythingAfterTheFirst()
    {
        IReadOnlyList<string> arguments = new OpenApiArgumentExpander().ArgumentsFor(Claimed("pets.json", "addPet", "name=a=b"), ContextOf(("pets.json", PetStore)));

        Assert.AreEqual("""{"name":"a=b"}""", arguments[^1]);
    }

    [TestMethod]
    public void Expand_CommandLineWithOpenApiOperationAndValues_ReplacesThemWithTheRequest()
    {
        IReadOnlyList<string> expanded = ArgumentExpansion.Expand(
            ["-s", "--openapi", "pets.json", "--operation", "deletePet", "petId=9", "--openapi-server", "http://h"],
            ExtensionArgumentExpanderCatalogue.Entries,
            ContextOf(("pets.json", PetStore)));

        CollectionAssert.AreEqual(new[] { "-s", "-X", "DELETE", "http://h/pets/9" }, expanded.ToArray());
    }

    [TestMethod]
    public void Expand_CommandLineWithoutTheOption_IsUnchanged()
    {
        string[] arguments = ["-s", "-d", "a=1", "http://h/?a=b"];

        IReadOnlyList<string> expanded = ArgumentExpansion.Expand(arguments, ExtensionArgumentExpanderCatalogue.Entries, ContextOf());

        CollectionAssert.AreEqual(arguments, expanded.ToArray());
    }

    [TestMethod]
    public void Catalogues_ListTheExpanderAndItsOptions()
    {
        Assert.IsTrue(ExtensionArgumentExpanderCatalogue.Entries.Any(expander => expander is OpenApiArgumentExpander));
        Assert.AreSame(OpenApiOptions.Specification, new OpenApiArgumentExpander().Option);
        CollectionAssert.AreEqual(new[] { OpenApiOptions.Operation, OpenApiOptions.Server }, new OpenApiArgumentExpander().CompanionOptions.ToArray());
    }

    [TestMethod]
    public void CommandLineOptions_WithExtensionsOff_LeaveTheOptionsOut_SoTheParserRefusesThemAsUnknown() =>
        Assert.IsFalse(ExtensionCommandLineOptions.For(name => name == "CURL_EXTENSIONS" ? "0" : null, _ => false).Any(option => option.Name is "openapi" or "operation" or "openapi-server"));

    [TestMethod]
    public void CommandLineOptions_WithExtensionsOn_HoldTheOptionsWithAValue()
    {
        IReadOnlyList<ExtensionOption> options = ExtensionCommandLineOptions.For(_ => null, _ => false);

        foreach (string name in new[] { "openapi", "operation", "openapi-server" })
        {
            Assert.AreEqual(ExtensionOptionArgument.Value, options.Single(option => option.Name == name).Argument);
        }
    }

    [TestMethod]
    public void Listing_NamesTheExtensionAndItsOptions()
    {
        Assert.IsTrue(ExtensionListing.Lines(_ => null).Any(line => line.Contains("openapi", StringComparison.Ordinal)));
        Assert.Contains("--openapi", ExtensionListing.Markdown(_ => null));
        Assert.Contains("--operation", ExtensionListing.Markdown(_ => null));
        Assert.Contains("--openapi-server", ExtensionListing.Markdown(_ => null));
        Assert.AreEqual(new Uri("https://spec.openapis.org/oas/v3.1.1.html"), OpenApiOptions.Description.Specification);
    }

    internal static ClaimedArguments Claimed(string file, string operation, params string[] values) =>
        new(file, [new(OpenApiOptions.Operation, operation)], values);

    internal static ArgumentExpansionContext ContextOf(params (string Path, string Text)[] files)
    {
        Dictionary<string, byte[]> bytes = files.ToDictionary(file => file.Path, file => Encoding.UTF8.GetBytes(file.Text), StringComparer.Ordinal);
        return new(_ => null, path => bytes.GetValueOrDefault(path));
    }
}
