using System.Globalization;
using System.Text;

namespace Obrigenie.Services
{
    // Petites opérations de texte partagées par les services du calendrier.
    public static class TexteUtil
    {
        // Retire les accents d'une chaîne ("Rentrée" → "Rentree").
        //
        // Les noms de congés viennent de sources différentes : le scraper écrit
        // "Rentrée scolaire", certaines entrées historiques "Rentree scolaire".
        // Comparer sur la forme sans accent évite de manquer une variante.
        public static string SansAccents(string? texte)
        {
            if (string.IsNullOrEmpty(texte)) return string.Empty;

            // Décomposition Unicode : "é" devient "e" suivi d'un accent combinant,
            // que l'on écarte ensuite pour ne garder que la lettre de base.
            return new string(texte.Normalize(NormalizationForm.FormD)
                .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                .ToArray());
        }
    }
}
