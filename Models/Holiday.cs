namespace Obrigenie.Models
{
    public class Holiday
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }

        public bool IsDateInHoliday(DateTime date)
        {
            return date.Date >= StartDate.Date && date.Date <= EndDate.Date;
        }
    }
}
