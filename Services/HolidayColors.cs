namespace Obrigenie.Services
{
    public static class HolidayColors
    {
        private static readonly (string MotCle, string Couleur)[] ParMotCle =
        {
            ("rentree",   "#2E7D32"),   // vert : reprise de l'année
            ("toussaint", "#E65100"),   // orange automne
            ("automne",   "#E65100"),
            ("noel",      "#C62828"),   // rouge Noël
            ("hiver",     "#C62828"),
            ("carnaval",  "#6A1B9A"),   // violet Carnaval
            ("detente",   "#6A1B9A"),
            ("paques",    "#00897B"),   // vert-bleu printemps
            ("printemps", "#00897B"),
            ("ete",       "#0277BD"),   // bleu été
            ("armistice", "#455A64"),   // gris-bleu commémoration
            ("ferie",     "#455A64"),
            ("fete",      "#AD1457"),   // rose fête
            ("pedagogiq", "#5D4037"),   // brun journée pédagogique
        };

        private static readonly string[] Palette =
        {
            "#0277BD", "#6A1B9A", "#AD1457", "#EF6C00", "#2E7D32", "#00838F", "#5D4037",
        };

        public static string Cle(string? nom)
        {
            if (string.IsNullOrWhiteSpace(nom)) return string.Empty;

            var normalise = TexteUtil.SansAccents(nom).ToLowerInvariant();

            foreach (var (motCle, _) in ParMotCle)
            {
                if (normalise.Contains(motCle, StringComparison.Ordinal)) return motCle;
            }

            return normalise;
        }

        public static string Pour(string? nom)
        {
            if (string.IsNullOrWhiteSpace(nom)) return Palette[0];

            var normalise = TexteUtil.SansAccents(nom).ToLowerInvariant();

            foreach (var (motCle, couleur) in ParMotCle)
            {
                if (normalise.Contains(motCle, StringComparison.Ordinal)) return couleur;
            }

            int empreinte = 0;
            foreach (var c in normalise) empreinte = (empreinte * 31 + c) & 0x7FFFFFFF;

            return Palette[empreinte % Palette.Length];
        }

        public static string Fond(string? nom, string alpha = "2E") => Pour(nom) + alpha;

        public static string VersPdf(string? nom)
        {
            var hex = Pour(nom);

            float Composante(int debut) =>
                Convert.ToInt32(hex.Substring(debut, 2), 16) / 255f;

            return string.Format(System.Globalization.CultureInfo.InvariantCulture,
                                 "{0:0.##} {1:0.##} {2:0.##}",
                                 Composante(1), Composante(3), Composante(5));
        }
    }
}
