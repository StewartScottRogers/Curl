using System.Net;

namespace Curl.Extended.Protocol.S3.Client;

/// <summary>Checks where <see cref="S3Configuration"/> takes the credentials, region and endpoint from (ADR-1090).</summary>
[TestClass]
public sealed class S3ConfigurationTests
{
    [TestMethod]
    public void From_EnvironmentKeys_SignsWithThemAndTheSessionToken()
    {
        Dictionary<string, string> variables = new()
        {
            ["AWS_ACCESS_KEY_ID"] = "AKID",
            ["AWS_SECRET_ACCESS_KEY"] = "secret",
            ["AWS_SESSION_TOKEN"] = "token",
            ["AWS_REGION"] = "eu-west-2",
            ["AWS_ENDPOINT_URL_S3"] = "http://minio:9000",
            ["AWS_ENDPOINT_URL"] = "http://other",
        };

        S3Configuration configuration = S3Configuration.From(variables.GetValueOrDefault, null);

        Assert.AreEqual(new("AKID", "secret", "eu-west-2"), configuration.Credentials);
        Assert.AreEqual("token", configuration.SessionToken);
        Assert.AreEqual("http://minio:9000", configuration.EndpointUrl);
    }

    [TestMethod]
    public void From_UserCredentials_OverrideTheEnvironmentAndDropTheSessionToken()
    {
        Dictionary<string, string> variables = new() { ["AWS_ACCESS_KEY_ID"] = "AKID", ["AWS_SESSION_TOKEN"] = "token", ["AWS_DEFAULT_REGION"] = "ap-south-1", ["AWS_ENDPOINT_URL"] = "https://r2" };

        S3Configuration configuration = S3Configuration.From(variables.GetValueOrDefault, new NetworkCredential("user", "pass"));

        Assert.AreEqual(new("user", "pass", "ap-south-1"), configuration.Credentials);
        Assert.IsNull(configuration.SessionToken);
        Assert.AreEqual("https://r2", configuration.EndpointUrl);
    }

    [TestMethod]
    [DataRow("AWS_ACCESS_KEY_ID")]
    [DataRow("AWS_SECRET_ACCESS_KEY")]
    public void From_OnlyOneKeyVariable_SendsUnsignedToUsEast1OnAws(string name)
    {
        S3Configuration configuration = S3Configuration.From(variable => variable == name ? "x" : null, null);

        Assert.IsNull(configuration.Credentials);
        Assert.AreEqual(S3Configuration.DefaultRegion, configuration.Region);
        Assert.IsNull(configuration.EndpointUrl);
    }
}
