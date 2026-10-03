using Obrigenie.Models;
using Obrigenie.Services;

namespace ObrigenieTest;

public class SchoolPeriodHelperTests
{
    private static List<Holiday> MakeHolidays() =>
    [
        new() { Name = "Rentree scolaire",               StartDate = new(2024, 9, 2),   EndDate = new(2024, 9, 2) },

        new() { Name = "Conge d'automne (Toussaint)",    StartDate = new(2024, 10, 28), EndDate = new(2024, 11, 3) },

        new() { Name = "Vacances d'hiver (Noel)",        StartDate = new(2024, 12, 23), EndDate = new(2025, 1, 5) },

        new() { Name = "Conge de detente (Carnaval)",    StartDate = new(2025, 3, 3),   EndDate = new(2025, 3, 16) },

        new() { Name = "Vacances de printemps (Paques)", StartDate = new(2025, 4, 14),  EndDate = new(2025, 4, 27) },

        new() { Name = "Vacances d'ete",                 StartDate = new(2025, 7, 7),   EndDate = new(2025, 8, 31) },
    ];

    [Fact]
    public void GetLabel_DuringHoliday_ShowsHolidayName()
    {
        var label = SchoolPeriodHelper.GetLabel(new DateTime(2024, 10, 30), MakeHolidays());

        Assert.Contains("Toussaint", label);
    }

    [Fact]
    public void GetLabel_BetweenTwoHolidays_ShowsTransition()
    {
        var label = SchoolPeriodHelper.GetLabel(new DateTime(2024, 11, 15), MakeHolidays());

        Assert.NotNull(label);
        Assert.Contains("→", label);
    }

    [Fact]
    public void GetLabel_BeforeFirstHoliday_ReturnsNonNull()
    {
        var label = SchoolPeriodHelper.GetLabel(new DateTime(2024, 9, 15), MakeHolidays());

        Assert.NotNull(label);
    }

    [Fact]
    public void GetLabel_EmptyList_ReturnsNull()
    {
        var label = SchoolPeriodHelper.GetLabel(new DateTime(2024, 10, 15), new List<Holiday>());

        Assert.Null(label);
    }

    [Fact]
    public void GetLabel_DuringChristmas_ShowsNoel()
    {
        var label = SchoolPeriodHelper.GetLabel(new DateTime(2024, 12, 25), MakeHolidays());

        Assert.Contains("Noël", label);
    }

    [Fact]
    public void GetLabel_DuringSummer_ShowsEte()
    {
        var label = SchoolPeriodHelper.GetLabel(new DateTime(2025, 7, 15), MakeHolidays());

        Assert.Contains("Été", label);
    }

    [Fact]
    public void GetPeriodBounds_BetweenToussaintAndNoel_ReturnsCorrectBounds()
    {
        var date = new DateTime(2024, 11, 15);
        var (start, end, title) = SchoolPeriodHelper.GetPeriodBounds(date, MakeHolidays());

        Assert.True(start <= date);
        Assert.True(end >= date);

        Assert.Contains("→", title);
    }

    [Fact]
    public void GetPeriodBounds_DuringHoliday_RetreatsToBeforeHoliday()
    {
        var date = new DateTime(2024, 10, 30);
        var (start, end, title) = SchoolPeriodHelper.GetPeriodBounds(date, MakeHolidays());

        Assert.True(end < date || start < new DateTime(2024, 10, 28));
    }

    [Fact]
    public void GetPeriodBounds_AlwaysReturnsTitle()
    {
        var (_, _, title) = SchoolPeriodHelper.GetPeriodBounds(
            new DateTime(2024, 11, 15), MakeHolidays());

        Assert.False(string.IsNullOrEmpty(title));
    }
}
