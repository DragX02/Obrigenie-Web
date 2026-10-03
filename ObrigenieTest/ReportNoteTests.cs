using Obrigenie.Models;
using Obrigenie.Services;

namespace ObrigenieTest;

public class ReportNoteTests
{
    private static readonly DateTime Cible = new(2025, 10, 6);

    private const string Marqueur = "↪ Reporté au 06/10/2025";

    [Fact]
    public void Lire_SansMarqueur_RendLeTexteIntact()
    {
        var (texte, cible) = ReportNote.Lire("Lecture suivie chapitre 3");

        Assert.Equal("Lecture suivie chapitre 3", texte);
        Assert.Null(cible);
    }

    [Fact]
    public void Lire_ContenuVide_NeCasseRien()
    {
        Assert.Equal((string.Empty, null), ReportNote.Lire(null));
        Assert.Equal((string.Empty, null), ReportNote.Lire(""));
    }

    [Fact]
    public void Lire_AvecMarqueur_SepareLeTexteEtLaDate()
    {
        var (texte, cible) = ReportNote.Lire($"Lecture suivie chapitre 3\n{Marqueur}");

        Assert.Equal("Lecture suivie chapitre 3", texte);
        Assert.Equal(Cible, cible);
    }

    [Fact]
    public void Lire_MarqueurSeul_NeRendAucunTexte()
    {
        Assert.Equal(Marqueur, ReportNote.Libelle(Cible));
        Assert.Equal(Cible, ReportNote.Cible(Marqueur));
        Assert.Equal(string.Empty, ReportNote.Texte(Marqueur));
    }

    [Fact]
    public void Lire_MarqueurSansAccent_EstQuandMemeReconnu()
    {
        var (texte, cible) = ReportNote.Lire("Dictée\n↪ Reporte au 06/10/2025");

        Assert.Equal("Dictée", texte);
        Assert.Equal(Cible, cible);
    }

    [Fact]
    public void Lire_LigneRessemblanteMaisDateIllisible_ResteDuTexte()
    {
        var contenu = "Reporté au prochain cours de gym";
        var (texte, cible) = ReportNote.Lire(contenu);

        Assert.Equal(contenu, texte);
        Assert.Null(cible);
    }

    [Fact]
    public void Copier_ReprendHoraireEtViseeSurLaNouvelleDate()
    {
        var source = new Note
        {
            Id            = 42,
            Date          = new DateTime(2025, 9, 29),
            Hour          = 10, Minute = 15,
            EndHour       = 11, EndMinute = 45,
            Content       = $"Dictée préparée\n{Marqueur}",
            IdViseeFk     = 7,
            ViseeContexte = "Cours : Français",
        };

        var copie = ReportNote.Copier(source, Cible);

        Assert.Equal(0, copie.Id);
        Assert.Equal(Cible.Date, copie.Date.Date);
        Assert.Equal(DateTimeKind.Utc, copie.Date.Kind);

        Assert.Equal(10, copie.Hour);
        Assert.Equal(15, copie.Minute);
        Assert.Equal(11, copie.EndHour);
        Assert.Equal(45, copie.EndMinute);
        Assert.Equal(7, copie.IdViseeFk);
        Assert.Equal("Cours : Français", copie.ViseeContexte);

        Assert.Equal("Dictée préparée", copie.Content);
        Assert.Null(ReportNote.Cible(copie.Content));
    }

    [Fact]
    public void Copier_NeModifiePasLaNoteDOrigine()
    {
        var source = new Note { Id = 42, Date = new DateTime(2025, 9, 29), Hour = 9, Content = "Calcul mental" };

        ReportNote.Copier(source, Cible);

        Assert.Equal(42, source.Id);
        Assert.Equal(new DateTime(2025, 9, 29), source.Date);
        Assert.Equal("Calcul mental", source.Content);
    }

    [Fact]
    public void DateCopie_PorteeJournee_MeneDroitALaDateChoisie()
    {
        var note = new Note { Date = new DateTime(2025, 9, 29) };

        var arrivee = ReportNote.DateCopie(note, new DateTime(2025, 9, 29), new DateTime(2025, 10, 9));

        Assert.Equal(new DateTime(2025, 10, 9), arrivee);
    }

    [Theory]
    [InlineData("2025-09-29", "2025-10-20")]
    [InlineData("2025-10-01", "2025-10-22")]
    [InlineData("2025-10-03", "2025-10-24")]
    public void DateCopie_PorteeSemaine_ConserveLeJourDeSemaine(string depart, string arrivee)
    {
        var lundiSource = new DateTime(2025, 9, 29);
        var lundiCible  = new DateTime(2025, 10, 20);
        var note = new Note { Date = DateTime.Parse(depart) };

        var obtenue = ReportNote.DateCopie(note, lundiSource, lundiCible);

        Assert.Equal(DateTime.Parse(arrivee), obtenue);
        Assert.Equal(note.Date.DayOfWeek, obtenue.DayOfWeek);
    }

    [Fact]
    public void DateCopie_IgnoreLHeureDesDates()
    {
        var note = new Note { Date = new DateTime(2025, 9, 29, 23, 30, 0) };

        var arrivee = ReportNote.DateCopie(note,
            new DateTime(2025, 9, 29, 22, 0, 0),
            new DateTime(2025, 10, 6, 1, 0, 0));

        Assert.Equal(new DateTime(2025, 10, 6), arrivee);
    }

    [Fact]
    public void Infobulle_SuitLaPresenceDuMarqueur()
    {
        var reportee = new Note { Content = $"Dictée\n{Marqueur}" };
        var ordinaire = new Note { Content = "Dictée" };

        Assert.Equal("Reporté au 06/10/2025", ReportNote.Infobulle(reportee));
        Assert.Null(ReportNote.Infobulle(ordinaire));
    }
}
