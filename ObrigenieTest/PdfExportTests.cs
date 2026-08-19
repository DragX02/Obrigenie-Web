using System.Text;
using Obrigenie.Models;
using Obrigenie.Services;

namespace ObrigenieTest;

/// <summary>
/// Tests for the calendar PDF export.
///
/// Two concerns are covered:
///   - <see cref="NoteLayout"/>: how notes are merged into blocks in the hour grid.
///     The same code drives the on-screen grid and the printed page, so a regression
///     here silently changes both.
///   - <see cref="PdfWriter"/> / <see cref="CalendarPdfExporter"/>: the produced bytes
///     must form a structurally valid PDF. The writer builds the cross-reference table
///     by hand, so every declared offset is checked against the actual object position —
///     a wrong offset yields a file that viewers refuse to open.
/// </summary>
public class PdfExportTests
{
    // ── Helpers ─────────────────────────────────────────────────────────────

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

    // ── NoteLayout : fusion des blocs ───────────────────────────────────────

    [Fact]
    public void RowEnd_EndsOnTheHour_StopsAtThatRow()
    {
        // 09:00 → 11:00 occupe les lignes 9 et 10, la ligne 11 reste libre
        Assert.Equal(11, NoteLayout.RowEnd(Note(9, 0, 11, 0)));
    }

    [Fact]
    public void RowEnd_EndsMidHour_CoversTheStartedRow()
    {
        // 09:00 → 11:15 déborde sur la ligne 11, qui est donc occupée entièrement
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
        // Sans fusion, la note de 10:00 tomberait sur une ligne déjà absorbée
        // par la première et ne serait affichée nulle part.
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
        // Note héritée de l'ancienne grille commençant à 06:00
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

    // ── PdfWriter : structure du fichier ────────────────────────────────────

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

        // La colonne des heures doit couvrir toute la grille, et chaque note garder sa plage
        var texte = Encoding.Latin1.GetString(octets);
        Assert.Contains("(08:00) Tj", texte);
        Assert.Contains("(17:00) Tj", texte);
        Assert.Contains("(09:00 -> 11:00) Tj", texte);
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
        // Le PDF écrit ses chaînes en WinAnsi : les émojis n'y ont pas de place et
        // laisseraient un fichier corrompu s'ils étaient copiés tels quels.
        var note = Note(9, 0, 10, 0, "📝 réunion → salle B");
        var texte = Encoding.Latin1.GetString(CalendarPdfExporter.Jour("Test", DayWith(note), 8, 18));

        Assert.Contains("réunion -> salle B", texte);
        Assert.DoesNotContain("\ud83d", texte);
    }

    [Fact]
    public void Grille_LongContent_DoesNotOverflowIntoAnExtraPage()
    {
        // Le texte est rogné à la hauteur de la cellule : une note très longue ne doit
        // pas faire grossir le document indéfiniment.
        var note = Note(9, 0, 10, 0, string.Join(" ", Enumerable.Repeat("mot", 500)));
        var octets = CalendarPdfExporter.Grille("Semaine", new List<Day> { DayWith(note) }, 5);

        AssertPdfValide(octets);
        Assert.Equal(1, CompterPages(Encoding.Latin1.GetString(octets)));
    }

    // ── Vérifications communes ──────────────────────────────────────────────

    /// <summary>
    /// Vérifie l'en-tête, la présence de la table xref et surtout que chaque offset
    /// annoncé pointe bien sur le début de l'objet correspondant.
    /// </summary>
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

        // La table xref : "xref\n0 N\n" suivi d'une entrée par objet.
        // On cherche "\nxref\n" et non "xref\n", sinon le "startxref" du trailer matche aussi.
        int posXref = texte.LastIndexOf("\nxref\n", StringComparison.Ordinal);
        Assert.True(posXref > 0, "Table xref absente.");

        var lignes = texte[(posXref + 1)..].Split('\n');
        int nbObjets = int.Parse(lignes[1].Split(' ')[1]) - 1;

        for (int id = 1; id <= nbObjets; id++)
        {
            // Entrées : ligne 0 = "xref", ligne 1 = en-tête, ligne 2 = objet libre 0
            long offset = long.Parse(lignes[2 + id][..10]);
            Assert.True(offset > 0 && offset < octets.Length, $"Offset hors fichier pour l'objet {id}.");
            Assert.StartsWith($"{id} 0 obj", texte[(int)offset..]);
        }
    }

    /// <summary>Nombre de pages déclarées dans l'arbre de pages du document.</summary>
    private static int CompterPages(string texte)
    {
        int pos = texte.IndexOf("/Count ", StringComparison.Ordinal);
        Assert.True(pos > 0, "Nombre de pages absent.");

        var chiffres = new string(texte[(pos + 7)..].TakeWhile(char.IsDigit).ToArray());
        return int.Parse(chiffres);
    }
}
