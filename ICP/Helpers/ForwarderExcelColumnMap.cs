namespace ICP.Helpers;

public static class ForwarderExcelColumnMap
{
    public const string Type = nameof(ICP.Models.Icp.ForwarderDataUpload.Type);
    public const string InvoiceNo = nameof(ICP.Models.Icp.ForwarderDataUpload.InvoiceNo);
    public const string CustomerReference = nameof(ICP.Models.Icp.ForwarderDataUpload.CustomerReference);
    public const string MaterialCode = nameof(ICP.Models.Icp.ForwarderDataUpload.MaterialCode);
    public const string OrderMaterialName = nameof(ICP.Models.Icp.ForwarderDataUpload.OrderMaterialName);
    public const string Quantity = nameof(ICP.Models.Icp.ForwarderDataUpload.Quantity);
    public const string PortOfLoading = nameof(ICP.Models.Icp.ForwarderDataUpload.PortOfLoading);
    public const string ShipToName = nameof(ICP.Models.Icp.ForwarderDataUpload.ShipToName);
    public const string ShipToAddress = nameof(ICP.Models.Icp.ForwarderDataUpload.ShipToAddress);
    public const string ShipToPartyCountryCode = nameof(ICP.Models.Icp.ForwarderDataUpload.ShipToPartyCountryCode);
    public const string ShipToPortCode = nameof(ICP.Models.Icp.ForwarderDataUpload.ShipToPortCode);
    public const string FreightCharge = nameof(ICP.Models.Icp.ForwarderDataUpload.FreightCharge);
    public const string ConfirmedCustomDate = nameof(ICP.Models.Icp.ForwarderDataUpload.ConfirmedCustomDate);
    public const string Hawb = nameof(ICP.Models.Icp.ForwarderDataUpload.Hawb);
    public const string Mawb = nameof(ICP.Models.Icp.ForwarderDataUpload.Mawb);
    public const string Etd = nameof(ICP.Models.Icp.ForwarderDataUpload.Etd);
    public const string Eta = nameof(ICP.Models.Icp.ForwarderDataUpload.Eta);
    public const string Flight1 = nameof(ICP.Models.Icp.ForwarderDataUpload.Flight1);
    public const string Flight2 = nameof(ICP.Models.Icp.ForwarderDataUpload.Flight2);
    public const string Cb = nameof(ICP.Models.Icp.ForwarderDataUpload.Cb);
    public const string Action = nameof(ICP.Models.Icp.ForwarderDataUpload.Action);
    public const string Mdp = nameof(ICP.Models.Icp.ForwarderDataUpload.Mdp);

    private static readonly Dictionary<string, string> HeaderAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Type"] = Type,
        ["類型"] = Type,
        ["InvoiceNo"] = InvoiceNo,
        ["Invoice No"] = InvoiceNo,
        ["Invoice No."] = InvoiceNo,
        ["Invoice Number"] = InvoiceNo,
        ["發票號碼"] = InvoiceNo,
        ["CustomerReference"] = CustomerReference,
        ["Customer Reference"] = CustomerReference,
        ["Customer Ref"] = CustomerReference,
        ["MaterialCode"] = MaterialCode,
        ["Material Code"] = MaterialCode,
        ["OrderMaterialName"] = OrderMaterialName,
        ["Order Material Name"] = OrderMaterialName,
        ["Material Name"] = OrderMaterialName,
        ["Quantity"] = Quantity,
        ["Qty"] = Quantity,
        ["PortOfLoading"] = PortOfLoading,
        ["Port Of Loading"] = PortOfLoading,
        ["Port of Loading"] = PortOfLoading,
        ["POL"] = PortOfLoading,
        ["ShipToName"] = ShipToName,
        ["Ship To Name"] = ShipToName,
        ["Ship to Name"] = ShipToName,
        ["ShipToAddress"] = ShipToAddress,
        ["Ship To Address"] = ShipToAddress,
        ["Ship to Address"] = ShipToAddress,
        ["ShipToPartyCountryCode"] = ShipToPartyCountryCode,
        ["Ship To Party Country"] = ShipToPartyCountryCode,
        ["Ship to Party Country Code"] = ShipToPartyCountryCode,
        ["Ship To Party Country Code"] = ShipToPartyCountryCode,
        ["Country"] = ShipToPartyCountryCode,
        ["ShipToPortCode"] = ShipToPortCode,
        ["Ship To Port Code"] = ShipToPortCode,
        ["Ship to Port Code"] = ShipToPortCode,
        ["FreightCharge"] = FreightCharge,
        ["Freight charge"] = FreightCharge,
        ["Freight Charge"] = FreightCharge,
        ["ConfirmedCustomDate"] = ConfirmedCustomDate,
        ["Confirmed Custom Date"] = ConfirmedCustomDate,
        ["HAWB"] = Hawb,
        ["Hawb"] = Hawb,
        ["MAWB"] = Mawb,
        ["Mawb"] = Mawb,
        ["ETD"] = Etd,
        ["Etd"] = Etd,
        ["ETA"] = Eta,
        ["Eta"] = Eta,
        ["Flight1"] = Flight1,
        ["FLIGHT 1#"] = Flight1,
        ["Flight 1#"] = Flight1,
        ["Flight2"] = Flight2,
        ["FLIGHT 2#"] = Flight2,
        ["Flight 2#"] = Flight2,
        ["Cb"] = Cb,
        ["C/B"] = Cb,
        ["Action"] = Action,
        ["MDP"] = Mdp,
        ["Mdp"] = Mdp
    };

    public static bool TryResolveProperty(string? header, out string propertyName)
    {
        propertyName = string.Empty;
        if (string.IsNullOrWhiteSpace(header))
        {
            return false;
        }

        var normalized = NormalizeHeader(header);
        return HeaderAliases.TryGetValue(normalized, out propertyName!);
    }

    private static string NormalizeHeader(string header)
    {
        var normalized = header.Trim().Replace('\u00A0', ' ');
        while (normalized.Contains("  ", StringComparison.Ordinal))
        {
            normalized = normalized.Replace("  ", " ", StringComparison.Ordinal);
        }

        return normalized;
    }
}
