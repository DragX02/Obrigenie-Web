using Obrigenie.Models;

namespace ObrigenieTest;

/// <summary>
/// Unit tests for the Course and Holiday model classes.
/// These tests exercise pure logic with no external dependencies:
/// no database, no HTTP client, and no browser APIs are needed.
/// </summary>
public class ModelTests
{
    // ─────────────────────────────────────────────────────────────────────────
    // Course.OccursOnDay — bitmask day-of-week checks
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Verifies that OccursOnDay returns the expected result for every combination
    /// of a single-bit DaysOfWeek value and the corresponding DayOfWeek enum value.
    /// Each data row tests one specific day bit and one expected boolean outcome.
    /// </summary>
    [Theory]
    [InlineData(1,  DayOfWeek.Monday,    true)]   // bit 0 set   → Monday true
    [InlineData(1,  DayOfWeek.Tuesday,   false)]  // bit 0 set   → Tuesday false
    [InlineData(2,  DayOfWeek.Tuesday,   true)]   // bit 1 set   → Tuesday true
    [InlineData(2,  DayOfWeek.Wednesday, false)]  // bit 1 set   → Wednesday false
    [InlineData(4,  DayOfWeek.Wednesday, true)]   // bit 2 set   → Wednesday true
    [InlineData(8,  DayOfWeek.Thursday,  true)]   // bit 3 set   → Thursday true
    [InlineData(16, DayOfWeek.Friday,    true)]   // bit 4 set   → Friday true
    [InlineData(32, DayOfWeek.Saturday,  true)]   // bit 5 set   → Saturday true
    [InlineData(64, DayOfWeek.Sunday,    true)]   // bit 6 set   → Sunday true
    public void Course_OccursOnDay_EachDay(int daysOfWeek, DayOfWeek day, bool expected)
    {
        // Arrange: create a course with only the specified day bit set
        var course = new Course { DaysOfWeek = daysOfWeek };

        // Act + Assert: the method should match the expected boolean for that day
        Assert.Equal(expected, course.OccursOnDay(day));
    }

    /// <summary>
    /// Verifies that OccursOnDay correctly identifies multiple days
    /// when the DaysOfWeek bitmask has several bits set simultaneously.
    /// Tests the Mon + Wed + Fri combination (1 + 4 + 16 = 21).
    /// </summary>
    [Fact]
    public void Course_OccursOnDay_MultipleDays()
    {
        // Arrange: Monday (1) + Wednesday (4) + Friday (16) = 21
        var course = new Course { DaysOfWeek = 1 + 4 + 16 };

        // Act + Assert: the three set days should return true
        Assert.True(course.OccursOnDay(DayOfWeek.Monday));
        Assert.True(course.OccursOnDay(DayOfWeek.Wednesday));
        Assert.True(course.OccursOnDay(DayOfWeek.Friday));

        // The unset days should return false
        Assert.False(course.OccursOnDay(DayOfWeek.Tuesday));
        Assert.False(course.OccursOnDay(DayOfWeek.Thursday));
    }

    /// <summary>
    /// Verifies that OccursOnDay returns false for every day of the week
    /// when the DaysOfWeek bitmask is 0 (no days selected).
    /// </summary>
    [Fact]
    public void Course_OccursOnDay_NoDays_ReturnsFalse()
    {
        // Arrange: no bits set → course never occurs
        var course = new Course { DaysOfWeek = 0 };

        // Act + Assert: all days must return false
        foreach (var day in Enum.GetValues<DayOfWeek>())
            Assert.False(course.OccursOnDay(day));
    }

    /// <summary>
    /// Verifies that OccursOnDay returns true for every day of the week
    /// when all seven day bits are set (DaysOfWeek = 127 = 1+2+4+8+16+32+64).
    /// </summary>
    [Fact]
    public void Course_OccursOnDay_AllDays()
    {
        // Arrange: all seven bits set → course occurs every day
        var course = new Course { DaysOfWeek = 127 };

        // Act + Assert: every day of the week must return true
        foreach (var day in Enum.GetValues<DayOfWeek>())
            Assert.True(course.OccursOnDay(day));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Holiday.IsDateInHoliday — inclusive date range checks
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Verifies that a date that falls strictly inside a holiday period returns true.
    /// </summary>
    [Fact]
    public void Holiday_IsDateInHoliday_DateInside_ReturnsTrue()
    {
        // Arrange: Toussaint 2025 from Oct 27 to Nov 3
        var h = new Holiday
        {
            StartDate = new DateTime(2025, 10, 27),
            EndDate   = new DateTime(2025, 11, 3)
        };

        // Act + Assert: Oct 30 is inside the range
        Assert.True(h.IsDateInHoliday(new DateTime(2025, 10, 30)));
    }

    /// <summary>
    /// Verifies that the first day (StartDate) of a holiday period is included (inclusive lower bound).
    /// </summary>
    [Fact]
    public void Holiday_IsDateInHoliday_StartDate_ReturnsTrue()
    {
        var h = new Holiday
        {
            StartDate = new DateTime(2025, 10, 27),
            EndDate   = new DateTime(2025, 11, 3)
        };

        // Act + Assert: the start date itself must be included
        Assert.True(h.IsDateInHoliday(new DateTime(2025, 10, 27)));
    }

    /// <summary>
    /// Verifies that the last day (EndDate) of a holiday period is included (inclusive upper bound).
    /// </summary>
    [Fact]
    public void Holiday_IsDateInHoliday_EndDate_ReturnsTrue()
    {
        var h = new Holiday
        {
            StartDate = new DateTime(2025, 10, 27),
            EndDate   = new DateTime(2025, 11, 3)
        };

        // Act + Assert: the end date itself must be included
        Assert.True(h.IsDateInHoliday(new DateTime(2025, 11, 3)));
    }

    /// <summary>
    /// Verifies that the day immediately before the start of a holiday returns false.
    /// </summary>
    [Fact]
    public void Holiday_IsDateInHoliday_DayBefore_ReturnsFalse()
    {
        var h = new Holiday
        {
            StartDate = new DateTime(2025, 10, 27),
            EndDate   = new DateTime(2025, 11, 3)
        };

        // Act + Assert: Oct 26 is one day before the start — must not be included
        Assert.False(h.IsDateInHoliday(new DateTime(2025, 10, 26)));
    }

    /// <summary>
    /// Verifies that the day immediately after the end of a holiday returns false.
    /// </summary>
    [Fact]
    public void Holiday_IsDateInHoliday_DayAfter_ReturnsFalse()
    {
        var h = new Holiday
        {
            StartDate = new DateTime(2025, 10, 27),
            EndDate   = new DateTime(2025, 11, 3)
        };

        // Act + Assert: Nov 4 is one day after the end — must not be included
        Assert.False(h.IsDateInHoliday(new DateTime(2025, 11, 4)));
    }

    /// <summary>
    /// Verifies the edge case of a single-day holiday:
    /// the exact date returns true, and the next day returns false.
    /// </summary>
    [Fact]
    public void Holiday_IsDateInHoliday_SingleDay_BothEdges()
    {
        // Arrange: a single-day marker (e.g., Rentrée scolaire)
        var h = new Holiday
        {
            StartDate = new DateTime(2025, 9, 1),
            EndDate   = new DateTime(2025, 9, 1)
        };

        // Act + Assert: the exact day is included; the next day is not
        Assert.True(h.IsDateInHoliday(new DateTime(2025, 9, 1)));
        Assert.False(h.IsDateInHoliday(new DateTime(2025, 9, 2)));
    }
}
