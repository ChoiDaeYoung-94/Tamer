namespace AD.Advertising
{
    /// <summary>Saved self-reported age is not consent or a reviewed regional treatment.</summary>
    public static class AgeTreatmentPolicy
    {
        // The pinned SDK supports Child/Teen, but regional UMP treatment and
        // Families native close behavior are not verified. No bucket starts UMP.
        // A later reviewed composition must supply both contracts before enabling requests.
        public static bool IsReviewed(AgeChoice choice) => false;
    }
}
