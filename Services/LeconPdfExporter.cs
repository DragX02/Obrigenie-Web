using Obrigenie.Models;

namespace Obrigenie.Services
{
    // ─────────────────────────────────────────────────────────────────────────
    // Rend une préparation de leçon au format du formulaire papier
    // « Préparation de leçon type », en A4 portrait.
    //
    // La mise en page reprend celle du document d'origine, mesurée dans son XML
    // plutôt que devinée :
    //
    //   Titre de la leçon : ..................................................
    //                                    ┌──────────────────────────────────┐
    //                                    │ Enseignant :                     │
    //                                    │ Durée de la leçon :              │
    //                                    │ Nombre de séances :              │
    //                                    │ Niveaux :                        │
    //                                    └──────────────────────────────────┘
    //   Compétences :
    //       Année : …
    //       Cours : …
    //
    //                    Déroulement de la leçon :
    //
    //   Phase 1 : ...............        Temps :.......................
    //
    // Trois intitulés sont en gras souligné (le titre, les compétences, le
    // déroulement), et celui du déroulement est centré. Le cadre flotte à droite
    // sous la ligne de titre, ses quatre lignes bordées. Un champ vide reçoit des
    // points de conduite, comme sur le formulaire vierge : la feuille sert alors
    // de support à remplir à la main.
    //
    // Comme l'export du calendrier, il s'appuie sur PdfWriter : texte vectoriel,
    // sélectionnable et net à l'impression, sans bibliothèque externe.
    // ─────────────────────────────────────────────────────────────────────────
    public static class LeconPdfExporter
    {
        // Marge extérieure de la feuille
        private const float Marge = 45f;

        // Hauteurs de police
        private const float TailleTitre   = 13f;   // « Titre de la leçon : »
        private const float TailleSection = 11.5f; // « Compétences : », « Déroulement… »
        private const float TailleTexte   = 10f;   // corps des champs
        private const float TailleContexte = 9.5f; // le contexte de cascade, plus dense

        // Interligne d'une ligne de texte courante
        private const float Interligne = 14f;

        // Largeur du cadre, reprise du document d'origine (5410 twips = 270,5 pt).
        // Il est calé sur la marge de droite, comme dans le modèle où il flotte
        // contre le bord de la zone de texte.
        private const float LargeurCadre = 270.5f;

        // Hauteur d'une ligne du cadre
        private const float HauteurLigneCadre = 17f;

        // Les quatre lignes du cadre, dans l'ordre du formulaire
        private static readonly string[] LibellesCadre =
        {
            "Enseignant :", "Durée de la leçon :", "Nombre de séances :", "Niveaux :"
        };

        // Construit le PDF d'une préparation et retourne ses octets.
        public static byte[] Generer(Lecon lecon)
        {
            var pdf = new PdfWriter(landscape: false);

            float largeurUtile = pdf.PageWidth - 2 * Marge;
            float y = Marge;

            // ── Ligne de titre, sur toute la largeur ─────────────────────────
            y = ChampSouligne(pdf, Marge, y, largeurUtile,
                              "Titre de la leçon :", lecon.Titre, TailleTitre);

            y += 8f;

            // ── Cadre : enseignant, durée, séances, niveaux ──────────────────
            // Il flotte à droite ; le corps de la fiche reprend dessous.
            float xCadre = pdf.PageWidth - Marge - LargeurCadre;

            var valeursCadre = new[]
            {
                lecon.Enseignant, lecon.Duree, lecon.NombreSeances.ToString(), lecon.Niveaux
            };

            for (int i = 0; i < LibellesCadre.Length; i++)
            {
                float yLigne = y + i * HauteurLigneCadre;

                // Chaque ligne est encadrée : le modèle emploie un tableau à
                // bordures pleines, pas un simple cadre extérieur.
                pdf.Rect(xCadre, yLigne, LargeurCadre, HauteurLigneCadre, 0.7f, "0.2 0.2 0.2");

                Champ(pdf, xCadre + 5f, yLigne + 3.5f, LargeurCadre - 10f,
                      LibellesCadre[i], valeursCadre[i], TailleTexte);
            }

            y += LibellesCadre.Length * HauteurLigneCadre + 18f;

            // ── Compétences ─────────────────────────────────────────────────
            y = SauterSiBesoin(pdf, y, 3 * Interligne);
            y = Souligne(pdf, Marge, y, "Compétences :", TailleSection);
            y += 6f;

            // Le contexte composé par la cascade est décalé vers l'intérieur,
            // comme sur le modèle où il forme un bloc en retrait.
            y = Contexte(pdf, Marge + 22f, y, largeurUtile - 22f, lecon.Competences);

            y += 20f;

            // ── Déroulement de la leçon, centré ─────────────────────────────
            y = SauterSiBesoin(pdf, y, 4 * Interligne);

            const string titreDeroulement = "Déroulement de la leçon :";
            float xCentre = Marge + (largeurUtile - PdfWriter.LargeurApprox(titreDeroulement, TailleSection)) / 2f;
            y = Souligne(pdf, xCentre, y, titreDeroulement, TailleSection);

            y += 14f;

            // ── Les phases, sur deux colonnes ───────────────────────────────
            // Une phase vide reste une ligne à remplir à la main : elle est rendue
            // quand même, points de conduite compris.
            foreach (var phase in lecon.Phases.OrderBy(p => p.Ordre))
            {
                y = Phase(pdf, y, largeurUtile, phase);
            }

            return pdf.Build();
        }

        // ── Blocs ────────────────────────────────────────────────────────────

        // Écrit un intitulé de section en gras souligné.
        // Retourne l'ordonnée juste sous l'intitulé.
        private static float Souligne(PdfWriter pdf, float x, float y, string texte, float taille)
        {
            pdf.Text(x, y, taille, texte, gras: true);

            // PdfWriter ne connaît pas le soulignement : on le trace sous le texte,
            // à la largeur estimée de la chaîne.
            float bas = y + taille + 1.5f;
            pdf.Line(x, bas, x + PdfWriter.LargeurApprox(texte, taille), bas, 0.7f, "0 0 0");

            return y + taille + 6f;
        }

        // Écrit « Libellé : valeur » où le libellé est en gras souligné et la
        // valeur occupe le reste de la ligne (points de conduite si elle est vide).
        private static float ChampSouligne(PdfWriter pdf, float x, float y, float largeur,
                                           string libelle, string? valeur, float taille)
        {
            pdf.Text(x, y, taille, libelle, gras: true);

            float largeurLibelle = PdfWriter.LargeurApprox(libelle, taille);
            float bas = y + taille + 1.5f;
            pdf.Line(x, bas, x + largeurLibelle, bas, 0.7f, "0 0 0");

            float xValeur = x + largeurLibelle + 8f;
            Valeur(pdf, xValeur, y, x + largeur - xValeur, valeur, taille);

            return y + taille + 8f;
        }

        // Écrit « Libellé : valeur » sur une ligne, sans soulignement.
        private static void Champ(PdfWriter pdf, float x, float y, float largeur,
                                  string libelle, string? valeur, float taille)
        {
            pdf.Text(x, y, taille, libelle);

            float xValeur = x + PdfWriter.LargeurApprox(libelle, taille) + 5f;
            Valeur(pdf, xValeur, y, x + largeur - xValeur, valeur, taille);
        }

        // Écrit la valeur d'un champ, ou des points de conduite quand elle est vide.
        // La valeur est tronquée si elle dépasse : sur ces lignes, le modèle n'en
        // prévoit qu'une seule.
        private static void Valeur(PdfWriter pdf, float x, float y, float largeur,
                                   string? valeur, float taille)
        {
            if (largeur <= 0) return;

            var texte = PdfWriter.Nettoyer(valeur).Replace("\n", " ").Trim();

            if (texte.Length == 0)
            {
                pdf.Text(x, y, taille, Points(largeur, taille), couleur: "0.45 0.45 0.45");
                return;
            }

            pdf.Text(x, y, taille, PdfWriter.Tronquer(texte, taille, largeur));
        }

        // Suite de points remplissant la largeur donnée, comme les points de
        // conduite du formulaire vierge.
        private static string Points(float largeur, float taille)
        {
            int nombre = (int)(largeur / (taille * 0.5f));
            return nombre <= 0 ? string.Empty : new string('.', nombre);
        }

        // Écrit le contexte de cascade, une ligne par niveau, en conservant
        // l'indentation qui aligne les visées sous la première.
        // Retourne l'ordonnée juste sous le bloc.
        private static float Contexte(PdfWriter pdf, float x, float y, float largeur, string? valeur)
        {
            var texte = PdfWriter.Nettoyer(valeur);

            // Compétences non renseignées : trois lignes de points à compléter
            if (string.IsNullOrWhiteSpace(texte))
            {
                for (int i = 0; i < 3; i++)
                {
                    y = SauterSiBesoin(pdf, y, Interligne);
                    pdf.Text(x, y, TailleContexte, Points(largeur, TailleContexte), couleur: "0.45 0.45 0.45");
                    y += Interligne;
                }
                return y;
            }

            foreach (var ligne in PdfWriter.Decouper(texte, TailleContexte, largeur))
            {
                y = SauterSiBesoin(pdf, y, Interligne);
                pdf.Text(x, y, TailleContexte, ligne);
                y += Interligne;
            }

            return y;
        }

        // Écrit une phase sur deux colonnes : « Phase n : … » à gauche,
        // « Temps : … » à droite. Retourne l'ordonnée juste sous la phase.
        private static float Phase(PdfWriter pdf, float y, float largeur, LeconPhase phase)
        {
            y = SauterSiBesoin(pdf, y, 2 * Interligne);

            // La colonne de droite commence un peu après la moitié, comme sur le
            // modèle où « Temps » est nettement détaché de l'intitulé.
            float largeurGauche = largeur * 0.50f;
            float xTemps        = Marge + largeur * 0.56f;

            // Largeur restant à l'intitulé une fois « Phase n : » écrit devant
            var libelle = $"Phase {phase.Ordre} :";
            float largeurIntitule = largeurGauche - PdfWriter.LargeurApprox(libelle, TailleTexte) - 5f;

            var lignes = LignesIntitule(phase.Intitule, largeurIntitule);

            Champ(pdf, Marge, y, largeurGauche, libelle,
                  lignes.Count == 0 ? string.Empty : lignes[0], TailleTexte);
            Champ(pdf, xTemps, y, Marge + largeur - xTemps, "Temps :", phase.Temps, TailleTexte);

            y += Interligne;

            // Un intitulé de plusieurs lignes déborde sous sa colonne de gauche,
            // sans empiéter sur le temps.
            foreach (var ligne in lignes.Skip(1))
            {
                y = SauterSiBesoin(pdf, y, Interligne);
                pdf.Text(Marge + 12f, y, TailleTexte, ligne);
                y += Interligne;
            }

            // Le modèle espace nettement les phases : la fiche vierge doit laisser
            // la place d'écrire à la main entre les lignes.
            return y + 12f;
        }

        // Découpe l'intitulé d'une phase à la largeur qui lui reste, une fois
        // « Phase n : » écrit devant. Liste vide quand la phase n'est pas remplie.
        private static List<string> LignesIntitule(string? intitule, float largeur)
        {
            var texte = PdfWriter.Nettoyer(intitule);
            if (string.IsNullOrWhiteSpace(texte)) return new List<string>();

            return PdfWriter.Decouper(texte, TailleTexte, largeur);
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
