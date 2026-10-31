using System.Globalization;

namespace Curl.Extension.AzureStorage.Bodies;

/// <summary>Reads the RFC 1123 times Azure Storage puts in its bodies, such as <c>Wed, 09 Sep 2009 09:20:02 GMT</c>.</summary>
internal static class AzureStorageHttpTime
{
    /// <summary>Reads <paramref name="text"/> as an RFC 1123 time in UTC.</summary>
    public static DateTimeOffset Parse(string text) =>
        DateTimeOffset.ParseExact(text, "r", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);
}
