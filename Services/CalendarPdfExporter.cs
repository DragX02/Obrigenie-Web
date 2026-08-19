using Obrigenie.Models;

namespace Obrigenie.Services
{
    // ─────────────────────────────────────────────────────────────────────────
    // Mise en page des exports PDF du calendrier.
    //
    // Chaque vue a sa méthode : la vue jour sort en portrait (une colonne, grille
    // verticale), les grilles semaine / mois / trimestre en paysage. Le dessin
    // passe par PdfWriter, donc le PDF produit est vectoriel et ne dépend d'aucune
    // bibliothèque externe.
    // ─────────────────────────────────────────────────────────────────────────
    public static class CalendarPdfExporter
    {
        // Marges de page et couleurs, communes à toutes les vues
        private const float Marge      = 24f;
        private const float HautGrille = 52f;
        private const string GrisTrait = "0.65 0.65 0.65";
        private const string GrisTexte = "0.35 0.35 0.35";
        private const string VertCours = "0.11 0.37 0.13";
        private const string OrangeNote = "0.9 0.45 0";

        // ── Vue jour : portrait ──────────────────────────────────────────────

        // Grille horaire d'une journée : colonne d'heures à gauche, notes fusionnées
        // à droite sur toute leur durée, comme à l'écran.
        public static byte[] Jour(string titre, Day jour, int heureDebut, int heureFin)
        {
            var pdf = new PdfWriter(landscape: false);
            Titre(pdf, titre);

            float largeurLabel = 44f;
            float xGrille      = Marge;
            float xContenu     = Marge + largeurLabel;
            float largeurCont  = pdf.PageWidth - Marge - xContenu;

            int nbLignes    = Math.Max(1, heureFin - heureDebut);
            float hauteur   = pdf.PageHeight - HautGrille - Marge;
            float hLigne    = hauteur / nbLignes;

            // Cadre extérieur de la grille
            pdf.Rect(xGrille, HautGrille, pdf.PageWidth - 2 * Marge, hauteur, 0.8f, GrisTrait);
            pdf.Line(xContenu, HautGrille, xContenu, HautGrille + hauteur, 0.8f, GrisTrait);

            // Étiquettes d'heure + séparateurs de lignes
            for (int h = heureDebut; h < heureFin; h++)
            {
                float y = HautGrille + (h - heureDebut) * hLigne;
                if (h > heureDebut) pdf.Line(xGrille, y, pdf.PageWidth - Marge, y, 0.4f, GrisTrait);
                pdf.Text(xGrille + 5, y + 5, 8.5f, $"{h:D2}:00", false, GrisTexte);
            }

            // Blocs de notes fusionnés sur leur durée
            foreach (var bloc in NoteLayout.Blocs(jour.Notes, heureDebut, heureFin))
            {
                float y = HautGrille + (bloc.Start - heureDebut) * hLigne;
                float h = (bloc.End - bloc.Start) * hLigne;

                pdf.Rect(xContenu + 3, y + 3, largeurCont - 6, h - 6, 0.8f, "0.85 0.55 0.1");
                DessinerNotes(pdf, bloc.Notes, xContenu + 8, y + 8, largeurCont - 16, h - 16, 8.5f, complet: true);
            }

            return pdf.Build();
        }

        // ── Vues semaine / semaine+ / mois : paysage ─────────────────────────

        // Grille de jours en colonnes. `colonnes` vaut 5 (semaine), 7 (semaine+)
        // ou 7 avec plusieurs lignes (mois).
        public static byte[] Grille(string titre, IReadOnlyList<Day> jours, int colonnes)
        {
            var pdf = new PdfWriter(landscape: true);
            Titre(pdf, titre);

            if (jours.Count == 0 || colonnes <= 0) return pdf.Build();

            int lignes = (int)Math.Ceiling(jours.Count / (double)colonnes);

            float largeurTotale = pdf.PageWidth - 2 * Marge;
            float hauteurTotale = pdf.PageHeight - HautGrille - Marge;
            float lCell = largeurTotale / colonnes;
            float hCell = hauteurTotale / lignes;

            // Une seule ligne de cellules (semaine) : le détail des notes tient largement,
            // on peut donc écrire le contexte de cascade complet.
            bool detail = lignes == 1;

            for (int i = 0; i < jours.Count; i++)
            {
                var jour = jours[i];
                float x = Marge + (i % colonnes) * lCell;
                float y = HautGrille + (i / colonnes) * hCell;

                pdf.Rect(x, y, lCell, hCell, 0.6f, GrisTrait);

                // En-tête de cellule : nom du jour abrégé + numéro
                var entete = $"{Abreger(jour.DayOfWeek)} {jour.DayOfMonth}";
                pdf.Text(x + 5, y + 4, 9f, PdfWriter.Nettoyer(entete), true);

                float yTexte = y + 17;

                // Nom de vacances éventuel
                if (!string.IsNullOrEmpty(jour.ShortHolidayName))
                {
                    pdf.Text(x + 5, yTexte, 7.5f,
                             PdfWriter.Tronquer(PdfWriter.Nettoyer(jour.ShortHolidayName), 7.5f, lCell - 10),
                             false, OrangeNote);
                    yTexte += 10;
                }

                // Cours du jour, avec leur horaire
                foreach (var cours in jour.Courses)
                {
                    if (yTexte > y + hCell - 10) break;
                    var ligne = $"{cours.StartTime:hh\\:mm}-{cours.EndTime:hh\\:mm} {cours.Name}";
                    pdf.Text(x + 5, yTexte, 7.5f,
                             PdfWriter.Tronquer(PdfWriter.Nettoyer(ligne), 7.5f, lCell - 10), false, VertCours);
                    yTexte += 10;
                }

                // Notes du jour
                var notes = jour.Notes.OrderBy(n => n.Hour).ThenBy(n => n.Minute).ToList();
                DessinerNotes(pdf, notes, x + 5, yTexte, lCell - 10, y + hCell - yTexte - 3, 7.5f, complet: detail);
            }

            return pdf.Build();
        }

        // ── Vue trimestre : paysage ──────────────────────────────────────────

        // Une semaine de la période scolaire, projetée pour l'export.
        public sealed class PeriodeSemaine
        {
            public string Entete = string.Empty;
            public bool   VacancesCompletes;
            public string VacancesLibelle = string.Empty;
            public List<PeriodeJour> Jours = new();
        }

        // Un jour d'une semaine de la période scolaire, projeté pour l'export.
        public sealed class PeriodeJour
        {
            public bool DansPeriode;
            public bool Vacances;
            public int  NbNotes;
            public string PremierCours = string.Empty;
        }

        // Tableau semaines (colonnes) × jours Lun–Ven (lignes).
        public static byte[] Periode(string titre, IReadOnlyList<PeriodeSemaine> semaines,
                                     IReadOnlyList<string> nomsJours)
        {
            var pdf = new PdfWriter(landscape: true);
            Titre(pdf, titre);

            if (semaines.Count == 0) return pdf.Build();

            float largeurJours = 42f;
            float largeurTotale = pdf.PageWidth - 2 * Marge - largeurJours;
            float hauteurTotale = pdf.PageHeight - HautGrille - Marge;

            float lCol = largeurTotale / semaines.Count;
            float hEntete = 24f;
            float hLigne = (hauteurTotale - hEntete) / Math.Max(1, nomsJours.Count);

            // Colonne fixe des noms de jours
            for (int j = 0; j < nomsJours.Count; j++)
            {
                float y = HautGrille + hEntete + j * hLigne;
                pdf.Rect(Marge, y, largeurJours, hLigne, 0.6f, GrisTrait);
                pdf.Text(Marge + 5, y + hLigne / 2 - 4, 8f, PdfWriter.Nettoyer(nomsJours[j]), true);
            }

            // Une colonne par semaine
            for (int s = 0; s < semaines.Count; s++)
            {
                var semaine = semaines[s];
                float x = Marge + largeurJours + s * lCol;

                // En-tête : numéro de semaine et plage de dates
                pdf.Rect(x, HautGrille, lCol, hEntete, 0.6f, GrisTrait);
                pdf.Text(x + 3, HautGrille + 4, 7f,
                         PdfWriter.Tronquer(PdfWriter.Nettoyer(semaine.Entete), 7f, lCol - 6), true);

                // Semaine entièrement en vacances : une seule case sur toute la colonne
                if (semaine.VacancesCompletes)
                {
                    float hTotale = hLigne * nomsJours.Count;
                    pdf.Rect(x, HautGrille + hEntete, lCol, hTotale, 0.6f, GrisTrait);
                    pdf.Text(x + 3, HautGrille + hEntete + hTotale / 2 - 4, 7f,
                             PdfWriter.Tronquer(PdfWriter.Nettoyer(semaine.VacancesLibelle), 7f, lCol - 6),
                             false, OrangeNote);
                    continue;
                }

                for (int j = 0; j < nomsJours.Count; j++)
                {
                    float y = HautGrille + hEntete + j * hLigne;
                    pdf.Rect(x, y, lCol, hLigne, 0.5f, GrisTrait);

                    if (j >= semaine.Jours.Count) continue;
                    var jour = semaine.Jours[j];
                    if (!jour.DansPeriode) continue;

                    float yTexte = y + 3;

                    if (jour.Vacances)
                    {
                        pdf.Text(x + 3, yTexte, 7f, "Conge", false, OrangeNote);
                        yTexte += 9;
                    }

                    if (!string.IsNullOrEmpty(jour.PremierCours))
                    {
                        pdf.Text(x + 3, yTexte, 6.5f,
                                 PdfWriter.Tronquer(PdfWriter.Nettoyer(jour.PremierCours), 6.5f, lCol - 6),
                                 false, VertCours);
                        yTexte += 9;
                    }

                    if (jour.NbNotes > 0)
                        pdf.Text(x + 3, yTexte, 6.5f, $"{jour.NbNotes} note(s)", false, GrisTexte);
                }
            }

            return pdf.Build();
        }

        // ── Éléments communs ─────────────────────────────────────────────────

        // Titre centré en haut de la première page.
        private static void Titre(PdfWriter pdf, string titre)
        {
            var texte = PdfWriter.Nettoyer(titre);
            float largeur = PdfWriter.LargeurApprox(texte, 14f);
            pdf.Text(Math.Max(Marge, (pdf.PageWidth - largeur) / 2), 22f, 14f, texte, true);
        }

        // Écrit une liste de notes dans le rectangle donné, en s'arrêtant dès que la
        // place manque. `complet` ajoute le contexte de cascade complet ; sinon seul
        // le cours est repris, pour les cellules étroites de la vue mois.
        private static void DessinerNotes(PdfWriter pdf, IReadOnlyList<Note> notes,
                                          float x, float y, float largeur, float hauteur,
                                          float taille, bool complet)
        {
            float yCourant = y;
            float yMax     = y + hauteur;
            float interligne = taille + 1.5f;

            foreach (var note in notes)
            {
                if (yCourant + interligne > yMax) return;

                // Plage horaire, en gras
                pdf.Text(x, yCourant, taille, NoteLayout.PlageHoraire(note), true, OrangeNote);
                yCourant += interligne;

                // Contexte de cascade : toutes les lignes si la place le permet,
                // sinon uniquement le cours
                var contexte = complet
                    ? PdfWriter.Nettoyer(note.ViseeContexte)
                    : PdfWriter.Nettoyer(NoteLayout.CourseLabel(note));

                foreach (var ligne in PdfWriter.Decouper(contexte, taille - 0.5f, largeur))
                {
                    if (yCourant + interligne > yMax) return;
                    pdf.Text(x, yCourant, taille - 0.5f, ligne, false, VertCours);
                    yCourant += interligne;
                }

                // Texte libre de la note
                foreach (var ligne in PdfWriter.Decouper(PdfWriter.Nettoyer(note.Content), taille - 0.5f, largeur))
                {
                    if (yCourant + interligne > yMax) return;
                    pdf.Text(x, yCourant, taille - 0.5f, ligne);
                    yCourant += interligne;
                }

                yCourant += 3;
            }
        }

        // Abrège un nom de jour ("lundi" → "Lun") pour les en-têtes de cellules.
        private static string Abreger(string nomJour)
            => string.IsNullOrEmpty(nomJour) ? string.Empty
             : char.ToUpperInvariant(nomJour[0]) + nomJour[1..Math.Min(3, nomJour.Length)];
    }
}
