namespace Obrigenie.Models
{
    public class SchoolYearCalendar
    {
        public DateTime SchoolYearStart { get; set; }
        public List<Holiday> Holidays { get; set; } = new();
    }
}
