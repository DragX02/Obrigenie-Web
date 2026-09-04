using Obrigenie.Models;

namespace Obrigenie.Services
{
    // ─────────────────────────────────────────────────────────────────────────
    // Rend une préparation de leçon au format du formulaire papier
    // « Préparation de leçon type », en A4 portrait :
    //
    //     Titre de la leçon : ……                Enseignant : ……
    //     ┌───────────────────────────────────┐
    //     │ Durée de la leçon : ……            │
    //     │ Nombre de séances : ……            │
    //     │ Niveaux : ……                      │
    //     └───────────────────────────────────┘
    //     Compétences : ……
    //
    //     Déroulement de la leçon :
    //       Phase 1 : ……                Temps : ……
    //       Phase 2 : ……                Temps : ……
    //
    // Comme l'export du calendrier, il s'appuie sur PdfWriter : texte vectoriel,
    // sélectionnable et net à l'impression, sans bibliothèque externe.
    // ─────────────────────────────────────────────────────────────────────────
    public static class LeconPdfExporter
    {
        // Marge extérieure de la feuille
        private const float Marge = 45f;

        // Hauteurs de police
        private const float TailleTitre   = 15f;
        private const float TailleLibelle = 10.5f;
        private const float TailleTexte   = 10.5f;

        // Interligne d'un paragraphe
        private const float Interligne = 14f;

        // Largeur réservée à la colonne « Temps : … » d'une phase
        private const float LargeurTemps = 120f;

        // Construit le PDF d'une préparation et retourne ses octets.
        public static byte[] Generer(Lecon lecon)
        {
            var pdf = new PdfWriter(landscape: false);

            float largeurUtile = pdf.PageWidth - 2 * Marge;
            float y = Marge;

            // ── En-tête : titre à gauche, enseignant à droite ────────────────
            // Le titre peut être long : il occupe les deux tiers de la largeur et
            // passe à la ligne si nécessaire, l'enseignant garde le dernier tiers.
            float largeurTitre = largeurUtile * 0.62f;

            y = Champ(pdf, Marge, y, largeurTitre, "Titre de la leçon :", lecon.Titre, TailleTitre, gras: true);

            float xEnseignant = Marge + largeurTitre + 10f;
            Champ(pdf, xEnseignant, Marge, largeurUtile - largeurTitre - 10f,
                  "Enseignant :", lecon.Enseignant, TailleLibelle);

            // Le bloc suivant commence sous la plus basse des deux colonnes
            y = Math.Max(y, Marge + 2 * Interligne) + 8f;

            // ── Cadre : durée, nombre de séances, niveaux ────────────────────
            // Le formulaire papier encadre ces trois lignes ; le cadre est tracé
            // après coup, une fois la hauteur réellement occupée connue.
            float hautCadre = y;
            float yCadre    = y + 8f;

            yCadre = Champ(pdf, Marge + 10f, yCadre, largeurUtile - 20f, "Durée de la leçon :", lecon.Duree, TailleTexte);
            yCadre = Champ(pdf, Marge + 10f, yCadre, largeurUtile - 20f, "Nombre de séances :", lecon.NombreSeances.ToString(), TailleTexte);
            yCadre = Champ(pdf, Marge + 10f, yCadre, largeurUtile - 20f, "Niveaux :", lecon.Niveaux, TailleTexte);

            pdf.Rect(Marge, hautCadre, largeurUtile, yCadre + 4f - hautCadre, 0.8f, "0.35 0.35 0.35");

            y = yCadre + 22f;

            // ── Compétences ─────────────────────────────────────────────────
            y = Bloc(pdf, Marge, y, largeurUtile, "Compétences :", lecon.Competences);

            y += 12f;

            // ── Déroulement de la leçon ─────────────────────────────────────
            y = SauterSiBesoin(pdf, y, 3 * Interligne);
            pdf.Text(Marge, y, TailleLibelle, "Déroulement de la leçon :", gras: true);
            y += Interligne + 4f;

            // Une phase vide sur le formulaire papier reste une ligne à remplir à la
            // main : on la rend quand même, pour que la feuille imprimée serve aussi
            // de support de préparation.
            foreach (var phase in lecon.Phases.OrderBy(p => p.Ordre))
            {
                y = Phase(pdf, y, largeurUtile, phase);
            }

            return pdf.Build();
        }

        // Écrit « Libellé : valeur » sur une ligne, la valeur passant à la ligne
        // suivante si elle dépasse. Retourne l'ordonnée juste sous le champ.
        private static float Champ(PdfWriter pdf, float x, float y, float largeur,
                                   string libelle, string? valeur, float taille, bool gras = false)
        {
            y = SauterSiBesoin(pdf, y, Interligne);

            pdf.Text(x, y, taille, libelle, gras: gras);

            float xValeur   = x + PdfWriter.LargeurApprox(libelle, taille) + 6f;
            float dispoLig1 = largeur - (xValeur - x);

            var texte = PdfWriter.Nettoyer(valeur);

            // Champ non rempli : un filet pointillé tient la place, comme sur le papier
            if (string.IsNullOrWhiteSpace(texte))
            {
                pdf.Line(xValeur, y + taille, x + largeur, y + taille, 0.4f, "0.75 0.75 0.75");
                return y + Interligne;
            }

            // La première ligne partage sa hauteur avec le libellé, les suivantes
            // repartent de la marge du champ.
            var lignes = PdfWriter.Decouper(texte, taille, dispoLig1);

            pdf.Text(xValeur, y, taille, lignes[0]);
            y += Interligne;

            foreach (var ligne in lignes.Skip(1))
            {
                y = SauterSiBesoin(pdf, y, Interligne);
                pdf.Text(x, y, taille, ligne);
                y += Interligne;
            }

            return y;
        }

        // Écrit un libellé puis son texte libre en dessous, sur autant de lignes
        // que nécessaire. Retourne l'ordonnée juste sous le bloc.
        private static float Bloc(PdfWriter pdf, float x, float y, float largeur,
                                  string libelle, string? valeur)
        {
            y = SauterSiBesoin(pdf, y, 2 * Interligne);

            pdf.Text(x, y, TailleLibelle, libelle, gras: true);
            y += Interligne + 2f;

            var texte = PdfWriter.Nettoyer(valeur);

            // Bloc non rempli : trois filets, de quoi écrire à la main
            if (string.IsNullOrWhiteSpace(texte))
            {
                for (int i = 0; i < 3; i++)
                {
                    pdf.Line(x, y + TailleTexte, x + largeur, y + TailleTexte, 0.4f, "0.75 0.75 0.75");
                    y += Interligne;
                }
                return y;
            }

            foreach (var ligne in PdfWriter.Decouper(texte, TailleTexte, largeur))
            {
                y = SauterSiBesoin(pdf, y, Interligne);
                pdf.Text(x, y, TailleTexte, ligne);
                y += Interligne;
            }

            return y;
        }

        // Écrit une phase : son numéro et son intitulé à gauche, son temps à droite.
        // Retourne l'ordonnée juste sous la phase.
        private static float Phase(PdfWriter pdf, float y, float largeur, LeconPhase phase)
        {
            y = SauterSiBesoin(pdf, y, 2 * Interligne);

            float hautPhase   = y;
            float largeurGauche = largeur - LargeurTemps - 10f;

            // Colonne de droite : « Temps : … », aligné sur la première ligne
            float xTemps = Marge + largeur - LargeurTemps;
            Champ(pdf, xTemps, y, LargeurTemps, "Temps :", phase.Temps, TailleTexte);

            // Colonne de gauche : « Phase n : … », qui peut occuper plusieurs lignes
            y = Champ(pdf, Marge, y, largeurGauche, $"Phase {phase.Ordre} :", phase.Intitule, TailleTexte, gras: true);

            // La phase occupe au moins la hauteur de sa colonne de droite
            y = Math.Max(y, hautPhase + Interligne) + 6f;

            return y;
        }

        // Passe à une nouvelle page quand le bloc à écrire ne tient plus sur celle-ci.
        // Retourne l'ordonnée où écrire : inchangée, ou la marge haute de la page suivante.
        private static float SauterSiBesoin(PdfWriter pdf, float y, float hauteurBloc)
        {
            if (y + hauteurBloc <= pdf.PageHeight - Marge) return y;

            pdf.NewPage();
            return Marge;
        }

        // Nom de fichier proposé au téléchargement, dérivé du titre de la leçon.
        // Les caractères interdits par les systèmes de fichiers sont remplacés.
        public static string NomFichier(Lecon lecon)
        {
            var titre = PdfWriter.Nettoyer(lecon.Titre).Trim();
            if (titre.Length == 0) titre = "lecon";

            foreach (var interdit in System.IO.Path.GetInvalidFileNameChars())
                titre = titre.Replace(interdit, '-');

            if (titre.Length > 60) titre = titre[..60];

            return $"Preparation - {titre}.pdf";
        }
    }
}
