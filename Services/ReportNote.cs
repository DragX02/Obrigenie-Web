using Obrigenie.Models;

namespace Obrigenie.Services
{
    public static class ReportNote
    {
        private const string Fleche = "↪";

        private static readonly string[] Cles = { "Reporté au ", "Reporte au " };

        public const string FormatDate = "dd/MM/yyyy";

        private static readonly System.Globalization.CultureInfo Neutre =
            System.Globalization.CultureInfo.InvariantCulture;

        private static readonly string[] FormatsLus = { "dd/MM/yyyy", "dd-MM-yyyy", "dd.MM.yyyy" };

        public static (string Texte, DateTime? Cible) Lire(string? content)
        {
            if (string.IsNullOrEmpty(content)) return (string.Empty, null);

            var lignes = content.Replace("\r\n", "\n").Split('\n');
            var gardees = new List<string>(lignes.Length);
            DateTime? cible = null;

            foreach (var ligne in lignes)
            {
                var date = DateMarqueur(ligne);
                if (date != null)
                {
                    cible = date;
                    continue;
                }
                gardees.Add(ligne);
            }

            return (string.Join("\n", gardees).TrimEnd('\n', ' '), cible);
        }

        public static string Texte(string? content) => Lire(content).Texte;

        public static DateTime? Cible(string? content) => Lire(content).Cible;

        public static string Libelle(DateTime cible)
            => $"{Fleche} Reporté au {cible.ToString(FormatDate, Neutre)}";

        public static string? Infobulle(Note note)
            => Cible(note.Content) is DateTime c ? $"Reporté au {c.ToString(FormatDate, Neutre)}" : null;

        public static Note Copier(Note source, DateTime cible) => new Note
        {
            Id            = 0,
            Date          = new DateTime(cible.Year, cible.Month, cible.Day, 0, 0, 0, DateTimeKind.Utc),
            Hour          = source.Hour,
            Minute        = source.Minute,
            EndHour       = source.EndHour,
            EndMinute     = source.EndMinute,
            Content       = Texte(source.Content),
            Titre         = source.Titre,
            IdViseeFk     = source.IdViseeFk,
            ViseeContexte = source.ViseeContexte,
        };

        public static DateTime DateCopie(Note note, DateTime reference, DateTime cible)
            => note.Date.Date.AddDays((cible.Date - reference.Date).Days);

        private static DateTime? DateMarqueur(string ligne)
        {
            var t = ligne.Trim().TrimStart(Fleche[0], '>', '-', ' ');

            foreach (var cle in Cles)
            {
                if (!t.StartsWith(cle, StringComparison.OrdinalIgnoreCase)) continue;

                var reste = t[cle.Length..].Trim();
                if (DateTime.TryParseExact(reste, FormatsLus, Neutre,
                        System.Globalization.DateTimeStyles.None, out var date))
                    return date;

                return null;
            }

            return null;
        }
    }
}
