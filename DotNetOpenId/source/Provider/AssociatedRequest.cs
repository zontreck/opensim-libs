using System;
using System.Globalization;

namespace DotNetOpenId.Provider;

internal abstract class AssociatedRequest : Request
{
    protected AssociatedRequest(OpenIdProvider provider) : base(provider)
    {
    }

    internal string AssociationHandle { get; set; }

    public override string ToString()
    {
        var returnString = "AssociatedRequest.AssocHandle = {0}";
        return base.ToString() + Environment.NewLine + string.Format(CultureInfo.CurrentCulture,
            returnString, AssociationHandle);
    }
}