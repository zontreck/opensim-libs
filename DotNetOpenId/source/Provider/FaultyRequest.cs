namespace DotNetOpenId.Provider;

internal class FaultyRequest : Request
{
    internal FaultyRequest(OpenIdProvider provider, IEncodable response)
        : base(provider)
    {
        Response = response;
    }

    public new IEncodable Response { get; }

    internal override string Mode => null;

    public override bool IsResponseReady => true;

    protected override IEncodable CreateResponse()
    {
        return Response;
    }
}