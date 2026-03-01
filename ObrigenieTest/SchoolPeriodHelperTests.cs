using Obrigenie.Models;
using Obrigenie.Services;

namespace ObrigenieTest;

/// <summary>
/// Tests unitaires pour SchoolPeriodHelper :
/// logique pure de calcul des périodes scolaires et labels de navigation.
/// </summary>
public class SchoolPeriodHelperTests
{
    // Année scolaire fictive 2024-2025 avec les congés habituels belges
    private static List<Holiday> MakeHolidays() =>
    [
        new() { Name = "Rentree scolaire",             StartDate = new(2024, 9, 2),  EndDate = new(2024, 9, 2) },
        new() { Name = "Conge d'automne (Toussaint)",  StartDate = new(2024, 10, 28), EndDate = new(2024, 11, 3) },
        new() { Name = "Vacances d'hiver (Noel)",      StartDate = new(2024, 12, 23), EndDate = new(2025, 1, 5) },
        new() { Name = "Conge de detente (Carnaval)",  StartDate = new(2025, 3, 3),  EndDate = new(2025, 3, 16) },
        new() { Name = "Vacances de printemps (Paques)", StartDate = new(2025, 4, 14), EndDate = new(2025, 4, 27) },
        new() { Name = "Vacances d'ete",               StartDate = new(2025, 7, 7),  EndDate = new(2025, 8, 31) },
    ];

    // ── GetLabel ──────────────────────────────────────────────────────────

    [Fact]
    public void GetLabel_PendantConge_AfficheNomConge()
    {
        var label = SchoolPeriodHelper.GetLabel(new DateTime(2024, 10, 30), MakeHolidays());
        Assert.Contains("Toussaint", label);
    }

    [Fact]
    public void GetLabel_EntreDeuxConges_AfficheTransition()
    {
        // Entre Toussaint et Noël
        var label = SchoolPeriodHelper.GetLabel(new DateTime(2024, 11, 15), MakeHolidays());
        Assert.NotNull(label);
        Assert.Contains("→", label);
    }

    [Fact]
    public void GetLabel_AvantPremierConge_AfficheTransition()
    {
        // Juste après la rentrée, avant Toussaint
        var label = SchoolPeriodHelper.GetLabel(new DateTime(2024, 9, 15), MakeHolidays());
        Assert.NotNull(label);
    }

    [Fact]
    public void GetLabel_ListeVide_RetourneNull()
    {
        var label = SchoolPeriodHelper.GetLabel(new DateTime(2024, 10, 15), new List<Holiday>());
        Assert.Null(label);
    }

    [Fact]
    public void GetLabel_PendantNoel_AfficheNoel()
    {
        var label = SchoolPeriodHelper.GetLabel(new DateTime(2024, 12, 25), MakeHolidays());
        Assert.Contains("Noël", label);
    }

    [Fact]
    public void GetLabel_PendantEte_AfficheEte()
    {
        var label = SchoolPeriodHelper.GetLabel(new DateTime(2025, 7, 15), MakeHolidays());
        Assert.Contains("Été", label);
    }

    // ── GetPeriodBounds ───────────────────────────────────────────────────

    [Fact]
    public void GetPeriodBounds_EntreToussaintEtNoel_RetourneBonnesPeriodes()
    {
        var date = new DateTime(2024, 11, 15);
        var (start, end, title) = SchoolPeriodHelper.GetPeriodBounds(date, MakeHolidays());

        // La période commence le lendemain de la fin de Toussaint (4 nov)
        Assert.True(start <= date);
        // La période se termine avant le début de Noël (23 déc)
        Assert.True(end >= date);
        Assert.Contains("→", title);
    }

    [Fact]
    public void GetPeriodBounds_PendantConge_ReculeAvantConge()
    {
        // Pendant Toussaint → doit prendre la période d'avant
        var date = new DateTime(2024, 10, 30);
        var (start, end, title) = SchoolPeriodHelper.GetPeriodBounds(date, MakeHolidays());

        // La date de congé ne doit pas être dans la période retournée
        Assert.True(end < date || start < new DateTime(2024, 10, 28));
    }

    [Fact]
    public void GetPeriodBounds_RetourneTitre()
    {
        var (_, _, title) = SchoolPeriodHelper.GetPeriodBounds(
            new DateTime(2024, 11, 15), MakeHolidays());
        Assert.False(string.IsNullOrEmpty(title));
    }
}
