using Curl.Kerberos;
using Curl.Protocol.Abstractions;

namespace Curl.Authentication;

/// <summary>
/// The hand-built Negotiate and Kerberos route of ADR-0142 (S+K): the first step gets a
/// service ticket and makes the GSS-API Kerberos initial token, wrapped for Negotiate in a
/// SPNEGO NegTokenInit offering Kerberos V5 alone, as MIT's library does; the second reads
/// the acceptor's AP-REP, from inside a NegTokenResp for Negotiate, and completes the context.
/// The credential cache is always the one used: curl's GSS-API Negotiate ignores
/// <c>-u</c>'s password off Windows.
/// </summary>
/// <param name="request">The mechanism, <see cref="SecurityMechanism.Negotiate" /> or <see cref="SecurityMechanism.Kerberos" />, and the acceptor.</param>
/// <param name="tickets">Gets the service ticket.</param>
/// <param name="timeProvider">Gives the authenticator's time.</param>
/// <param name="randomSource">Gives the subkey, sequence number and confounders.</param>
internal sealed class HandBuiltKerberosSecurityContext(
    SecurityContextRequest request,
    KerberosServiceTicketSource tickets,
    TimeProvider timeProvider,
    IKerberosRandomSource randomSource) : ISecurityContext
{
    private KerberosCredential? serviceTicket;
    private KerberosGssContext? gss;

    /// <inheritdoc />
    public bool IsCompleted => gss?.IsCompleted == true;

    /// <inheritdoc />
    public async ValueTask<SecurityContextStep> NextTokenAsync(ReadOnlyMemory<byte> incomingToken, CancellationToken cancellationToken) =>
        gss is null
            ? await InitialStepAsync(cancellationToken).ConfigureAwait(false)
            : ReplyStep(gss, incomingToken);

    /// <inheritdoc />
    public void Dispose()
    {
        gss?.Dispose();
        serviceTicket?.Dispose();
    }

    private static SecurityContextStep Failure(SecurityContextStatus status) => new(status, []);

    private static SecurityContextStatus StatusOf(KerberosKdcError error) =>
        error is KerberosKdcError.NoCredentials or KerberosKdcError.TicketExpired
            ? SecurityContextStatus.NoCredentials
            : SecurityContextStatus.Refused;

    /// <summary>
    /// Gets the Kerberos token inside a NegTokenResp: its <c>responseToken</c>, or
    /// <see langword="null" /> when the acceptor rejected the exchange or sent none.
    /// </summary>
    private static ReadOnlyMemory<byte>? AcceptedTokenOf(ReadOnlyMemory<byte> incomingToken)
    {
        SpnegoNegotiationResponse response = SpnegoNegotiationResponse.Decode(incomingToken);
        return response.State == SpnegoNegotiationState.Reject ? null : response.ResponseToken;
    }

    private SecurityContextStep ReplyStep(KerberosGssContext context, ReadOnlyMemory<byte> incomingToken)
    {
        try
        {
            ReadOnlyMemory<byte>? mechanismToken = request.Mechanism == SecurityMechanism.Negotiate ? AcceptedTokenOf(incomingToken) : incomingToken;
            if (mechanismToken is not { } token)
            {
                return Failure(SecurityContextStatus.Refused);
            }

            context.NextToken(token.Span);
            return new SecurityContextStep(SecurityContextStatus.Completed, []);
        }
        catch (SpnegoTokenException)
        {
            return Failure(SecurityContextStatus.MalformedToken);
        }
        catch (KerberosGssException failure)
        {
            return Failure(failure.Error == KerberosGssError.MalformedToken ? SecurityContextStatus.MalformedToken : SecurityContextStatus.Refused);
        }
    }

    private async ValueTask<SecurityContextStep> InitialStepAsync(CancellationToken cancellationToken)
    {
        try
        {
            serviceTicket = await tickets.GetAsync(request.ServiceName, request.HostName, cancellationToken).ConfigureAwait(false);
            gss = new KerberosGssContext(serviceTicket, new KerberosGssContextOptions(), timeProvider, randomSource);
        }
        catch (KerberosFileException)
        {
            return Failure(SecurityContextStatus.NoCredentials);
        }
        catch (KerberosKdcException failure)
        {
            return Failure(StatusOf(failure.Error));
        }
        catch (Exception failure) when (failure is KerberosConfigurationException or KerberosCryptographyException)
        {
            return Failure(SecurityContextStatus.Refused);
        }

        byte[] token = gss.NextToken([]);
        return new SecurityContextStep(
            SecurityContextStatus.ContinueNeeded,
            request.Mechanism == SecurityMechanism.Negotiate ? SpnegoInitialToken.Encode(SpnegoMechanism.MitKerberosMechanismTypes, token) : token);
    }
}
