using System.Globalization;
using System.Text;

namespace Obrigenie.Services
{
    public static class TexteUtil
    {
        public static string SansAccents(string? texte)
        {
            if (string.IsNullOrEmpty(texte)) return string.Empty;

            return new string(texte.Normalize(NormalizationForm.FormD)
                .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                .ToArray());
        }
    }
}
