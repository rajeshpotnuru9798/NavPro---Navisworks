using System;

namespace NavPro.Backend.Core
{
    public sealed class ClashTestNameParts
    {
        public bool IsValid { get; internal set; }
        public string Priority { get; internal set; } = string.Empty;
        public string DisciplineA { get; internal set; } = string.Empty;
        public string DisciplineB { get; internal set; } = string.Empty;
        public string ValidationMessage { get; internal set; } = string.Empty;
    }

    public static class ClashTestNameParser
    {
        private const string Separator = "_VS_";
        private const string ExpectedFormat = "Priority_DisciplineA_VS_DisciplineB";

        public static ClashTestNameParts Parse(string name)
        {
            var parts = new ClashTestNameParts();
            if (string.IsNullOrWhiteSpace(name)) return Invalid(parts);
            int first = name.IndexOf('_');
            int separator = first < 0 ? -1 : name.IndexOf(Separator, first + 1, StringComparison.OrdinalIgnoreCase);
            int lastSeparator = name.LastIndexOf(Separator, StringComparison.OrdinalIgnoreCase);
            if (first <= 0 || separator <= first + 1 || separator != lastSeparator || separator + Separator.Length >= name.Length)
                return Invalid(parts);

            string priority = name.Substring(0, first);
            string disciplineA = name.Substring(first + 1, separator - first - 1);
            string disciplineB = name.Substring(separator + Separator.Length);
            if (!IsCleanSegment(priority) || !IsCleanSegment(disciplineA) || !IsCleanSegment(disciplineB) ||
                IsSeparatorToken(priority) || IsSeparatorToken(disciplineA) || IsSeparatorToken(disciplineB))
                return Invalid(parts);

            parts.IsValid = true; parts.Priority = priority; parts.DisciplineA = disciplineA; parts.DisciplineB = disciplineB;
            parts.ValidationMessage = "Valid naming convention.";
            return parts;
        }

        private static bool IsCleanSegment(string value) => !string.IsNullOrWhiteSpace(value) && string.Equals(value, value.Trim(), StringComparison.Ordinal);
        private static bool IsSeparatorToken(string value) => string.Equals(value, "VS", StringComparison.OrdinalIgnoreCase);
        private static ClashTestNameParts Invalid(ClashTestNameParts parts)
        {
            parts.ValidationMessage = "Invalid clash test name. Expected " + ExpectedFormat + ", for example H_CI-Irrigation_VS_CS-Storm.";
            return parts;
        }
    }
}
