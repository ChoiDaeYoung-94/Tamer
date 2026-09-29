// Uses the project's existing com.unity.nuget.newtonsoft-json dependency.
using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

internal sealed class PrivateAdsContract
{
    public readonly string Checkout, AppId, RewardedUnit;
    private PrivateAdsContract(string checkout, string app, string unit)
    { Checkout = checkout; AppId = app; RewardedUnit = unit; }
    public static PrivateAdsContract Read(byte[] bytes, string checkout)
    {
        try { return ReadCore(bytes, checkout); }
        catch { throw Rejected(); }
    }
    private static PrivateAdsContract ReadCore(byte[] bytes, string checkout)
    {
        if (bytes == null || bytes.Length > 1048576) throw Rejected();
        var text = new UTF8Encoding(false, true).GetString(bytes);
        if (text.Length > 0 && text[0] == '\uFEFF') text = text.Substring(1);
        // Newtonsoft accepts extensions: check complete strict grammar first.
        new Grammar(text).Validate();
        JObject root;
        using (var input = new StringReader(text))
        using (var reader = new JsonTextReader(input) { DateParseHandling = DateParseHandling.None, MaxDepth = 64 })
        {
            root = JObject.Load(reader, new JsonLoadSettings {
                DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
            if (reader.Read()) throw Rejected();
        }
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
    private static string RequireString(JObject root, string name)
    {
        var value = root[name];
        if (value == null || value.Type != JTokenType.String || ((string)value).Length == 0) throw Rejected();
        return (string)value;
    }
    private static void RequireBool(JObject root, string name, bool expected)
    {
        var value = root[name];
        if (value == null || value.Type != JTokenType.Boolean || (bool)value != expected) throw Rejected();
    }
    private static Exception Rejected() { return new InvalidDataException("Private configuration rejected."); }

    // Syntax only: existing Newtonsoft decodes names and constructs values.
    private sealed class Grammar
    {
        private readonly string text;
        private int position;
        public Grammar(string value) { text = value; }
        public void Validate() { Value(0); Space(); if (position != text.Length) throw Rejected(); }
        private char Peek { get { return position < text.Length ? text[position] : '\0'; } }
        private void Space() { while (Peek == ' ' || Peek == '\t' || Peek == '\r' || Peek == '\n') position++; }
        private bool Take(char token) { if (Peek != token) return false; position++; return true; }
        private void Need(char token) { if (!Take(token)) throw Rejected(); }
        private void Value(int depth)
        {
            Space();
            if (Peek == '{' || Peek == '[')
            {
                if (depth >= 64) throw Rejected();
                bool obj = Take('{');
                if (!obj) Need('[');
                char end = obj ? '}' : ']';
                Space();
                if (Take(end)) return;
                do
                {
                    Space();
                    if (obj) { String(); Space(); Need(':'); }
                    Value(depth + 1);
                    Space();
                    if (Take(end)) return;
                    Need(','); // Next iteration requires a real member/value.
                } while (true);
            }
            if (Peek == '"') { String(); return; }
            foreach (var literal in new[] { "true", "false", "null" })
                if (position + literal.Length <= text.Length &&
                    string.CompareOrdinal(text, position, literal, 0, literal.Length) == 0)
                { position += literal.Length; return; }
            Number();
        }
        private void String()
        {
            Need('"');
            while (position < text.Length)
            {
                char ch = text[position++];
                if (ch == '"') return;
                if (ch < 0x20) throw Rejected();
                if (ch != '\\') continue;
                if (position == text.Length) throw Rejected();
                ch = text[position++];
                if (ch == 'u')
                {
                    for (int i = 0; i < 4; i++)
                    {
                        char digit = Peek;
                        if (!((digit >= '0' && digit <= '9') || (digit >= 'a' && digit <= 'f') ||
                              (digit >= 'A' && digit <= 'F'))) throw Rejected();
                        position++;
                    }
                }
                else if ("\"\\/bfnrt".IndexOf(ch) < 0) throw Rejected();
            }
            throw Rejected();
        }
        private bool Digit { get { return Peek >= '0' && Peek <= '9'; } }
        private void Digits() { if (!Digit) throw Rejected(); while (Digit) position++; }
        private void Number()
        {
            Take('-');
            if (!Take('0')) Digits();
            if (Take('.')) Digits();
            if (Take('e') || Take('E')) { if (!Take('+')) Take('-'); Digits(); }
        }
    }
}
