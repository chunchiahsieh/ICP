namespace TEL.IntegrationHub.Models.Ilc;

/// <summary>ILC dbo.Deposit_Head — only columns written by Hub.</summary>
public sealed class IlcDepositHead
{
    public int KeyId { get; set; }

    public string Status { get; set; } = "0";

    public string? InvNo { get; set; }

    public string? Gepo { get; set; }

    public string? Bu { get; set; }

    public DateTime SubmitDate { get; set; }

    public DateTime CreateDate { get; set; }

    public string Creator { get; set; } = "SYSTEM";
}

/// <summary>ILC dbo.Deposit_Import — only columns written by Hub.</summary>
public sealed class IlcDepositImport
{
    public int HeadkeyId { get; set; }

    public string? InvNo { get; set; }

    public string? Seq { get; set; }

    public string? ItemNo { get; set; }

    public string? Description { get; set; }

    public string? Qty { get; set; }

    public double? InvPrice { get; set; }

    public double? InvTotalPrice { get; set; }

    public string? Mawb { get; set; }

    public string? Hawb { get; set; }

    public DateTime? InvDate { get; set; }

    public string? FlightNo { get; set; }

    public string Creator { get; set; } = "SYSTEM";

    public DateTime CreateDate { get; set; }
}

/// <summary>ILC dbo.Deposit_Buyer — only columns written by Hub.</summary>
public sealed class IlcDepositBuyer
{
    public int HeadkeyId { get; set; }

    public string? ItemNo { get; set; }

    public string? Description { get; set; }

    public string? Qty { get; set; }

    public string Creator { get; set; } = "SYSTEM";

    public DateTime CreateDate { get; set; }
}
