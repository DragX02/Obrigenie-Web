using System.Text;
using Obrigenie.Models;
using Obrigenie.Services;

namespace ObrigenieTest;

public class PdfExportTests
{
    private static Note Note(int hour, int minute, int endHour, int endMinute, string content = "Test")
        => new()
        {
            Hour = hour, Minute = minute, EndHour = endHour, EndMinute = endMinute,
            Content = content, Date = new DateTime(2026, 8, 17),
        };

    private static Day DayWith(params Note[] notes)
        => new()
        {
            Date = new DateTime(2026, 8, 17),
            DayOfWeek = "lundi",
            DayOfMonth = "17",
            Notes = notes.ToList(),
        };

    [Fact]
    public void RowEnd_EndsOnTheHour_StopsAtThatRow()
    {
        Assert.Equal(11, NoteLayout.RowEnd(Note(9, 0, 11, 0)));
    }

    [Fact]
    public void RowEnd_EndsMidHour_CoversTheStartedRow()
    {
        Assert.Equal(12, NoteLayout.RowEnd(Note(9, 0, 11, 15)));
    }

    [Fact]
    public void RowEnd_MissingEndHour_FallsBackToOneHour()
    {
        Assert.Equal(10, NoteLayout.RowEnd(Note(9, 0, 0, 0)));
    }

    [Fact]
    public void Blocs_SpanningNote_ProducesASingleBlock()
    {
        var blocs = NoteLayout.Blocs(new[] { Note(9, 0, 11, 0) }, 8, 18);

        var bloc = Assert.Single(blocs);
        Assert.Equal(9, bloc.Start);
        Assert.Equal(11, bloc.End);
    }

    [Fact]
    public void Blocs_OverlappingNotes_ShareTheSameBlock()
    {
        var blocs = NoteLayout.Blocs(new[] { Note(9, 0, 11, 0), Note(10, 0, 12, 0) }, 8, 18);

        var bloc = Assert.Single(blocs);
        Assert.Equal(9, bloc.Start);
        Assert.Equal(12, bloc.End);
        Assert.Equal(2, bloc.Notes.Count);
    }

    [Fact]
    public void Blocs_SeparateNotes_StayInDistinctBlocks()
    {
        var blocs = NoteLayout.Blocs(new[] { Note(9, 0, 10, 0), Note(14, 0, 15, 0) }, 8, 18);

        Assert.Equal(2, blocs.Count);
    }

    [Fact]
    public void Blocs_NoteBeforeGrid_IsClippedToTheFirstRow()
    {
        var blocs = NoteLayout.Blocs(new[] { Note(6, 0, 9, 0) }, 8, 18);

        var bloc = Assert.Single(blocs);
        Assert.Equal(8, bloc.Start);
        Assert.Equal(9, bloc.End);
    }

    [Fact]
    public void Blocs_NoteFullyOutsideGrid_IsIgnored()
    {
        Assert.Empty(NoteLayout.Blocs(new[] { Note(6, 0, 7, 0) }, 8, 18));
    }

    [Fact]
    public void CourseLabel_ReadsTheCoursLineOfTheCascade()
    {
        var note = Note(9, 0, 10, 0);
        note.ViseeContexte = "Année : 6ème primaire\nCours : Langues modernes\nVisée : Le lien social";

        Assert.Equal("Langues modernes", NoteLayout.CourseLabel(note));
    }

    [Fact]
    public void CourseLabel_WithoutCascade_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, NoteLayout.CourseLabel(Note(9, 0, 10, 0)));
    }

    [Fact]
    public void PlageHoraire_FormatsBothEnds()
    {
        Assert.Equal("09:05 -> 11:30", NoteLayout.PlageHoraire(Note(9, 5, 11, 30)));
    }

    [Fact]
    public void Jour_ProducesAStructurallyValidPdf()
    {
        var octets = CalendarPdfExporter.Jour("lundi 17 août 2026", DayWith(Note(9, 0, 11, 0)), 8, 18);

        AssertPdfValide(octets);
    }

    [Fact]
    public void Grille_ProducesAStructurallyValidPdf()
    {
        var jours = new List<Day> { DayWith(Note(9, 0, 11, 0)), DayWith(), DayWith(), DayWith(), DayWith() };

        AssertPdfValide(CalendarPdfExporter.Grille("Semaine 17/08 - 21/08", jours, 5));
    }

    [Fact]
    public void Semaine_DrawsTheHourColumnAndTheNoteRanges()
    {
        var jours = new List<Day> { DayWith(Note(9, 0, 11, 0)), DayWith(), DayWith(), DayWith(), DayWith() };
        var octets = CalendarPdfExporter.Semaine("Semaine 17/08 - 21/08", jours, 8, 18);

        AssertPdfValide(octets);

        var texte = Encoding.Latin1.GetString(octets);
        Assert.Contains("(09:00) Tj", texte);
        Assert.Contains("(10:00) Tj", texte);
        Assert.Contains("(09:00 -> 11:00) Tj", texte);
    }

    [Fact]
    public void Semaine_ToutLeTexteDeLaGrille_EstEnTaille10()
    {
        var note = Note(9, 0, 10, 0, "Contenu de la note");
        note.Titre = "Dictée";
        var jours = new List<Day> { DayWith(note), DayWith() };

        var texte = Encoding.Latin1.GetString(CalendarPdfExporter.Semaine("Semaine", jours, 8, 18));

        Assert.Matches(@"/F2 10 Tf [^\n]*\(09:00 -> 10:00\) Tj", texte);
        Assert.Matches(@"/F1 10 Tf [^\n]*\(Contenu de la note\) Tj", texte);
        Assert.Matches(@"/F1 10 Tf [^\n]*\(09:00\) Tj", texte);
        Assert.Matches(@"/F2 10 Tf [^\n]*\(Lundi 17\) Tj", texte);
    }

    [Fact]
    public void Semaine_TropDeContenu_PasseSurUnePageSuivante()
    {
        var longue = string.Join("\n", Enumerable.Range(1, 12).Select(i => $"Ligne {i}"));
        var notes = Enumerable.Range(8, 6).Select(h => Note(h, 0, h + 1, 0, longue)).ToArray();

        var octets = CalendarPdfExporter.Semaine("Semaine", new List<Day> { DayWith(notes) }, 8, 18);

        AssertPdfValide(octets);
        Assert.DoesNotContain("/Count 1 ", Encoding.Latin1.GetString(octets));
    }

    [Fact]
    public void Semaine_AvecIdentite_EcritLEnTeteDuDocument()
    {
        var octets = CalendarPdfExporter.Semaine("Semaine 24/08 - 28/08",
                                                 new List<Day> { DayWith() }, 8, 18,
                                                 "Jean Dupont - instituteur", "2026-2027");

        AssertPdfValide(octets);

        var texte = Encoding.Latin1.GetString(octets);
        Assert.Contains("(Obrigenie) Tj", texte);
        Assert.Contains("(Jean Dupont - instituteur) Tj", texte);
        Assert.Contains("(Annee scolaire 2026-2027) Tj", texte);
    }

    [Fact]
    public void Semaine_SansIdentite_NEcritPasDeLigneVide()
    {
        var texte = Encoding.Latin1.GetString(
            CalendarPdfExporter.Semaine("Semaine", new List<Day> { DayWith() }, 8, 18));

        Assert.Contains("(Obrigenie) Tj", texte);
        Assert.DoesNotContain("Annee scolaire", texte);
    }

    [Fact]
    public void Jour_HeuresSansNote_NeSontPasImprimees()
    {
        var texte = Encoding.Latin1.GetString(
            CalendarPdfExporter.Jour("lundi", DayWith(Note(9, 0, 11, 0)), 8, 18));

        Assert.Contains("(09:00) Tj", texte);
        Assert.Contains("(10:00) Tj", texte);
        Assert.DoesNotContain("(08:00) Tj", texte);
        Assert.DoesNotContain("(14:00) Tj", texte);
    }

    [Fact]
    public void Jour_SansAucuneNote_ConserveLaGrilleComplete()
    {
        var texte = Encoding.Latin1.GetString(CalendarPdfExporter.Jour("mardi", DayWith(), 8, 18));

        Assert.Contains("(08:00) Tj", texte);
        Assert.Contains("(17:00) Tj", texte);
    }

    [Fact]
    public void Semaine_HeureVideChezTousLesJours_EstRetiree()
    {
        var jours = new List<Day>
        {
            DayWith(Note(9, 0, 10, 0)),
            DayWith(Note(14, 0, 15, 0)),
            DayWith(),
        };

        var texte = Encoding.Latin1.GetString(
            CalendarPdfExporter.Semaine("Semaine", jours, 8, 18));

        Assert.Contains("(09:00) Tj", texte);
        Assert.Contains("(14:00) Tj", texte);
        Assert.DoesNotContain("(11:00) Tj", texte);
        Assert.DoesNotContain("(17:00) Tj", texte);
    }

    [Fact]
    public void Semaine_NoteFusionnee_ResteDUnSeulTenant()
    {
        var jours = new List<Day> { DayWith(Note(9, 0, 12, 0)) };
        var octets = CalendarPdfExporter.Semaine("Semaine", jours, 8, 18);

        AssertPdfValide(octets);

        var texte = Encoding.Latin1.GetString(octets);
        Assert.Contains("(09:00) Tj", texte);
        Assert.Contains("(10:00) Tj", texte);
        Assert.Contains("(11:00) Tj", texte);
    }

    [Fact]
    public void EnTete_ContientLeLogoEnImageJpeg()
    {
        var octets = CalendarPdfExporter.Jour("lundi", DayWith(Note(9, 0, 10, 0)), 8, 18);

        AssertPdfValide(octets);

        var texte = Encoding.Latin1.GetString(octets);
        Assert.Contains("/Subtype /Image", texte);
        Assert.Contains("/Filter /DCTDecode", texte);
        Assert.Contains("/XObject << /Im1", texte);
        Assert.Contains("/Im1 Do", texte);
    }

    [Fact]
    public void Logo_EstUnJpegValide()
    {
        var jpeg = LogoObrigenie.Jpeg;

        Assert.True(jpeg.Length > 500);
        Assert.Equal(0xFF, jpeg[0]);
        Assert.Equal(0xD8, jpeg[1]);
        Assert.Equal(0xFF, jpeg[^2]);
        Assert.Equal(0xD9, jpeg[^1]);
    }

    [Fact]
    public void Periode_ProducesAStructurallyValidPdf()
    {
        var semaines = new List<CalendarPdfExporter.PeriodeSemaine>
        {
            new()
            {
                Entete = "S1 17/08 - 21/08",
                Jours = Enumerable.Range(0, 5)
                    .Select(_ => new CalendarPdfExporter.PeriodeJour { DansPeriode = true, NbNotes = 2 })
                    .ToList(),
            },
            new() { Entete = "S2 24/08 - 28/08", VacancesCompletes = true, VacancesLibelle = "Toussaint" },
        };

        AssertPdfValide(CalendarPdfExporter.Periode("Trimestre 1", semaines,
                                                    new[] { "Lun", "Mar", "Mer", "Jeu", "Ven" }));
    }

    [Fact]
    public void Jour_EmptyDay_StillProducesAValidPdf()
    {
        AssertPdfValide(CalendarPdfExporter.Jour("mardi 18 août 2026", DayWith(), 8, 18));
    }

    [Fact]
    public void Jour_EmojiAndArrows_AreStrippedFromTheContentStream()
    {
        var note = Note(9, 0, 10, 0, "📝 réunion → salle B");
        var texte = Encoding.Latin1.GetString(CalendarPdfExporter.Jour("Test", DayWith(note), 8, 18));

        Assert.Contains("réunion -> salle B", texte);
        Assert.DoesNotContain("\ud83d", texte);
    }

    [Fact]
    public void Grille_LongContent_DoesNotOverflowIntoAnExtraPage()
    {
        var note = Note(9, 0, 10, 0, string.Join(" ", Enumerable.Repeat("mot", 500)));
        var octets = CalendarPdfExporter.Grille("Semaine", new List<Day> { DayWith(note) }, 5);

        AssertPdfValide(octets);
        Assert.Equal(1, CompterPages(Encoding.Latin1.GetString(octets)));
    }

    private static void AssertPdfValide(byte[] octets)
    {
        Assert.NotNull(octets);
        Assert.True(octets.Length > 400, "Le PDF produit est anormalement petit.");

        var texte = Encoding.Latin1.GetString(octets);

        Assert.StartsWith("%PDF-1.4", texte);
        Assert.EndsWith("%%EOF\n", texte);
        Assert.Contains("/Type /Catalog", texte);
        Assert.Contains("/Type /Pages", texte);
        Assert.Contains("/BaseFont /Helvetica", texte);

        int posXref = texte.LastIndexOf("\nxref\n", StringComparison.Ordinal);
        Assert.True(posXref > 0, "Table xref absente.");

        var lignes = texte[(posXref + 1)..].Split('\n');
        int nbObjets = int.Parse(lignes[1].Split(' ')[1]) - 1;

        for (int id = 1; id <= nbObjets; id++)
        {
            long offset = long.Parse(lignes[2 + id][..10]);
            Assert.True(offset > 0 && offset < octets.Length, $"Offset hors fichier pour l'objet {id}.");
            Assert.StartsWith($"{id} 0 obj", texte[(int)offset..]);
        }
    }

    private static int CompterPages(string texte)
    {
        int pos = texte.IndexOf("/Count ", StringComparison.Ordinal);
        Assert.True(pos > 0, "Nombre de pages absent.");

        var chiffres = new string(texte[(pos + 7)..].TakeWhile(char.IsDigit).ToArray());
        return int.Parse(chiffres);
    }
}
