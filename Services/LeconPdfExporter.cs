using Obrigenie.Models;

namespace Obrigenie.Services
{
    public static class LeconPdfExporter
    {
        private const float Marge = 45f;

        private const float MargeBordure = 22f;

        // Hauteurs de police
        private const float TailleTitre   = 13f;   // « Titre de la leçon : »
        private const float TailleSection = 11.5f; // « Compétences : », « Déroulement… »
        private const float TailleTexte   = 10f;   // corps des champs
        private const float TailleContexte = 9.5f; // le contexte de cascade, plus dense

        private const float Interligne = 14f;

        private const float LargeurCadre = 270.5f;

        private const float HauteurLigneCadre = 17f;

        private static readonly string[] LibellesCadre =
        {
            "Enseignant :", "Durée de la leçon :", "Nombre de séances :", "Niveaux :"
        };

        public static byte[] Generer(Lecon lecon)
        {
            var pdf = new PdfWriter(landscape: false);

            BordurePage(pdf);

            float largeurUtile = pdf.PageWidth - 2 * Marge;
            float y = EnTete(pdf);

            y = ChampSouligne(pdf, Marge, y, largeurUtile,
                              "Titre de la leçon :", lecon.Titre, TailleTitre);

            y += 8f;

            float xCadre = pdf.PageWidth - Marge - LargeurCadre;

            var valeursCadre = new[]
            {
                lecon.Enseignant, lecon.Duree, lecon.NombreSeances.ToString(), lecon.Niveaux
            };

            for (int i = 0; i < LibellesCadre.Length; i++)
            {
                float yLigne = y + i * HauteurLigneCadre;

                pdf.Rect(xCadre, yLigne, LargeurCadre, HauteurLigneCadre, 0.7f, "0.2 0.2 0.2");

                Champ(pdf, xCadre + 5f, yLigne + 3.5f, LargeurCadre - 10f,
                      LibellesCadre[i], valeursCadre[i], TailleTexte);
            }

            y += LibellesCadre.Length * HauteurLigneCadre + 18f;

            y = SauterSiBesoin(pdf, y, 3 * Interligne);
            y = Souligne(pdf, Marge, y, "Compétences :", TailleSection);
            y += 6f;

            y = Contexte(pdf, Marge + 22f, y, largeurUtile - 22f, lecon.Competences);

            y += 20f;

            y = SauterSiBesoin(pdf, y, 4 * Interligne);

            const string titreDeroulement = "Déroulement de la leçon :";
            float xCentre = Marge + (largeurUtile - PdfWriter.LargeurApprox(titreDeroulement, TailleSection)) / 2f;
            y = Souligne(pdf, xCentre, y, titreDeroulement, TailleSection);

            y += 14f;

            foreach (var phase in lecon.Phases.OrderBy(p => p.Ordre))
            {
                y = Phase(pdf, y, largeurUtile, phase);
            }

            return pdf.Build();
        }

        private static void BordurePage(PdfWriter pdf)
        {
            float largeur = pdf.PageWidth - 2 * MargeBordure;
            float hauteur = pdf.PageHeight - 2 * MargeBordure;

            pdf.Rect(MargeBordure, MargeBordure, largeur, hauteur, 2f, "0 0 0");
        }

        private static float EnTete(PdfWriter pdf)
        {
            float tailleLogo = 20f;
            float yLogo = MargeBordure + 10f;

            pdf.Image(Marge, yLogo, tailleLogo, tailleLogo,
                      LogoObrigenie.Jpeg, LogoObrigenie.Largeur, LogoObrigenie.Hauteur);

            pdf.Text(Marge + tailleLogo + 5f, yLogo + 6f, 11f, "Obrigenie", true);

            string titre = "Préparation de leçon";
            float largeurTitre = PdfWriter.LargeurApprox(titre, 14f);
            pdf.Text((pdf.PageWidth - largeurTitre) / 2f, yLogo + 4f, 14f, titre, true);

            int debut = CalendarService.AnneeScolaire(DateTime.Now);
            string annee = $"Année scolaire {debut}-{debut + 1}";
            pdf.Text(pdf.PageWidth - Marge - PdfWriter.LargeurApprox(annee, 9f), yLogo + 8f, 9f,
                     annee, false, "0.35 0.35 0.35");

            float yTrait = yLogo + tailleLogo + 8f;
            pdf.Line(Marge, yTrait, pdf.PageWidth - Marge, yTrait, 0.7f, "0.6 0.6 0.6");

            return yTrait + 16f;
        }

        private static float Souligne(PdfWriter pdf, float x, float y, string texte, float taille)
        {
            pdf.Text(x, y, taille, texte, gras: true);

            float bas = y + taille + 1.5f;
            pdf.Line(x, bas, x + PdfWriter.LargeurApprox(texte, taille), bas, 0.7f, "0 0 0");

            return y + taille + 6f;
        }

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

        private static void Champ(PdfWriter pdf, float x, float y, float largeur,
                                  string libelle, string? valeur, float taille)
        {
            pdf.Text(x, y, taille, libelle);

            float xValeur = x + PdfWriter.LargeurApprox(libelle, taille) + 5f;
            Valeur(pdf, xValeur, y, x + largeur - xValeur, valeur, taille);
        }

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

        private static string Points(float largeur, float taille)
        {
            int nombre = (int)(largeur / (taille * 0.5f));
            return nombre <= 0 ? string.Empty : new string('.', nombre);
        }

        private static float Contexte(PdfWriter pdf, float x, float y, float largeur, string? valeur)
        {
            var texte = PdfWriter.Nettoyer(valeur);

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

        private static float Phase(PdfWriter pdf, float y, float largeur, LeconPhase phase)
        {
            y = SauterSiBesoin(pdf, y, 2 * Interligne);

            float largeurGauche = largeur * 0.50f;
            float xTemps        = Marge + largeur * 0.56f;

            var libelle = $"Phase {phase.Ordre} :";
            float largeurIntitule = largeurGauche - PdfWriter.LargeurApprox(libelle, TailleTexte) - 5f;

            var lignes = LignesIntitule(phase.Intitule, largeurIntitule);

            Champ(pdf, Marge, y, largeurGauche, libelle,
                  lignes.Count == 0 ? string.Empty : lignes[0], TailleTexte);
            Champ(pdf, xTemps, y, Marge + largeur - xTemps, "Temps :", phase.Temps, TailleTexte);

            y += Interligne;
            foreach (var ligne in lignes.Skip(1))
            {
                y = SauterSiBesoin(pdf, y, Interligne);
                pdf.Text(Marge + 12f, y, TailleTexte, ligne);
                y += Interligne;
            }

            return y + 12f;
        }

        private static List<string> LignesIntitule(string? intitule, float largeur)
        {
            var texte = PdfWriter.Nettoyer(intitule);
            if (string.IsNullOrWhiteSpace(texte)) return new List<string>();

            return PdfWriter.Decouper(texte, TailleTexte, largeur);
        }

        private static float SauterSiBesoin(PdfWriter pdf, float y, float hauteurBloc)
        {
            if (y + hauteurBloc <= pdf.PageHeight - Marge) return y;

            pdf.NewPage();
            BordurePage(pdf);

            return EnTete(pdf);
        }

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
