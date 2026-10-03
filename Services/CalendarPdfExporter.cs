using Obrigenie.Models;

namespace Obrigenie.Services
{
    public static class CalendarPdfExporter
    {
        private const float Marge      = 24f;
        private const float HautGrille = 58f;
        private const string GrisTrait = "0.65 0.65 0.65";
        private const string GrisTexte = "0.35 0.35 0.35";
        private const string VertCours = "0.11 0.37 0.13";
        private const string OrangeNote = "0.9 0.45 0";

        public static byte[] Jour(string titre, Day jour, int heureDebut, int heureFin,
                                  string? identite = null, string? anneeScolaire = null)
        {
            var pdf = new PdfWriter(landscape: false);
            Titre(pdf, titre, identite, anneeScolaire);

            if (!string.IsNullOrEmpty(jour.HolidayName))
            {
                var conge = PdfWriter.Nettoyer(jour.HolidayName);
                float largeur = PdfWriter.LargeurApprox(conge, 9f);
                pdf.Text(Math.Max(Marge, (pdf.PageWidth - largeur) / 2), 46f, 9f, conge, true,
                         HolidayColors.VersPdf(jour.HolidayName));
            }

            float largeurLabel = 44f;
            float xGrille      = Marge;
            float xContenu     = Marge + largeurLabel;
            float largeurCont  = pdf.PageWidth - Marge - xContenu;

            var heures = HeuresOccupees(new[] { jour }, heureDebut, heureFin);
            var ligneDe = IndexerLignes(heures);

            float hauteur   = pdf.PageHeight - HautGrille - Marge;
            float hLigne    = hauteur / heures.Count;

            pdf.Rect(xGrille, HautGrille, pdf.PageWidth - 2 * Marge, hauteur, 0.8f, GrisTrait);
            pdf.Line(xContenu, HautGrille, xContenu, HautGrille + hauteur, 0.8f, GrisTrait);

            for (int i = 0; i < heures.Count; i++)
            {
                float y = HautGrille + i * hLigne;
                if (i > 0) pdf.Line(xGrille, y, pdf.PageWidth - Marge, y, 0.4f, GrisTrait);
                pdf.Text(xGrille + 5, y + 5, 8.5f, $"{heures[i]:D2}:00", false, GrisTexte);
            }

            foreach (var bloc in NoteLayout.Blocs(jour.Notes, heureDebut, heureFin))
            {
                float y = HautGrille + ligneDe[bloc.Start] * hLigne;
                float h = (ligneDe[bloc.End - 1] - ligneDe[bloc.Start] + 1) * hLigne;

                pdf.Rect(xContenu + 3, y + 3, largeurCont - 6, h - 6, 0.8f, "0.85 0.55 0.1");
                DessinerNotes(pdf, bloc.Notes, xContenu + 8, y + 8, largeurCont - 16, h - 16, 10.5f, complet: true);
            }

            return pdf.Build();
        }

        private const float TailleSemaine     = 10f;
        private const float InterligneSemaine = TailleSemaine + 2f;
        private const float RetraitCadre      = 2f;
        private const float MargeCadre        = 5f;
        private const string OrangeCadre      = "0.85 0.55 0.1";

        private readonly record struct LigneCadre(string Texte, bool Gras, string Couleur, float Espace = 0f);

        public static byte[] Semaine(string titre, IReadOnlyList<Day> jours, int heureDebut, int heureFin,
                                     string? identite = null, string? anneeScolaire = null)
        {
            var pdf = new PdfWriter(landscape: true);
            Titre(pdf, titre, identite, anneeScolaire);

            if (jours.Count == 0) return pdf.Build();

            const float t = TailleSemaine;
            float largeurLabel = 46f;
            float hEntete      = 22f;

            float xJours       = Marge + largeurLabel;
            float lCol         = (pdf.PageWidth - Marge - xJours) / jours.Count;
            float largeurTexte = lCol - 2 * (RetraitCadre + MargeCadre);
            float hDispo       = pdf.PageHeight - HautGrille - Marge - hEntete;

            var heures = HeuresOccupees(jours, heureDebut, heureFin);

            var cadres = jours
                .Select(j => heures.Select(h => LignesCadre(j, h, heureDebut, heureFin, largeurTexte)).ToList())
                .ToList();

            float hMin = HauteurCadre(new[] { new LigneCadre("", false, "") });
            var hauteurs = heures
                .Select((_, i) => Math.Min(hDispo, Math.Max(hMin, cadres.Max(c => HauteurCadre(c[i])))))
                .ToList();

            var pages = new List<List<int>> { new() };
            float cumul = 0;
            for (int i = 0; i < heures.Count; i++)
            {
                if (pages[^1].Count > 0 && cumul + hauteurs[i] > hDispo)
                {
                    pages.Add(new List<int>());
                    cumul = 0;
                }
                pages[^1].Add(i);
                cumul += hauteurs[i];
            }

            for (int p = 0; p < pages.Count; p++)
            {
                if (p > 0)
                {
                    pdf.NewPage();
                    Titre(pdf, titre, identite, anneeScolaire);
                }

                var lignes = pages[p];
                float bonus   = (hDispo - lignes.Sum(i => hauteurs[i])) / lignes.Count;
                float hauteur = hEntete + hDispo;

                pdf.Rect(Marge, HautGrille, pdf.PageWidth - 2 * Marge, hauteur, 0.8f, GrisTrait);
                pdf.Line(xJours, HautGrille, xJours, HautGrille + hauteur, 0.8f, GrisTrait);
                pdf.Line(Marge, HautGrille + hEntete, pdf.PageWidth - Marge, HautGrille + hEntete, 0.8f, GrisTrait);

                for (int d = 0; d < jours.Count; d++)
                {
                    var jour = jours[d];
                    float x = xJours + d * lCol;

                    if (d > 0) pdf.Line(x, HautGrille, x, HautGrille + hauteur, 0.5f, GrisTrait);

                    var entete = PdfWriter.Nettoyer($"{NomComplet(jour.DayOfWeek)} {jour.DayOfMonth}");
                    pdf.Text(x + 4, HautGrille + 5, t, entete, true);

                    if (!string.IsNullOrEmpty(jour.ShortHolidayName))
                    {
                        float largeurEntete = PdfWriter.LargeurApprox(entete, t) + 8;
                        pdf.Text(x + largeurEntete, HautGrille + 5, t,
                                 PdfWriter.Tronquer(PdfWriter.Nettoyer(jour.ShortHolidayName), t, lCol - largeurEntete - 6),
                                 false, HolidayColors.VersPdf(jour.ShortHolidayName));
                    }
                }

                float y = HautGrille + hEntete;
                for (int k = 0; k < lignes.Count; k++)
                {
                    int i = lignes[k];
                    float h = hauteurs[i] + bonus;

                    if (k > 0) pdf.Line(Marge, y, xJours, y, 0.4f, GrisTrait);
                    pdf.Text(Marge + 4, y + RetraitCadre + MargeCadre, t, $"{heures[i]:D2}:00", false, GrisTexte);

                    for (int d = 0; d < jours.Count; d++)
                    {
                        var contenu = cadres[d][i];
                        bool aNote  = contenu.Any(l => l.Couleur == OrangeNote);

                        float xc = xJours + d * lCol + RetraitCadre;
                        float yc = y + RetraitCadre;
                        float lc = lCol - 2 * RetraitCadre;
                        float hc = h - 2 * RetraitCadre;

                        if (jours[d].Courses.Any(c => CoursCouvre(c, heures[i], heureDebut, heureFin)))
                            pdf.FillRect(xc, yc, lc, hc, "0.93 0.93 0.93");
                        pdf.Rect(xc, yc, lc, hc, aNote ? 0.8f : 0.4f, aNote ? OrangeCadre : GrisTrait);

                        float yTexte = yc + MargeCadre;
                        float yMax   = yc + hc - MargeCadre;
                        foreach (var ligne in contenu)
                        {
                            yTexte += ligne.Espace;
                            if (yTexte + InterligneSemaine > yMax + 2f) break;
                            pdf.Text(xc + MargeCadre, yTexte, t, ligne.Texte, ligne.Gras, ligne.Couleur);
                            yTexte += InterligneSemaine;
                        }
                    }

                    y += h;
                }
            }

            return pdf.Build();
        }

        private static List<LigneCadre> LignesCadre(Day jour, int heure, int heureDebut, int heureFin, float largeur)
        {
            const float t = TailleSemaine;
            var lignes = new List<LigneCadre>();

            foreach (var cours in jour.Courses)
            {
                if (Math.Max(cours.StartTime.Hours, heureDebut) != heure
                    || !CoursCouvre(cours, heure, heureDebut, heureFin)) continue;

                var libelle = PdfWriter.Nettoyer($"{cours.StartTime:hh\\:mm}-{cours.EndTime:hh\\:mm} {cours.Name}");
                lignes.Add(new LigneCadre(PdfWriter.Tronquer(libelle, t, largeur), true, VertCours));
            }

            foreach (var note in jour.Notes.OrderBy(n => n.Hour).ThenBy(n => n.Minute))
            {
                if (Math.Max(note.Hour, heureDebut) != heure
                    || note.Hour >= heureFin || NoteLayout.RowEnd(note) <= heureDebut) continue;

                lignes.Add(new LigneCadre(NoteLayout.PlageHoraire(note), true, OrangeNote, lignes.Count > 0 ? 4f : 0f));

                foreach (var l in PdfWriter.Decouper(PdfWriter.Nettoyer(note.Titre), t, largeur))
                    lignes.Add(new LigneCadre(l, true, "0 0 0"));

                if (ReportNote.Cible(note.Content) is DateTime cible)
                    lignes.Add(new LigneCadre(PdfWriter.Nettoyer($"-> Reporte au {cible:dd/MM/yyyy}"), false, OrangeNote));

                foreach (var l in PdfWriter.Decouper(PdfWriter.Nettoyer(note.ViseeContexte), t, largeur))
                    lignes.Add(new LigneCadre(l, false, VertCours));

                foreach (var l in PdfWriter.Decouper(PdfWriter.Nettoyer(ReportNote.Texte(note.Content)), t, largeur))
                    lignes.Add(new LigneCadre(l, false, "0 0 0"));
            }

            return lignes;
        }

        private static float HauteurCadre(IEnumerable<LigneCadre> lignes)
            => 2 * (RetraitCadre + MargeCadre) + lignes.Sum(l => l.Espace + InterligneSemaine);

        private static bool CoursCouvre(Course cours, int heure, int heureDebut, int heureFin)
        {
            int debut = Math.Max(cours.StartTime.Hours, heureDebut);
            int fin   = Math.Min(cours.EndTime.Minutes > 0 ? cours.EndTime.Hours + 1 : cours.EndTime.Hours, heureFin);
            return heure >= debut && heure < fin;
        }

        public static byte[] Grille(string titre, IReadOnlyList<Day> jours, int colonnes,
                                    string? identite = null, string? anneeScolaire = null)
        {
            var pdf = new PdfWriter(landscape: true);
            Titre(pdf, titre, identite, anneeScolaire);

            if (jours.Count == 0 || colonnes <= 0) return pdf.Build();

            int lignes = (int)Math.Ceiling(jours.Count / (double)colonnes);

            float largeurTotale = pdf.PageWidth - 2 * Marge;
            float hauteurTotale = pdf.PageHeight - HautGrille - Marge;
            float lCell = largeurTotale / colonnes;
            float hCell = hauteurTotale / lignes;

            bool detail = lignes == 1;

            for (int i = 0; i < jours.Count; i++)
            {
                var jour = jours[i];
                float x = Marge + (i % colonnes) * lCell;
                float y = HautGrille + (i / colonnes) * hCell;

                pdf.Rect(x, y, lCell, hCell, 0.6f, GrisTrait);

                var entete = $"{Abreger(jour.DayOfWeek)} {jour.DayOfMonth}";
                pdf.Text(x + 5, y + 4, 9f, PdfWriter.Nettoyer(entete), true);

                float yTexte = y + 17;

                if (!string.IsNullOrEmpty(jour.ShortHolidayName))
                {
                    pdf.Text(x + 5, yTexte, 7.5f,
                             PdfWriter.Tronquer(PdfWriter.Nettoyer(jour.ShortHolidayName), 7.5f, lCell - 10),
                             false, HolidayColors.VersPdf(jour.ShortHolidayName));
                    yTexte += 10;
                }

                foreach (var cours in jour.Courses)
                {
                    if (yTexte > y + hCell - 10) break;
                    var ligne = $"{cours.StartTime:hh\\:mm}-{cours.EndTime:hh\\:mm} {cours.Name}";
                    pdf.Text(x + 5, yTexte, 7.5f,
                             PdfWriter.Tronquer(PdfWriter.Nettoyer(ligne), 7.5f, lCell - 10), false, VertCours);
                    yTexte += 10;
                }

                var notes = jour.Notes.OrderBy(n => n.Hour).ThenBy(n => n.Minute).ToList();
                DessinerNotes(pdf, notes, x + 5, yTexte, lCell - 10, y + hCell - yTexte - 3, 9.5f, complet: detail);
            }

            return pdf.Build();
        }
        public sealed class PeriodeSemaine
        {
            public string Entete = string.Empty;
            public bool   VacancesCompletes;
            public string VacancesLibelle = string.Empty;
            public List<PeriodeJour> Jours = new();
        }

        public sealed class PeriodeJour
        {
            public bool DansPeriode;
            public bool Vacances;
            public string Conge = string.Empty;
            public int  NbNotes;
            public string PremierCours = string.Empty;
        }

        public static byte[] Periode(string titre, IReadOnlyList<PeriodeSemaine> semaines,
                                     IReadOnlyList<string> nomsJours,
                                     string? identite = null, string? anneeScolaire = null)
        {
            var pdf = new PdfWriter(landscape: true);
            Titre(pdf, titre, identite, anneeScolaire);

            if (semaines.Count == 0) return pdf.Build();

            float largeurJours = 42f;
            float largeurTotale = pdf.PageWidth - 2 * Marge - largeurJours;
            float hauteurTotale = pdf.PageHeight - HautGrille - Marge;

            float lCol = largeurTotale / semaines.Count;
            float hEntete = 24f;
            float hLigne = (hauteurTotale - hEntete) / Math.Max(1, nomsJours.Count);

            for (int j = 0; j < nomsJours.Count; j++)
            {
                float y = HautGrille + hEntete + j * hLigne;
                pdf.Rect(Marge, y, largeurJours, hLigne, 0.6f, GrisTrait);
                pdf.Text(Marge + 5, y + hLigne / 2 - 4, 8f, PdfWriter.Nettoyer(nomsJours[j]), true);
            }

            for (int s = 0; s < semaines.Count; s++)
            {
                var semaine = semaines[s];
                float x = Marge + largeurJours + s * lCol;

                pdf.Rect(x, HautGrille, lCol, hEntete, 0.6f, GrisTrait);
                pdf.Text(x + 3, HautGrille + 4, 7f,
                         PdfWriter.Tronquer(PdfWriter.Nettoyer(semaine.Entete), 7f, lCol - 6), true);

                if (semaine.VacancesCompletes)
                {
                    float hTotale = hLigne * nomsJours.Count;
                    pdf.Rect(x, HautGrille + hEntete, lCol, hTotale, 0.6f, GrisTrait);
                    pdf.Text(x + 3, HautGrille + hEntete + hTotale / 2 - 4, 7f,
                             PdfWriter.Tronquer(PdfWriter.Nettoyer(semaine.VacancesLibelle), 7f, lCol - 6),
                             false, HolidayColors.VersPdf(semaine.VacancesLibelle));
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
                        pdf.Text(x + 3, yTexte, 7f, "Conge", false, HolidayColors.VersPdf(jour.Conge));
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

        private static List<int> HeuresOccupees(IEnumerable<Day> jours, int heureDebut, int heureFin)
        {
            var occupees = new SortedSet<int>();

            foreach (var jour in jours)
            {
                foreach (var bloc in NoteLayout.Blocs(jour.Notes, heureDebut, heureFin))
                {
                    for (int h = bloc.Start; h < bloc.End; h++) occupees.Add(h);
                }

                foreach (var cours in jour.Courses)
                {
                    int debut = Math.Max(cours.StartTime.Hours, heureDebut);
                    int fin   = Math.Min(cours.EndTime.Minutes > 0 ? cours.EndTime.Hours + 1 : cours.EndTime.Hours,
                                         heureFin);
                    for (int h = debut; h < fin; h++) occupees.Add(h);
                }
            }

            return occupees.Count > 0
                ? occupees.ToList()
                : Enumerable.Range(heureDebut, Math.Max(1, heureFin - heureDebut)).ToList();
        }

        private static Dictionary<int, int> IndexerLignes(List<int> heures)
        {
            var index = new Dictionary<int, int>();
            for (int i = 0; i < heures.Count; i++) index[heures[i]] = i;
            return index;
        }

        private static void Titre(PdfWriter pdf, string titre,
                                  string? identite = null, string? anneeScolaire = null)
        {
            const float tailleLogo = 20f;
            pdf.Image(Marge, 6f, tailleLogo, tailleLogo,
                      LogoObrigenie.Jpeg, LogoObrigenie.Largeur, LogoObrigenie.Hauteur);

            pdf.Text(Marge + tailleLogo + 5f, 12f, 11f, "Obrigenie", true);

            var texte = PdfWriter.Nettoyer(titre);
            float largeur = PdfWriter.LargeurApprox(texte, 14f);
            pdf.Text(Math.Max(Marge, (pdf.PageWidth - largeur) / 2), 12f, 14f, texte, true);

            if (!string.IsNullOrWhiteSpace(anneeScolaire))
            {
                var annee = PdfWriter.Nettoyer($"Annee scolaire {anneeScolaire}");
                pdf.Text(pdf.PageWidth - Marge - PdfWriter.LargeurApprox(annee, 9f), 15f, 9f,
                         annee, false, GrisTexte);
            }

            if (!string.IsNullOrWhiteSpace(identite))
            {
                var ligne = PdfWriter.Nettoyer(identite);
                pdf.Text(Math.Max(Marge, (pdf.PageWidth - PdfWriter.LargeurApprox(ligne, 9.5f)) / 2),
                         32f, 9.5f, ligne, false, GrisTexte);
            }
        }

        private static void DessinerNotes(PdfWriter pdf, IReadOnlyList<Note> notes,
                                          float x, float y, float largeur, float hauteur,
                                          float taille, bool complet)
        {
            while (taille > 5.5f && HauteurNotes(notes, largeur, taille, complet) > hauteur)
                taille -= 0.5f;

            float yCourant = y;
            float yMax     = y + hauteur;
            float interligne = taille + 1.5f;

            foreach (var note in notes)
            {
                if (yCourant + interligne > yMax) return;

                var plage = NoteLayout.PlageHoraire(note);
                pdf.Text(x, yCourant, taille, plage, true, OrangeNote);
                if (!string.IsNullOrWhiteSpace(note.Titre))
                {
                    float decalage = PdfWriter.LargeurApprox(plage, taille) + 6;
                    pdf.Text(x + decalage, yCourant, taille,
                             PdfWriter.Tronquer(PdfWriter.Nettoyer(note.Titre), taille, largeur - decalage), true);
                }
                yCourant += interligne;
                if (ReportNote.Cible(note.Content) is DateTime cibleReport)
                {
                    if (yCourant + interligne > yMax) return;
                    pdf.Text(x, yCourant, taille - 0.5f,
                             PdfWriter.Nettoyer($"-> Reporte au {cibleReport:dd/MM/yyyy}"), false, OrangeNote);
                    yCourant += interligne;
                }

                var contexte = complet
                    ? PdfWriter.Nettoyer(note.ViseeContexte)
                    : PdfWriter.Nettoyer(NoteLayout.CourseLabel(note));

                foreach (var ligne in PdfWriter.Decouper(contexte, taille - 0.5f, largeur))
                {
                    if (yCourant + interligne > yMax) return;
                    pdf.Text(x, yCourant, taille - 0.5f, ligne, false, VertCours);
                    yCourant += interligne;
                }

                foreach (var ligne in PdfWriter.Decouper(PdfWriter.Nettoyer(ReportNote.Texte(note.Content)), taille - 0.5f, largeur))
                {
                    if (yCourant + interligne > yMax) return;
                    pdf.Text(x, yCourant, taille - 0.5f, ligne);
                    yCourant += interligne;
                }

                yCourant += 3;
            }
        }

        private static float HauteurNotes(IReadOnlyList<Note> notes, float largeur, float taille, bool complet)
        {
            float interligne = taille + 1.5f;
            float total = 0;

            foreach (var note in notes)
            {
                int nbLignes = 1;
                if (ReportNote.Cible(note.Content) != null) nbLignes++;

                var contexte = complet
                    ? PdfWriter.Nettoyer(note.ViseeContexte)
                    : PdfWriter.Nettoyer(NoteLayout.CourseLabel(note));
                nbLignes += PdfWriter.Decouper(contexte, taille - 0.5f, largeur).Count;
                nbLignes += PdfWriter.Decouper(PdfWriter.Nettoyer(ReportNote.Texte(note.Content)), taille - 0.5f, largeur).Count;

                total += nbLignes * interligne + 3;
            }

            return total;
        }

        private static string NomComplet(string nomJour)
            => string.IsNullOrEmpty(nomJour) ? string.Empty
             : char.ToUpperInvariant(nomJour[0]) + nomJour[1..];

        private static string Abreger(string nomJour)
            => string.IsNullOrEmpty(nomJour) ? string.Empty
             : char.ToUpperInvariant(nomJour[0]) + nomJour[1..Math.Min(3, nomJour.Length)];
    }
}
