using System.Reflection;
using Curl.Conformance.Extended.Harness;

namespace Curl.Conformance.Extended.UnitTests.Harness;

[TestClass]
public sealed class RequirementMatrixTests
{
    private const string Header = "| ID | Level | Requirement | Covered by |\n|----|:-----:|-------------|------------|\n";

    /// <summary>Every test method in this assembly that carries a <see cref="SpecRequirementAttribute"/>.</summary>
    internal static IReadOnlyList<SpecRequirementCarrier> CarriersInThisAssembly() =>
        typeof(RequirementMatrixTests).Assembly.GetTypes()
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Select(method => new SpecRequirementCarrier(
                    $"{type.Name}.{method.Name}",
                    method.GetCustomAttributes<SpecRequirementAttribute>().Select(attribute => attribute.Id).ToList())))
            .Where(carrier => carrier.RequirementIds.Count > 0)
            .ToList();

    [TestMethod]
    public void EveryMatrixAgreesWithTheTestsThatCarrySpecRequirement()
    {
        IReadOnlyList<RequirementMatrix> matrices = RequirementMatrix.LoadAll(Path.Combine(AppContext.BaseDirectory, "Conformance", "Extended"));

        IReadOnlyList<string> problems = RequirementMatrix.FindProblems(matrices, CarriersInThisAssembly());

        Assert.AreEqual(0, problems.Count, string.Join(Environment.NewLine, problems));
    }

    [TestMethod]
    public void Parse_ReadsRowsAndSkipsHeaderSeparatorAndProse()
    {
        RequirementMatrix matrix = RequirementMatrix.Parse("Mqtt5", "# MQTT 5\n\nProse.\n\n" + Header + "| Mqtt5:MQTT-3.1.2-3 | MUST | Reject it. | ConnectTests.Rejects |\r\n| Mqtt5:MQTT-1 | MAY |\n");

        Assert.AreEqual("Mqtt5", matrix.ProtocolName);
        Assert.HasCount(2, matrix.Rows);
        Assert.AreEqual(new RequirementMatrixRow("Mqtt5:MQTT-3.1.2-3", "MUST", "Reject it.", "ConnectTests.Rejects"), matrix.Rows[0]);
        Assert.AreEqual(new RequirementMatrixRow("Mqtt5:MQTT-1", "MAY", string.Empty, string.Empty), matrix.Rows[1]);
    }

    [TestMethod]
    public void FindProblems_MatrixAndTestsAgree_ReportsNothing()
    {
        RequirementMatrix matrix = RequirementMatrix.Parse("Coap", Header
            + "| Coap:RFC7252-5.4.1-1 | MUST | Reject it. | OptionTests.Rejects |\n"
            + "| Coap:RFC7252-5.4.1-2 | MUST | Ignore it. | OptionTests.Rejects |\n"
            + "| Coap:RFC7252-9-1 | SHOULD | Secure it. | n/a: DTLS is out of the harness |\n");
        SpecRequirementCarrier test = new("OptionTests.Rejects", ["Coap:RFC7252-5.4.1-1", "Coap:RFC7252-5.4.1-2"]);

        Assert.AreEqual(0, RequirementMatrix.FindProblems([matrix], [test]).Count);
    }

    [TestMethod]
    public void FindProblems_IdInAPartMatrixOfItsProtocol_IsListed()
    {
        RequirementMatrix part = RequirementMatrix.Parse("Kafka-Records", Header + "| Kafka:Guide-Fetch-1 | MUST | Fetch it. | FetchTests.Fetches |\n");
        SpecRequirementCarrier test = new("FetchTests.Fetches", ["Kafka:Guide-Fetch-1"]);

        Assert.AreEqual(0, RequirementMatrix.FindProblems([part], [test]).Count);
    }

    [TestMethod]
    public void FindProblems_NoMatrices_ReportsNothing()
    {
        Assert.AreEqual(0, RequirementMatrix.FindProblems([], []).Count);
    }

    [TestMethod]
    public void FindProblems_EmptyMatrix_Fails()
    {
        IReadOnlyList<string> problems = RequirementMatrix.FindProblems([RequirementMatrix.Parse("Coap", Header)], []);

        CollectionAssert.AreEqual(new[] { "Coap.md has no requirement rows." }, problems.ToArray());
    }

    [TestMethod]
    public void FindProblems_DuplicateId_Fails()
    {
        RequirementMatrix matrix = RequirementMatrix.Parse("Coap", Header + "| Coap:A-1 | MUST | x | n/a: y |\n| Coap:A-1 | MUST | x | n/a: y |\n");

        CollectionAssert.AreEqual(new[] { "Coap:A-1 is a row of Coap.md twice." }, RequirementMatrix.FindProblems([matrix], []).ToArray());
    }

    [TestMethod]
    public void FindProblems_RowNamingNoTest_Fails()
    {
        RequirementMatrix matrix = RequirementMatrix.Parse("Coap", Header + "| Coap:A-1 | MUST | x | Missing.Test |\n| Coap:A-2 | MUST | x | |\n");

        CollectionAssert.AreEqual(
            new[]
            {
                "Coap:A-1 in Coap.md is covered by 'Missing.Test', which is no test carrying [SpecRequirement].",
                "Coap:A-2 in Coap.md is covered by '', which is no test carrying [SpecRequirement].",
            },
            RequirementMatrix.FindProblems([matrix], []).ToArray());
    }

    [TestMethod]
    public void FindProblems_RowNamingATestThatDoesNotCarryItsId_Fails()
    {
        RequirementMatrix matrix = RequirementMatrix.Parse("Coap", Header + "| Coap:A-1 | MUST | x | T.M |\n| Coap:A-2 | MUST | x | n/a: y |\n");

        CollectionAssert.AreEqual(
            new[] { "Coap:A-1 in Coap.md is covered by T.M, which does not carry [SpecRequirement(\"Coap:A-1\")]." },
            RequirementMatrix.FindProblems([matrix], [new SpecRequirementCarrier("T.M", ["Coap:A-2"])]).ToArray()[..1]);
    }

    [TestMethod]
    [DataRow("n/a:")]
    [DataRow("n/a:   ")]
    public void FindProblems_NotApplicableWithNoReason_Fails(string coveredBy)
    {
        RequirementMatrix matrix = new("Coap", [new RequirementMatrixRow("Coap:A-1", "MUST", "x", coveredBy)]);

        CollectionAssert.AreEqual(new[] { "Coap:A-1 in Coap.md is n/a with no reason." }, RequirementMatrix.FindProblems([matrix], []).ToArray());
    }

    [TestMethod]
    public void FindProblems_IdInAPartMatrixOfItsProtocol_ReportsNothing()
    {
        RequirementMatrix part = RequirementMatrix.Parse("Gcs-Signing", Header + "| Gcs:A-1 | MUST | x | T.M |\n");
        SpecRequirementCarrier test = new("T.M", ["Gcs:A-1"]);

        Assert.IsEmpty(RequirementMatrix.FindProblems([part], [test]));
    }

    [TestMethod]
    public void FindProblems_CarriedIdInNoMatrixRow_Fails()
    {
        // Mqtt5:B-1 is a row, but of Coap.md: an ID counts only in its own protocol's matrix.
        RequirementMatrix matrix = RequirementMatrix.Parse("Coap", Header + "| Coap:A-1 | MUST | x | T.M |\n| Mqtt5:B-1 | MUST | x | T.M |\n");
        SpecRequirementCarrier test = new("T.M", ["Coap:A-1", "Coap:A-9", "Mqtt5:B-1", "NoColon"]);

        CollectionAssert.AreEqual(
            new[]
            {
                "Coap:A-9, carried by T.M, is no row of Coap.md.",
                "Mqtt5:B-1, carried by T.M, is no row of Mqtt5.md.",
                "NoColon, carried by T.M, is no row of NoColon.md.",
            },
            RequirementMatrix.FindProblems([matrix], [test]).ToArray());
    }

    [TestMethod]
    public void LoadAll_MissingFolder_LoadsNothing()
    {
        Assert.AreEqual(0, RequirementMatrix.LoadAll(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))).Count);
    }

    [TestMethod]
    public void LoadAll_ReadsEveryMarkdownFileNamedByItsFileName()
    {
        string directory = Directory.CreateTempSubdirectory().FullName;
        try
        {
            File.WriteAllText(Path.Combine(directory, "Mqtt5.md"), Header + "| Mqtt5:A-1 | MUST | x | n/a: y |\n");
            File.WriteAllText(Path.Combine(directory, "Coap.md"), Header);
            File.WriteAllText(Path.Combine(directory, "notes.txt"), "not a matrix");

            IReadOnlyList<RequirementMatrix> matrices = RequirementMatrix.LoadAll(directory);

            CollectionAssert.AreEqual(new[] { "Coap", "Mqtt5" }, matrices.Select(matrix => matrix.ProtocolName).ToArray());
            Assert.HasCount(1, matrices[1].Rows);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public void SpecRequirementAttribute_NamesItsIdAndProtocol()
    {
        SpecRequirementAttribute attribute = new("Mqtt5:MQTT-3.1.2-3");

        Assert.AreEqual("Mqtt5:MQTT-3.1.2-3", attribute.Id);
        Assert.AreEqual("Mqtt5", attribute.ProtocolName);
        Assert.AreEqual("NoColon", SpecRequirementAttribute.ProtocolNameOf("NoColon"));
    }
}
