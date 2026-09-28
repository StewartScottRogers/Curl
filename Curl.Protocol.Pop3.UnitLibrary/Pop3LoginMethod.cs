namespace Curl.Protocol.Pop3;

/// <summary>
/// The ways a POP3 session may log in, as the login options allow (<see cref="Pop3LoginOptions" />).
/// </summary>
internal enum Pop3LoginMethod
{
    /// <summary>SASL <c>AUTH</c> when offered, else <c>APOP</c>, else <c>USER</c>/<c>PASS</c>.</summary>
    Any,

    /// <summary>Only <c>APOP</c> (<c>AUTH=+APOP</c>).</summary>
    Apop,

    /// <summary>Only the SASL mechanism <c>AUTH=&lt;mech&gt;</c> named.</summary>
    Sasl,
}
