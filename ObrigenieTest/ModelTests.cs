using Obrigenie.Models;

namespace ObrigenieTest;

/// <summary>
/// Tests unitaires pour les modèles Course et Holiday.
/// Aucune dépendance externe — logique pure.
/// </summary>
public class ModelTests
{
    // ── Course.OccursOnDay ────────────────────────────────────────────────

    [Theory]
    [InlineData(1,  DayOfWeek.Monday,    true)]
    [InlineData(1,  DayOfWeek.Tuesday,   false)]
    [InlineData(2,  DayOfWeek.Tuesday,   true)]
    [InlineData(2,  DayOfWeek.Wednesday, false)]
    [InlineData(4,  DayOfWeek.Wednesday, true)]
    [InlineData(8,  DayOfWeek.Thursday,  true)]
    [InlineData(16, DayOfWeek.Friday,    true)]
    [InlineData(32, DayOfWeek.Saturday,  true)]
    [InlineData(64, DayOfWeek.Sunday,    true)]
    public void Course_OccursOnDay_ChaqueJour(int daysOfWeek, DayOfWeek day, bool expected)
    {
        var course = new Course { DaysOfWeek = daysOfWeek };
        Assert.Equal(expected, course.OccursOnDay(day));
    }

    [Fact]
    public void Course_OccursOnDay_MultipleJours()
    {
        // Lundi (1) + Mercredi (4) + Vendredi (16) = 21
        var course = new Course { DaysOfWeek = 1 + 4 + 16 };

        Assert.True(course.OccursOnDay(DayOfWeek.Monday));
        Assert.True(course.OccursOnDay(DayOfWeek.Wednesday));
        Assert.True(course.OccursOnDay(DayOfWeek.Friday));
        Assert.False(course.OccursOnDay(DayOfWeek.Tuesday));
        Assert.False(course.OccursOnDay(DayOfWeek.Thursday));
    }

    [Fact]
    public void Course_OccursOnDay_Aucun_RetourneFalse()
    {
        var course = new Course { DaysOfWeek = 0 };
        foreach (var day in Enum.GetValues<DayOfWeek>())
            Assert.False(course.OccursOnDay(day));
    }

    [Fact]
    public void Course_OccursOnDay_TousLesJours()
    {
        // 1+2+4+8+16+32+64 = 127
        var course = new Course { DaysOfWeek = 127 };
        foreach (var day in Enum.GetValues<DayOfWeek>())
            Assert.True(course.OccursOnDay(day));
    }

    // ── Holiday.IsDateInHoliday ───────────────────────────────────────────

    [Fact]
    public void Holiday_IsDateInHoliday_DateDedans_RetourneTrue()
    {
        var h = new Holiday
        {
            StartDate = new DateTime(2025, 10, 27),
            EndDate   = new DateTime(2025, 11, 3)
        };
        Assert.True(h.IsDateInHoliday(new DateTime(2025, 10, 30)));
    }

    [Fact]
    public void Holiday_IsDateInHoliday_DateDebut_RetourneTrue()
    {
        var h = new Holiday
        {
            StartDate = new DateTime(2025, 10, 27),
            EndDate   = new DateTime(2025, 11, 3)
        };
        Assert.True(h.IsDateInHoliday(new DateTime(2025, 10, 27)));
    }

    [Fact]
    public void Holiday_IsDateInHoliday_DateFin_RetourneTrue()
    {
        var h = new Holiday
        {
            StartDate = new DateTime(2025, 10, 27),
            EndDate   = new DateTime(2025, 11, 3)
        };
        Assert.True(h.IsDateInHoliday(new DateTime(2025, 11, 3)));
    }

    [Fact]
    public void Holiday_IsDateInHoliday_DateAvant_RetourneFalse()
    {
        var h = new Holiday
        {
            StartDate = new DateTime(2025, 10, 27),
            EndDate   = new DateTime(2025, 11, 3)
        };
        Assert.False(h.IsDateInHoliday(new DateTime(2025, 10, 26)));
    }

    [Fact]
    public void Holiday_IsDateInHoliday_DateApres_RetourneFalse()
    {
        var h = new Holiday
        {
            StartDate = new DateTime(2025, 10, 27),
            EndDate   = new DateTime(2025, 11, 3)
        };
        Assert.False(h.IsDateInHoliday(new DateTime(2025, 11, 4)));
    }

    [Fact]
    public void Holiday_IsDateInHoliday_UnJour_RetourneTrue()
    {
        var h = new Holiday
        {
            StartDate = new DateTime(2025, 9, 1),
            EndDate   = new DateTime(2025, 9, 1)
        };
        Assert.True(h.IsDateInHoliday(new DateTime(2025, 9, 1)));
        Assert.False(h.IsDateInHoliday(new DateTime(2025, 9, 2)));
    }
}
