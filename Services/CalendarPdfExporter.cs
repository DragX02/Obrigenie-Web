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

        private const float TailleSemaine     = 8f;
        private const float InterligneSemaine = TailleSemaine + 2f;
        private const float RetraitCadre      = 2f;
        private const float MargeCadre        = 4f;
        private const float EcartCadres       = 0f;
        private const string OrangeCadre      = "0.85 0.55 0.1";

        private readonly record struct LigneCadre(string Texte, bool Gras, string Couleur);

        private sealed record CadreSemaine(TimeSpan Debut, bool EstNote, List<LigneCadre> Lignes);

        private readonly record struct Placement(int Page, int Jour, float Y, float Hauteur, CadreSemaine Cadre);

        public static byte[] Semaine(string titre, IReadOnlyList<Day> jours, int heureDebut, int heureFin,
                                     string? identite = null, string? anneeScolaire = null)
        {
            var pdf = new PdfWriter(landscape: true);
            Titre(pdf, titre, identite, anneeScolaire);

            if (jours.Count == 0) return pdf.Build();

            const float t = TailleSemaine;
            float hEntete      = 18f;
            float lCol         = (pdf.PageWidth - 2 * Marge) / jours.Count;
            float largeurTexte = lCol - 2 * (RetraitCadre + MargeCadre);
            float yHaut        = HautGrille + hEntete;
            float hDispo       = pdf.PageHeight - Marge - yHaut;

            var placements = new List<Placement>();
            for (int d = 0; d < jours.Count; d++)
            {
                int page = 0;
                float y = RetraitCadre;

                foreach (var cadre in CadresDuJour(jours[d], heureDebut, heureFin, largeurTexte))
                {
                    var reste = new List<LigneCadre>(cadre.Lignes);
                    bool suite = false;

                    while (reste.Count > 0)
                    {
                        var entete = suite
                            ? new List<LigneCadre> { new($"{cadre.Lignes[0].Texte} (suite)", true, cadre.Lignes[0].Couleur) }
                            : new List<LigneCadre>();

                        float libre = hDispo - RetraitCadre - y;
                        int capacite = (int)Math.Floor((libre - 2 * MargeCadre + 0.01f) / InterligneSemaine) - entete.Count;

                        if (capacite < Math.Min(2, reste.Count) && y > RetraitCadre)
                        {
                            page++;
                            y = RetraitCadre;
                            continue;
                        }

                        int n = Math.Clamp(capacite, 1, reste.Count);
                        var morceau = entete.Concat(reste.Take(n)).ToList();
                        reste.RemoveRange(0, n);

                        float h = HauteurCadre(morceau);
                        placements.Add(new Placement(page, d, y, h, cadre with { Lignes = morceau }));
                        y += h + EcartCadres;
                        suite = true;
                    }
                }
            }

            int nbPages = placements.Count == 0 ? 1 : placements.Max(p => p.Page) + 1;

            for (int p = 0; p < nbPages; p++)
            {
                if (p > 0)
                {
                    pdf.NewPage();
                    Titre(pdf, titre, identite, anneeScolaire);
                }

                float hauteur = hEntete + hDispo;
                pdf.Rect(Marge, HautGrille, pdf.PageWidth - 2 * Marge, hauteur, 0.8f, GrisTrait);
                pdf.Line(Marge, yHaut, pdf.PageWidth - Marge, yHaut, 0.8f, GrisTrait);

                for (int d = 0; d < jours.Count; d++)
                {
                    var jour = jours[d];
                    float x = Marge + d * lCol;

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

                foreach (var pl in placements.Where(pl => pl.Page == p))
                {
                    float xc = Marge + pl.Jour * lCol + RetraitCadre;
                    float yc = yHaut + pl.Y;
                    float lc = lCol - 2 * RetraitCadre;

                    if (!pl.Cadre.EstNote) pdf.FillRect(xc, yc, lc, pl.Hauteur, "0.93 0.93 0.93");
                    pdf.Rect(xc, yc, lc, pl.Hauteur, pl.Cadre.EstNote ? 0.8f : 0.4f,
                             pl.Cadre.EstNote ? OrangeCadre : GrisTrait);

                    float yTexte = yc + MargeCadre;
                    float yMax   = yc + pl.Hauteur - MargeCadre;
                    foreach (var ligne in pl.Cadre.Lignes)
                    {
                        if (yTexte + InterligneSemaine > yMax + 2f) break;
                        pdf.Text(xc + MargeCadre, yTexte, t, ligne.Texte, ligne.Gras, ligne.Couleur);
                        yTexte += InterligneSemaine;
                    }
                }
            }

            return pdf.Build();
        }

        private static List<CadreSemaine> CadresDuJour(Day jour, int heureDebut, int heureFin, float largeur)
        {
            const float t = TailleSemaine;
            var cadres = new List<CadreSemaine>();

            foreach (var cours in jour.Courses)
            {
                int fin = cours.EndTime.Minutes > 0 ? cours.EndTime.Hours + 1 : cours.EndTime.Hours;
                if (cours.StartTime.Hours >= heureFin || fin <= heureDebut) continue;

                var libelle = PdfWriter.Nettoyer($"{cours.StartTime:hh\\:mm}-{cours.EndTime:hh\\:mm} {cours.Name}");
                cadres.Add(new CadreSemaine(cours.StartTime, false,
                    new List<LigneCadre> { new(PdfWriter.Tronquer(libelle, t, largeur), true, VertCours) }));
            }

            foreach (var note in jour.Notes)
            {
                if (note.Hour >= heureFin || NoteLayout.RowEnd(note) <= heureDebut) continue;

                var lignes = new List<LigneCadre> { new(NoteLayout.PlageHoraire(note), true, OrangeNote) };

                foreach (var l in PdfWriter.Decouper(PdfWriter.Nettoyer(note.Titre), t, largeur))
                    lignes.Add(new LigneCadre(l, true, "0 0 0"));

                if (ReportNote.Cible(note.Content) is DateTime cible)
                    lignes.Add(new LigneCadre(PdfWriter.Nettoyer($"-> Reporte au {cible:dd/MM/yyyy}"), false, OrangeNote));

                foreach (var l in PdfWriter.Decouper(ReduireRetrait(PdfWriter.Nettoyer(note.ViseeContexte)), t, largeur))
                    lignes.Add(new LigneCadre(l, false, VertCours));

                foreach (var l in PdfWriter.Decouper(ReduireRetrait(PdfWriter.Nettoyer(ReportNote.Texte(note.Content))), t, largeur))
                    lignes.Add(new LigneCadre(l, false, "0 0 0"));

                cadres.Add(new CadreSemaine(new TimeSpan(note.Hour, note.Minute, 0), true, lignes));
            }

            return cadres.OrderBy(c => c.Debut).ThenBy(c => c.EstNote).ToList();
        }

        private static string ReduireRetrait(string texte)
            => string.Join("\n", texte.Split('\n').Select(l =>
            {
                var contenu = l.TrimStart();
                return contenu.Length < l.Length ? "  " + contenu : contenu;
            }));

        private static float HauteurCadre(IEnumerable<LigneCadre> lignes)
            => 2 * MargeCadre + lignes.Count() * InterligneSemaine;

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
