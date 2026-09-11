using System;

namespace AD.Advertising
{
    /// <summary>Reads the existing comma-delimited purchase value without changing its format.</summary>
    public static class AdEntitlement
    {
        public static bool HasNoAds(string purchases)
        {
            if (string.IsNullOrEmpty(purchases))
                return false;

            return Array.Exists(purchases.Split(','), item =>
                string.Equals(item.Trim(), "ProductNoAds", StringComparison.Ordinal));
        }
    }
}
