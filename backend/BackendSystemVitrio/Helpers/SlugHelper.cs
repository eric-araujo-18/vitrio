using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace BackendSystemVitrio.Helpers
{
    // Centraliza utilitários de texto usados por vários services
    // (antes cada service tinha sua própria cópia de Slugify/OnlyDigits).
    public static class SlugHelper
    {
        
        public static string Slugify(string? value, string fallback = "item")
        {
            if (string.IsNullOrWhiteSpace(value))
                return fallback;

            var normalized = value.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();

            foreach (var c in normalized)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                    sb.Append(c);
            }

            var slug = sb.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();
            slug = Regex.Replace(slug, @"[^a-z0-9\s-]", "");
            slug = Regex.Replace(slug, @"[\s-]+", "-").Trim('-');

            return string.IsNullOrWhiteSpace(slug) ? fallback : slug;
        }

        public static string? OnlyDigits(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            var digits = Regex.Replace(value, @"\D", "");
            return digits.Length == 0 ? null : digits;
        }

        // Valida cor no formato #RRGGBB (o <input type="color"> sempre manda assim).
        public static bool IsHexColor(string? value)
            => value is not null && Regex.IsMatch(value, "^#[0-9a-fA-F]{6}$");
    }
}
