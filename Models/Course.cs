namespace Obrigenie.Models
{
    public class Course
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Color { get; set; } = "#FF0000";
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public int DaysOfWeek { get; set; }

        public bool OccursOnDay(DayOfWeek dayOfWeek)
        {
            return dayOfWeek switch
            {
                DayOfWeek.Monday => (DaysOfWeek & 1) != 0,
                DayOfWeek.Tuesday => (DaysOfWeek & 2) != 0,
                DayOfWeek.Wednesday => (DaysOfWeek & 4) != 0,
                DayOfWeek.Thursday => (DaysOfWeek & 8) != 0,
                DayOfWeek.Friday => (DaysOfWeek & 16) != 0,
                DayOfWeek.Saturday => (DaysOfWeek & 32) != 0,
                DayOfWeek.Sunday => (DaysOfWeek & 64) != 0,
                _ => false
            };
        }
    }
}
