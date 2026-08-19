using Obrigenie.Models;
using Obrigenie.Services;

namespace ObrigenieTest;

/// <summary>
/// Tests for <see cref="CalendarService.AppliquerCorrections"/>.
///
/// The official school calendar is shared by every user and filled automatically,
/// so some of its dates are wrong. Each user stores personal corrections that are
/// merged on top of it at display time: a corrected period replaces the official
/// one, a hidden one disappears, and a user-created one is appended.
///
/// These rules decide what every calendar view shows as a holiday, so they are
/// covered on their own rather than through the page that consumes them.
/// </summary>
public class UserCongeTests
{
    private static SchoolYearCalendar Officiel(params Holiday[] conges)
        => new()
        {
            SchoolYearStart = new DateTime(2026, 8, 24),
            Holidays = conges.ToList(),
        };

    private static Holiday Conge(int id, string nom, DateTime debut, DateTime fin)
        => new() { Id = id, Name = nom, StartDate = debut, EndDate = fin };

    // ── Aucun changement ────────────────────────────────────────────────────

    [Fact]
    public void SansCorrection_LeCalendrierOfficielEstRenduTelQuel()
    {
        var calendrier = Officiel(Conge(1, "Toussaint", new DateTime(2026, 10, 26), new DateTime(2026, 11, 6)));

        var resultat = CalendarService.AppliquerCorrections(calendrier, new List<UserConge>());

        Assert.Same(calendrier, resultat);
    }

    [Fact]
    public void CorrectionSansCongeOfficielCorrespondant_EstIgnoree()
    {
        // Le calendrier officiel peut être reconstruit côté serveur : une correction
        // orpheline ne doit pas réapparaître comme un congé fantôme.
        var calendrier = Officiel(Conge(1, "Toussaint", new DateTime(2026, 10, 26), new DateTime(2026, 11, 6)));
        var corrections = new List<UserConge>
        {
            new() { Id = 9, IdCalendrierFk = 999, Nom = "Orphelin",
                    DateDebut = new DateTime(2026, 12, 1), DateFin = new DateTime(2026, 12, 2) },
        };

        var resultat = CalendarService.AppliquerCorrections(calendrier, corrections);

        var conge = Assert.Single(resultat.Holidays);
        Assert.Equal("Toussaint", conge.Name);
    }

    // ── Correction des dates ────────────────────────────────────────────────

    [Fact]
    public void CongeCorrige_PrendLesDatesEtLeNomDeLUtilisateur()
    {
        var calendrier = Officiel(Conge(1, "Toussaint", new DateTime(2026, 10, 26), new DateTime(2026, 11, 6)));
        var corrections = new List<UserConge>
        {
            new() { Id = 5, IdCalendrierFk = 1, Nom = "Conge d'automne",
                    DateDebut = new DateTime(2026, 10, 19), DateFin = new DateTime(2026, 10, 30) },
        };

        var conge = Assert.Single(CalendarService.AppliquerCorrections(calendrier, corrections).Holidays);

        Assert.Equal("Conge d'automne", conge.Name);
        Assert.Equal(new DateTime(2026, 10, 19), conge.StartDate);
        Assert.Equal(new DateTime(2026, 10, 30), conge.EndDate);
        // L'identifiant officiel est conservé pour que la page Congés retrouve sa ligne
        Assert.Equal(1, conge.Id);
    }

    [Fact]
    public void CongeCorrige_ChangeLesJoursConsideresCommeConge()
    {
        var calendrier = Officiel(Conge(1, "Toussaint", new DateTime(2026, 10, 26), new DateTime(2026, 11, 6)));
        var corrections = new List<UserConge>
        {
            new() { Id = 5, IdCalendrierFk = 1, Nom = "Toussaint",
                    DateDebut = new DateTime(2026, 10, 19), DateFin = new DateTime(2026, 10, 30) },
        };

        var conge = Assert.Single(CalendarService.AppliquerCorrections(calendrier, corrections).Holidays);

        Assert.True(conge.IsDateInHoliday(new DateTime(2026, 10, 20)));   // ajouté par la correction
        Assert.False(conge.IsDateInHoliday(new DateTime(2026, 11, 3)));   // retiré par la correction
    }

    // ── Masquage ────────────────────────────────────────────────────────────

    [Fact]
    public void CongeMasque_DisparaitDuCalendrier()
    {
        var calendrier = Officiel(
            Conge(1, "Toussaint", new DateTime(2026, 10, 26), new DateTime(2026, 11, 6)),
            Conge(2, "Noel", new DateTime(2026, 12, 21), new DateTime(2027, 1, 1)));

        var corrections = new List<UserConge> { new() { Id = 5, IdCalendrierFk = 1, Nom = "Toussaint", Masque = true } };

        var resultat = CalendarService.AppliquerCorrections(calendrier, corrections);

        var conge = Assert.Single(resultat.Holidays);
        Assert.Equal("Noel", conge.Name);
    }

    [Theory]
    [InlineData("Rentree scolaire", true)]
    [InlineData("Rentrée scolaire", true)]   // la base contient la forme accentuée
    [InlineData("RENTRÉE SCOLAIRE", true)]
    [InlineData("Conge d'automne (Toussaint)", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void EstRentree_IgnoreLesAccentsEtLaCasse(string? nom, bool attendu)
    {
        Assert.Equal(attendu, CalendarService.EstRentree(nom));
    }

    [Fact]
    public void MarqueurDeRentreeAccentue_NEstPasMasque()
    {
        // Bug constaté en production : le test portait sur "Rentree" sans accent,
        // laissant passer "Rentrée scolaire" tel qu'il est stocké en base.
        var calendrier = Officiel(Conge(1, "Rentrée scolaire", new DateTime(2026, 8, 24), new DateTime(2026, 8, 24)));
        var corrections = new List<UserConge> { new() { Id = 5, IdCalendrierFk = 1, Nom = "x", Masque = true } };

        Assert.Single(CalendarService.AppliquerCorrections(calendrier, corrections).Holidays);
    }

    [Fact]
    public void MarqueurDeRentree_NEstJamaisMasque()
    {
        // La Rentrée ancre la numérotation des semaines scolaires : la masquer
        // décalerait toutes les étiquettes de période.
        var calendrier = Officiel(Conge(1, "Rentree scolaire", new DateTime(2026, 8, 24), new DateTime(2026, 8, 24)));
        var corrections = new List<UserConge> { new() { Id = 5, IdCalendrierFk = 1, Nom = "Rentree", Masque = true } };

        Assert.Single(CalendarService.AppliquerCorrections(calendrier, corrections).Holidays);
    }

    // ── Ajout ───────────────────────────────────────────────────────────────

    [Fact]
    public void CongeAjoute_ApparaitDansLeCalendrier()
    {
        var calendrier = Officiel(Conge(1, "Toussaint", new DateTime(2026, 10, 26), new DateTime(2026, 11, 6)));
        var corrections = new List<UserConge>
        {
            new() { Id = 7, IdCalendrierFk = null, Nom = "Journee pedagogique",
                    DateDebut = new DateTime(2026, 9, 30), DateFin = new DateTime(2026, 9, 30) },
        };

        var resultat = CalendarService.AppliquerCorrections(calendrier, corrections);

        Assert.Equal(2, resultat.Holidays.Count);
        Assert.Contains(resultat.Holidays, h => h.Name == "Journee pedagogique");
    }

    [Fact]
    public void CongesResultants_SontTriesParDateDeDebut()
    {
        var calendrier = Officiel(Conge(1, "Noel", new DateTime(2026, 12, 21), new DateTime(2027, 1, 1)));
        var corrections = new List<UserConge>
        {
            new() { Id = 7, Nom = "Journee pedagogique",
                    DateDebut = new DateTime(2026, 9, 30), DateFin = new DateTime(2026, 9, 30) },
        };

        var resultat = CalendarService.AppliquerCorrections(calendrier, corrections);

        Assert.Equal("Journee pedagogique", resultat.Holidays[0].Name);
        Assert.Equal("Noel", resultat.Holidays[1].Name);
    }

    [Fact]
    public void AjoutMasque_NEstPasAffiche()
    {
        var calendrier = Officiel(Conge(1, "Noel", new DateTime(2026, 12, 21), new DateTime(2027, 1, 1)));
        var corrections = new List<UserConge>
        {
            new() { Id = 7, Nom = "Brouillon", Masque = true,
                    DateDebut = new DateTime(2026, 9, 30), DateFin = new DateTime(2026, 9, 30) },
        };

        Assert.Single(CalendarService.AppliquerCorrections(calendrier, corrections).Holidays);
    }

    [Fact]
    public void DateDeRentree_EstConservee()
    {
        var calendrier = Officiel(Conge(1, "Toussaint", new DateTime(2026, 10, 26), new DateTime(2026, 11, 6)));
        var corrections = new List<UserConge> { new() { Id = 5, IdCalendrierFk = 1, Nom = "x", Masque = true } };

        Assert.Equal(calendrier.SchoolYearStart,
                     CalendarService.AppliquerCorrections(calendrier, corrections).SchoolYearStart);
    }
}
