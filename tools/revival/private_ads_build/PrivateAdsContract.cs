// Staged into this checkout's Editor directory only by private_ads_build.py.
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text.RegularExpressions;
using System.Xml;

internal sealed class PrivateAdsContract
{
    public readonly string Checkout, AppId, RewardedUnit;
    private PrivateAdsContract(string checkout, string app, string unit)
    { Checkout = checkout; AppId = app; RewardedUnit = unit; }

    public static PrivateAdsContract Read(byte[] bytes, string checkout)
    {
        // XML JSON mapping retains every member, unlike deserializing to a dictionary.
        // Walk every object to reject duplicate *decoded* names, including nested ones.
        var doc = new XmlDocument { XmlResolver = null };
        using (var reader = JsonReaderWriterFactory.CreateJsonReader(bytes,
            new XmlDictionaryReaderQuotas { MaxDepth = 64, MaxStringContentLength = 1048576,
                MaxArrayLength = 1048576, MaxBytesPerRead = 4096, MaxNameTableCharCount = 1048576 }))
            doc.Load(reader);
        var root = doc.DocumentElement;
        if (root == null || root.GetAttribute("type") != "object") throw Rejected();
        ValidateMembers(root);
        RequireBool(root, "consoleInventoryConfirmed", true);
        RequireBool(root, "productionActivationApproved", false);
        RequireBool(root, "regionalReviewApproved", false);
        var owner = RequireString(root, "checkout");
        var app = RequireString(root, "androidAppId");
        var unit = RequireString(root, "productionRewardedAdUnit");
        if (!Path.IsPathRooted(owner) || !string.Equals(Path.GetFullPath(owner).TrimEnd('\\', '/'),
            Path.GetFullPath(checkout).TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase) ||
            !Regex.IsMatch(app, @"\Aca-app-pub-[0-9]{16}~[0-9]{10}\z") ||
            !Regex.IsMatch(unit, @"\Aca-app-pub-[0-9]{16}/[0-9]{10}\z") ||
            app.StartsWith("ca-app-pub-3940256099942544~", StringComparison.Ordinal) ||
            app.Split('~')[0] != unit.Split('/')[0]) throw Rejected();
        return new PrivateAdsContract(Path.GetFullPath(owner), app, unit);
    }

    private static string Name(XmlElement node)
    { return node.HasAttribute("item") ? node.GetAttribute("item") : node.LocalName; }
    private static void ValidateMembers(XmlElement node)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (XmlNode child in node.ChildNodes)
        {
            var member = child as XmlElement;
            if (member == null) continue;
            if (node.GetAttribute("type") == "object" && !names.Add(Name(member))) throw Rejected();
            ValidateMembers(member);
        }
    }
    private static XmlElement Member(XmlElement root, string name)
    {
        foreach (XmlNode child in root.ChildNodes)
            if (child is XmlElement && Name((XmlElement)child) == name) return (XmlElement)child;
        throw Rejected();
    }
    private static string RequireString(XmlElement root, string name)
    {
        var member = Member(root, name);
        if (member.GetAttribute("type") != "string" || member.InnerText.Length == 0) throw Rejected();
        return member.InnerText;
    }
    private static void RequireBool(XmlElement root, string name, bool expected)
    {
        var member = Member(root, name);
        if (member.GetAttribute("type") != "boolean" || member.InnerText != (expected ? "true" : "false"))
            throw Rejected();
    }
    private static Exception Rejected() { return new InvalidDataException("Private configuration rejected."); }
}
