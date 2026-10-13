using Microsoft.AspNetCore.DataProtection;
using System.Text.Json;

namespace ICP.Services;

public sealed class TariffOverwriteConfirmationException(string[] invoices, string token) : Exception
{
    public string[] Invoices { get; } = invoices;
    public string Token { get; } = token;
}

public static class TariffOverwriteConfirmation
{
    private sealed record Payload(string FileHash, string User, string Snapshot, DateTime ExpiresUtc);

    public static string Create(IDataProtector protector, string fileHash, string user, string snapshot) =>
        protector.Protect(JsonSerializer.Serialize(new Payload(fileHash, user, snapshot, DateTime.UtcNow.AddMinutes(15))));

    public static bool IsValid(IDataProtector protector, string? token, string fileHash, string user, string snapshot)
    {
        if (string.IsNullOrEmpty(token)) return false;
        try
        {
            var payload = JsonSerializer.Deserialize<Payload>(protector.Unprotect(token));
            return payload is not null && payload.ExpiresUtc > DateTime.UtcNow
                && payload.FileHash == fileHash && payload.User == user && payload.Snapshot == snapshot;
        }
        catch (System.Security.Cryptography.CryptographicException) { return false; }
        catch (JsonException) { return false; }
    }
}
