using Obrigenie.Models;
using Obrigenie.Services;

namespace ObrigenieTest;

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

    [Fact]
    public void SansCorrection_LeCalendrierOfficielEstRenduTelQuel()
    {
        var calendrier = Officiel(Conge(1, "Toussaint", new DateTime(2026, 10, 26), new DateTime(2026, 11, 6)));

        var resultat = CalendarService.AppliquerCorrections(calendrier, new List<UserConge>());

        var conge = Assert.Single(resultat.Holidays);
        Assert.Equal("Toussaint", conge.Name);
        Assert.Equal(new DateTime(2026, 10, 26), conge.StartDate);
        Assert.Equal(calendrier.SchoolYearStart, resultat.SchoolYearStart);
    }

    [Fact]
    public void CorrectionSansCongeOfficielCorrespondant_EstIgnoree()
    {
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

        Assert.True(conge.IsDateInHoliday(new DateTime(2026, 10, 20)));
        Assert.False(conge.IsDateInHoliday(new DateTime(2026, 11, 3)));
    }

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
    [InlineData("Rentrée scolaire", true)]
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
        var calendrier = Officiel(Conge(1, "Rentrée scolaire", new DateTime(2026, 8, 24), new DateTime(2026, 8, 24)));
        var corrections = new List<UserConge> { new() { Id = 5, IdCalendrierFk = 1, Nom = "x", Masque = true } };

        Assert.Single(CalendarService.AppliquerCorrections(calendrier, corrections).Holidays);
    }

    [Fact]
    public void MarqueurDeRentree_NEstJamaisMasque()
    {
        var calendrier = Officiel(Conge(1, "Rentree scolaire", new DateTime(2026, 8, 24), new DateTime(2026, 8, 24)));
        var corrections = new List<UserConge> { new() { Id = 5, IdCalendrierFk = 1, Nom = "Rentree", Masque = true } };

        Assert.Single(CalendarService.AppliquerCorrections(calendrier, corrections).Holidays);
    }

    [Fact]
    public void RentreeCorrigee_PrendLaNouvelleDate()
    {
        var calendrier = Officiel(Conge(1, "Rentrée scolaire", new DateTime(2026, 9, 1), new DateTime(2026, 9, 1)));
        var corrections = new List<UserConge>
        {
            new() { Id = 5, IdCalendrierFk = 1, Nom = "Rentrée scolaire",
                    DateDebut = new DateTime(2026, 8, 24), DateFin = new DateTime(2026, 8, 24) },
        };

        var conge = Assert.Single(CalendarService.AppliquerCorrections(calendrier, corrections).Holidays);

        Assert.Equal(new DateTime(2026, 8, 24), conge.StartDate);
    }

    [Fact]
    public void RentreeCorrigee_DeplaceLeDebutDAnneeScolaire()
    {
        var calendrier = Officiel(Conge(1, "Rentrée scolaire", new DateTime(2026, 8, 24), new DateTime(2026, 8, 24)));
        var corrections = new List<UserConge>
        {
            new() { Id = 5, IdCalendrierFk = 1, Nom = "Rentrée scolaire",
                    DateDebut = new DateTime(2026, 9, 1), DateFin = new DateTime(2026, 9, 1) },
        };

        var resultat = CalendarService.AppliquerCorrections(calendrier, corrections);

        Assert.Equal(new DateTime(2026, 9, 1), resultat.SchoolYearStart);
    }

    [Fact]
    public void RentreeCorrigee_SupprimeLeMarqueurSynthetiqueDeLaMemeAnnee()
    {
        var calendrier = Officiel(
            Conge(1, "Rentrée scolaire", new DateTime(2026, 9, 1), new DateTime(2026, 9, 1)),
            new Holiday { Id = 0, Name = "Rentree scolaire",
                          StartDate = new DateTime(2026, 8, 26), EndDate = new DateTime(2026, 8, 26) });

        var corrections = new List<UserConge>
        {
            new() { Id = 5, IdCalendrierFk = 1, Nom = "Rentrée scolaire",
                    DateDebut = new DateTime(2026, 8, 24), DateFin = new DateTime(2026, 8, 24) },
        };

        var conge = Assert.Single(CalendarService.AppliquerCorrections(calendrier, corrections).Holidays);

        Assert.Equal(new DateTime(2026, 8, 24), conge.StartDate);
    }

    [Fact]
    public void SansCorrectionDeRentree_LeMarqueurSynthetiqueEstConserve()
    {
        var calendrier = Officiel(
            Conge(1, "Toussaint", new DateTime(2026, 10, 26), new DateTime(2026, 11, 6)),
            new Holiday { Id = 0, Name = "Rentree scolaire",
                          StartDate = new DateTime(2026, 8, 26), EndDate = new DateTime(2026, 8, 26) });

        var corrections = new List<UserConge>
        {
            new() { Id = 5, IdCalendrierFk = 1, Nom = "Conge d'automne",
                    DateDebut = new DateTime(2026, 10, 19), DateFin = new DateTime(2026, 10, 30) },
        };

        var resultat = CalendarService.AppliquerCorrections(calendrier, corrections);

        Assert.Equal(2, resultat.Holidays.Count);
        Assert.Contains(resultat.Holidays, h => h.Id == 0 && h.StartDate == new DateTime(2026, 8, 26));
    }

    [Fact]
    public void DeuxRentreesLaMemeAnnee_UneSeuleEstAffichee()
    {
        var calendrier = Officiel(
            Conge(1, "Rentrée scolaire", new DateTime(2026, 8, 24), new DateTime(2026, 8, 24)),
            Conge(2, "Rentree scolaire", new DateTime(2026, 8, 28), new DateTime(2026, 8, 28)));

        Assert.Single(CalendarService.AppliquerCorrections(calendrier, new List<UserConge>()).Holidays);
    }

    [Fact]
    public void DeuxRentreesLaMemeAnnee_LaVersionCorrigeeEstRetenue()
    {
        var calendrier = Officiel(
            Conge(1, "Rentrée scolaire", new DateTime(2026, 8, 24), new DateTime(2026, 8, 24)),
            Conge(2, "Rentree scolaire", new DateTime(2026, 8, 28), new DateTime(2026, 8, 28)));

        var corrections = new List<UserConge>
        {
            new() { Id = 5, IdCalendrierFk = 2, Nom = "Rentrée scolaire",
                    DateDebut = new DateTime(2026, 8, 31), DateFin = new DateTime(2026, 8, 31) },
        };

        var conge = Assert.Single(CalendarService.AppliquerCorrections(calendrier, corrections).Holidays);

        Assert.Equal(new DateTime(2026, 8, 31), conge.StartDate);
    }

    [Fact]
    public void RentreesDAnneesScolairesDifferentes_SontToutesConservees()
    {
        var calendrier = Officiel(
            Conge(1, "Rentrée scolaire", new DateTime(2025, 8, 25), new DateTime(2025, 8, 25)),
            Conge(2, "Rentrée scolaire", new DateTime(2026, 8, 24), new DateTime(2026, 8, 24)));

        Assert.Equal(2, CalendarService.AppliquerCorrections(calendrier, new List<UserConge>()).Holidays.Count);
    }

    [Fact]
    public void MemeCongeSousDeuxLibelles_EstAfficheUneSeuleFois()
    {
        var calendrier = Officiel(
            Conge(1, "Vacances d'automne (Toussaint)", new DateTime(2026, 10, 19), new DateTime(2026, 11, 1)),
            Conge(2, "Congé d'automne (Toussaint)", new DateTime(2026, 10, 19), new DateTime(2026, 11, 1)));

        Assert.Single(CalendarService.AppliquerCorrections(calendrier, new List<UserConge>()).Holidays);
    }

    [Fact]
    public void MemeCongeSousDeuxLibelles_LaCorrectionSAppliqueAuxDeux()
    {
        var calendrier = Officiel(
            Conge(1, "Vacances d'automne (Toussaint)", new DateTime(2026, 10, 19), new DateTime(2026, 11, 1)),
            Conge(2, "Congé d'automne (Toussaint)", new DateTime(2026, 10, 19), new DateTime(2026, 11, 1)));

        var corrections = new List<UserConge>
        {
            new() { Id = 5, IdCalendrierFk = 1, Nom = "Toussaint",
                    DateDebut = new DateTime(2026, 10, 26), DateFin = new DateTime(2026, 11, 8) },
        };

        var conge = Assert.Single(CalendarService.AppliquerCorrections(calendrier, corrections).Holidays);

        Assert.Equal(new DateTime(2026, 10, 26), conge.StartDate);
        Assert.Equal(new DateTime(2026, 11, 8), conge.EndDate);
    }

    [Fact]
    public void CongesHomonymesSansChevauchement_SontConserves()
    {
        var calendrier = Officiel(
            Conge(1, "Lundi de Pâques", new DateTime(2027, 4, 6), new DateTime(2027, 4, 6)),
            Conge(2, "Vacances de printemps (Pâques)", new DateTime(2027, 4, 27), new DateTime(2027, 5, 10)));

        Assert.Equal(2, CalendarService.AppliquerCorrections(calendrier, new List<UserConge>()).Holidays.Count);
    }

    [Fact]
    public void MarqueurSynthetique_SEffaceDevantLaRentreeOfficielle()
    {
        var calendrier = Officiel(
            new Holiday { Id = 0, Name = "Rentree scolaire",
                          StartDate = new DateTime(2026, 8, 26), EndDate = new DateTime(2026, 8, 26) },
            Conge(1, "Rentrée scolaire", new DateTime(2026, 8, 24), new DateTime(2026, 8, 24)));

        var conge = Assert.Single(CalendarService.AppliquerCorrections(calendrier, new List<UserConge>()).Holidays);

        Assert.Equal(1, conge.Id);
        Assert.Equal(new DateTime(2026, 8, 24), conge.StartDate);
    }

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
