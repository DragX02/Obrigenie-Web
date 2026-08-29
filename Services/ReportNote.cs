using Obrigenie.Models;

namespace Obrigenie.Services
{
    // ─────────────────────────────────────────────────────────────────────────
    // Report d'une leçon sur une autre date.
    //
    // Reporter ne déplace pas la note : elle est recopiée telle quelle (horaire,
    // texte, visée) sur la date choisie, et l'originale garde la mention du
    // report pour qu'on voie, sur la semaine d'origine, où la leçon est repartie.
    //
    // Cette mention vit dans le texte de la note, sur une dernière ligne dédiée
    // ("↪ Reporté au 06/10/2025"), et non dans une colonne dédiée : le schéma de
    // production se modifie à la main et l'enregistrement d'une note existante ne
    // met à jour que son contenu et son horaire. Le marqueur est retiré partout où
    // la note est affichée ou modifiée, puis réécrit à l'enregistrement : il n'est
    // jamais montré sous sa forme brute.
    // ─────────────────────────────────────────────────────────────────────────
    public static class ReportNote
    {
        // Début de la ligne de marqueur. La flèche sert de repère visuel dans la
        // base ; la reconnaissance, elle, ne dépend que du mot "Reporté au".
        private const string Fleche = "↪";

        // Mot-clé reconnu, avec et sans accent : selon la saisie, une note relue
        // puis réenregistrée peut avoir perdu ses accents en cours de route.
        private static readonly string[] Cles = { "Reporté au ", "Reporte au " };

        // Format de la date écrite dans le marqueur et affiché à l'utilisateur.
        public const string FormatDate = "dd/MM/yyyy";

        // Le marqueur est écrit et relu en culture invariante : la note voyage entre
        // navigateurs (et le calendrier se change de langue en cours de route), et une
        // date écrite avec le séparateur d'une culture serait illisible par une autre.
        private static readonly System.Globalization.CultureInfo Neutre =
            System.Globalization.CultureInfo.InvariantCulture;

        // Séparateurs acceptés à la relecture, pour rattraper les marqueurs écrits
        // avant cette règle par une culture qui n'utilisait pas la barre oblique.
        private static readonly string[] FormatsLus = { "dd/MM/yyyy", "dd-MM-yyyy", "dd.MM.yyyy" };

        // Longueur maximale du contenu acceptée par le serveur ; au-delà, il tronque.
        // Le texte libre cède la place au marqueur plutôt que l'inverse, sans quoi
        // la troncature couperait la mention de report.
        private const int MaxContenu = 2000;

        // Sépare le texte libre de la note de son éventuelle mention de report.
        // Retourne le texte débarrassé du marqueur et la date cible quand il y en a une.
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
                    // Plusieurs marqueurs ne devraient pas coexister ; si cela arrive,
                    // le dernier écrit fait foi et les autres sont écartés du texte.
                    cible = date;
                    continue;
                }
                gardees.Add(ligne);
            }

            // Le marqueur était précédé d'une ligne vide : elle n'a plus lieu d'être
            return (string.Join("\n", gardees).TrimEnd('\n', ' '), cible);
        }

        // Texte libre seul, sans la mention de report.
        public static string Texte(string? content) => Lire(content).Texte;

        // Date de report portée par la note, ou null quand elle n'a pas été reportée.
        public static DateTime? Cible(string? content) => Lire(content).Cible;

        // Réécrit le contenu avec la mention de report vers `cible`.
        // Une mention déjà présente est remplacée : reporter deux fois une leçon
        // laisse une seule ligne, celle du dernier report.
        public static string Marquer(string? content, DateTime cible)
        {
            var texte = Texte(content);
            var marqueur = Libelle(cible);

            if (string.IsNullOrEmpty(texte)) return marqueur;

            // Le texte libre est rogné si nécessaire pour que le marqueur tienne
            // dans les 2000 caractères acceptés par le serveur.
            int place = MaxContenu - marqueur.Length - 1;
            if (place < 0) return marqueur;
            if (texte.Length > place) texte = texte[..place];

            return $"{texte}\n{marqueur}";
        }

        // Libellé court affiché dans la case de la leçon d'origine — et forme exacte
        // du marqueur écrit dans le texte de la note.
        public static string Libelle(DateTime cible)
            => $"{Fleche} Reporté au {cible.ToString(FormatDate, Neutre)}";

        // Infobulle de la case d'une leçon : mention de report quand elle existe, sinon null.
        public static string? Infobulle(Note note)
            => Cible(note.Content) is DateTime c ? $"Reporté au {c.ToString(FormatDate, Neutre)}" : null;

        // Construit la copie d'une note pour la date cible.
        // La copie repart sans mention de report : c'est elle, la leçon reportée.
        // L'identifiant est laissé à 0 pour que le serveur la crée au lieu d'écraser l'originale.
        public static Note Copier(Note source, DateTime cible) => new Note
        {
            Id            = 0,
            // Minuit en UTC, comme partout ailleurs, pour qu'aucun décalage horaire
            // ne fasse glisser la copie sur le jour précédent à la sérialisation.
            Date          = new DateTime(cible.Year, cible.Month, cible.Day, 0, 0, 0, DateTimeKind.Utc),
            Hour          = source.Hour,
            Minute        = source.Minute,
            EndHour       = source.EndHour,
            EndMinute     = source.EndMinute,
            Content       = Texte(source.Content),
            IdViseeFk     = source.IdViseeFk,
            ViseeContexte = source.ViseeContexte,
        };

        // Date à laquelle une leçon atterrit quand on copie sa source vers `cible`.
        //
        // Toute la copie tient dans ce décalage. Copier une leçon ou une journée prend
        // le jour lui-même comme référence : le décalage mène droit à la date choisie.
        // Copier une semaine prend son lundi comme référence : le décalage vaut alors un
        // multiple de 7, et chaque leçon garde son jour de semaine dans la semaine visée.
        public static DateTime DateCopie(Note note, DateTime reference, DateTime cible)
            => note.Date.Date.AddDays((cible.Date - reference.Date).Days);

        // Reconnaît une ligne de marqueur et en extrait la date, ou null si la ligne
        // est du texte ordinaire.
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

                // Mot-clé reconnu mais date illisible : la ligne reste du texte,
                // mieux vaut l'afficher telle quelle que la faire disparaître.
                return null;
            }

            return null;
        }
    }
}
