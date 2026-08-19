namespace Obrigenie.Services
{
    // ─────────────────────────────────────────────────────────────────────────
    // Couleur d'affichage d'un congé scolaire.
    //
    // Chaque congé reçoit une couleur stable déduite de son nom : les vacances de
    // Noël sont toujours rouges, celles de Carnaval toujours violettes, dans toutes
    // les vues et d'un chargement à l'autre. Un nom inconnu (congé ajouté par
    // l'utilisateur, jour férié local) prend une couleur de la palette choisie par
    // empreinte du nom — donc elle aussi stable.
    //
    // Les teintes sont volontairement moyennes en luminosité : lisibles en texte sur
    // fond clair comme sur fond sombre, et utilisables en fond une fois transparentes.
    // ─────────────────────────────────────────────────────────────────────────
    public static class HolidayColors
    {
        // Couleurs par mot-clé, testées dans l'ordre sur le nom sans accent en minuscules.
        // Les libellés officiels varient ("Conge d'automne (Toussaint)", "Vacances d'automne"),
        // d'où la recherche par mot-clé plutôt que par nom exact.
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

        // Palette de repli pour les noms non reconnus, indexée par empreinte du nom.
        private static readonly string[] Palette =
        {
            "#0277BD", "#6A1B9A", "#AD1457", "#EF6C00", "#2E7D32", "#00838F", "#5D4037",
        };

        // Couleur hexadécimale (#RRGGBB) associée à un nom de congé.
        public static string Pour(string? nom)
        {
            if (string.IsNullOrWhiteSpace(nom)) return Palette[0];

            var normalise = TexteUtil.SansAccents(nom).ToLowerInvariant();

            foreach (var (motCle, couleur) in ParMotCle)
            {
                if (normalise.Contains(motCle, StringComparison.Ordinal)) return couleur;
            }

            // Empreinte simple et déterministe : le même nom donne toujours la même
            // couleur, y compris entre deux sessions ou deux navigateurs.
            int empreinte = 0;
            foreach (var c in normalise) empreinte = (empreinte * 31 + c) & 0x7FFFFFFF;

            return Palette[empreinte % Palette.Length];
        }

        // Même couleur en version fond translucide, posée sur la cellule du jour.
        // L'alpha en notation #RRGGBBAA garde une teinte lisible sur les deux thèmes :
        // assez visible sur fond blanc, sans écraser le texte sur fond sombre.
        public static string Fond(string? nom, string alpha = "2E") => Pour(nom) + alpha;

        // Composantes rouge/vert/bleu normalisées (0–1), au format attendu par le
        // flux de contenu PDF ("0.9 0.45 0").
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
