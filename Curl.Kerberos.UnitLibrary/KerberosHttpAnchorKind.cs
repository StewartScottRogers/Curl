namespace Curl.Kerberos;

/// <summary>What a <see cref="KerberosHttpAnchor" />'s location names.</summary>
public enum KerberosHttpAnchorKind
{
    /// <summary><c>FILE:</c>, a PEM file of certificates.</summary>
    File,

    /// <summary><c>DIR:</c>, a directory whose files, other than those whose names start with a dot, are PEM files.</summary>
    Directory,

    /// <summary><c>ENV:</c>, an environment variable whose value is another <c>http_anchors</c> value.</summary>
    EnvironmentVariable,
}
