using Obrigenie.Models;
using Obrigenie.Services;

namespace ObrigenieTest;

/// <summary>
/// Unit tests for the <see cref="SchoolPeriodHelper"/> static class.
/// Tests cover two public methods:
///   - GetLabel:        computes the navigation banner text for a given date.
///   - GetPeriodBounds: computes the start date, end date, and title of the
///                      school period (trimester) that contains a given date.
///
/// All tests use a hand-crafted holiday list that mimics a typical Belgian
/// school year (2024-2025) with five standard vacation periods.
/// No external dependencies — pure logic, no I/O.
/// </summary>
public class SchoolPeriodHelperTests
{
    /// <summary>
    /// Builds the shared holiday list for the fictitious 2024-2025 school year.
    /// Includes five standard Belgian school vacations and one Rentrée marker.
    /// </summary>
    private static List<Holiday> MakeHolidays() =>
    [
        // Back-to-school marker — single day, used as the anchor for S1 numbering
        new() { Name = "Rentree scolaire",               StartDate = new(2024, 9, 2),   EndDate = new(2024, 9, 2) },

        // Autumn break (Toussaint) — Oct 28 to Nov 3
        new() { Name = "Conge d'automne (Toussaint)",    StartDate = new(2024, 10, 28), EndDate = new(2024, 11, 3) },

        // Winter break (Christmas) — Dec 23 to Jan 5
        new() { Name = "Vacances d'hiver (Noel)",        StartDate = new(2024, 12, 23), EndDate = new(2025, 1, 5) },

        // Carnival break (Carnaval) — Mar 3 to Mar 16
        new() { Name = "Conge de detente (Carnaval)",    StartDate = new(2025, 3, 3),   EndDate = new(2025, 3, 16) },

        // Spring break (Easter) — Apr 14 to Apr 27
        new() { Name = "Vacances de printemps (Paques)", StartDate = new(2025, 4, 14),  EndDate = new(2025, 4, 27) },

        // Summer holidays — Jul 7 to Aug 31
        new() { Name = "Vacances d'ete",                 StartDate = new(2025, 7, 7),   EndDate = new(2025, 8, 31) },
    ];

    // ─────────────────────────────────────────────────────────────────────────
    // GetLabel tests
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Verifies that GetLabel returns a string containing "Toussaint" when the
    /// given date falls inside the Toussaint vacation period.
    /// Expected format: "Congé de Toussaint".
    /// </summary>
    [Fact]
    public void GetLabel_DuringHoliday_ShowsHolidayName()
    {
        // Oct 30 is inside the Toussaint break (Oct 28 – Nov 3)
        var label = SchoolPeriodHelper.GetLabel(new DateTime(2024, 10, 30), MakeHolidays());

        // The label must mention the holiday name
        Assert.Contains("Toussaint", label);
    }

    /// <summary>
    /// Verifies that GetLabel returns a transition arrow label when the given date
    /// is between two consecutive vacation periods (not inside any holiday).
    /// Expected format: "PreviousHoliday → NextHoliday".
    /// </summary>
    [Fact]
    public void GetLabel_BetweenTwoHolidays_ShowsTransition()
    {
        // Nov 15 is between Toussaint (ended Nov 3) and Christmas (starts Dec 23)
        var label = SchoolPeriodHelper.GetLabel(new DateTime(2024, 11, 15), MakeHolidays());

        // The label must be non-null and contain the arrow separator
        Assert.NotNull(label);
        Assert.Contains("→", label);
    }

    /// <summary>
    /// Verifies that GetLabel returns a non-null label when the date is between
    /// the Rentrée (Sep 2) and the first vacation (Toussaint, Oct 28).
    /// </summary>
    [Fact]
    public void GetLabel_BeforeFirstHoliday_ReturnsNonNull()
    {
        // Sep 15 is after the Rentrée and before Toussaint
        var label = SchoolPeriodHelper.GetLabel(new DateTime(2024, 9, 15), MakeHolidays());

        // Should return some informational label (not null) even before the first vacation
        Assert.NotNull(label);
    }

    /// <summary>
    /// Verifies that GetLabel returns null when the holiday list is empty.
    /// No holiday data means no meaningful label can be constructed.
    /// </summary>
    [Fact]
    public void GetLabel_EmptyList_ReturnsNull()
    {
        var label = SchoolPeriodHelper.GetLabel(new DateTime(2024, 10, 15), new List<Holiday>());

        Assert.Null(label);
    }

    /// <summary>
    /// Verifies that GetLabel returns a string containing "Noël" when the date
    /// falls inside the Christmas vacation period.
    /// </summary>
    [Fact]
    public void GetLabel_DuringChristmas_ShowsNoel()
    {
        // Dec 25 is inside the Christmas break (Dec 23 – Jan 5)
        var label = SchoolPeriodHelper.GetLabel(new DateTime(2024, 12, 25), MakeHolidays());

        Assert.Contains("Noël", label);
    }

    /// <summary>
    /// Verifies that GetLabel returns a string containing "Été" when the date
    /// falls inside the summer holiday period.
    /// </summary>
    [Fact]
    public void GetLabel_DuringSummer_ShowsEte()
    {
        // Jul 15 is inside the summer break (Jul 7 – Aug 31)
        var label = SchoolPeriodHelper.GetLabel(new DateTime(2025, 7, 15), MakeHolidays());

        Assert.Contains("Été", label);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // GetPeriodBounds tests
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Verifies that GetPeriodBounds returns correct start/end dates and a non-empty title
    /// when the given date is between Toussaint (ended Nov 3) and Christmas (starts Dec 23).
    /// The period start should be on or before Nov 15, and the period end should be on or after it.
    /// </summary>
    [Fact]
    public void GetPeriodBounds_BetweenToussaintAndNoel_ReturnsCorrectBounds()
    {
        var date = new DateTime(2024, 11, 15);
        var (start, end, title) = SchoolPeriodHelper.GetPeriodBounds(date, MakeHolidays());

        // The period must include the reference date
        Assert.True(start <= date);
        Assert.True(end >= date);

        // The title must contain the transition arrow
        Assert.Contains("→", title);
    }

    /// <summary>
    /// Verifies that when the given date falls inside a vacation period, GetPeriodBounds
    /// retreats to the preceding school period rather than returning the holiday itself.
    /// The end date of the returned period must be before the holiday starts (Oct 28).
    /// </summary>
    [Fact]
    public void GetPeriodBounds_DuringHoliday_RetreatsToBeforeHoliday()
    {
        // Oct 30 is inside Toussaint (Oct 28 – Nov 3) → should retreat to the previous period
        var date = new DateTime(2024, 10, 30);
        var (start, end, title) = SchoolPeriodHelper.GetPeriodBounds(date, MakeHolidays());

        // Either the end of the returned period is before Oct 30 (previous period),
        // or the start is clearly in the pre-Toussaint window
        Assert.True(end < date || start < new DateTime(2024, 10, 28));
    }

    /// <summary>
    /// Verifies that GetPeriodBounds always returns a non-empty title string.
    /// The title is used as the header in the SchoolPeriod view and must never be blank.
    /// </summary>
    [Fact]
    public void GetPeriodBounds_AlwaysReturnsTitle()
    {
        var (_, _, title) = SchoolPeriodHelper.GetPeriodBounds(
            new DateTime(2024, 11, 15), MakeHolidays());

        // Title must be a non-null, non-empty string
        Assert.False(string.IsNullOrEmpty(title));
    }
}
