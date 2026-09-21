using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using AD.Privacy;

// Test-only protected-store stand-in. The dictionary survives client object replacement, never app files.
public sealed class ReceiptTestKeys : IDeletionReceiptKeys
{
    public readonly Dictionary<string, byte[]> Keys = new Dictionary<string, byte[]>();
    public void Create(string alias)
    {
        if (Keys.ContainsKey(alias)) return;
        var bytes = new byte[32]; using (var random = RandomNumberGenerator.Create()) random.GetBytes(bytes);
        Keys.Add(alias, bytes);
    }
    public string Sign(string alias, string message)
    {
        if (!Keys.TryGetValue(alias, out var bytes)) throw new InvalidOperationException("Fixture protected key is missing.");
        using (var hmac = new HMACSHA256(bytes)) return BitConverter.ToString(hmac.ComputeHash(Encoding.UTF8.GetBytes(message))).Replace("-", "").ToLowerInvariant();
    }
    public void Delete(string alias) { Keys.Remove(alias); }
}
