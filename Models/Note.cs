namespace Obrigenie.Models
{
    public class Note
    {
        public int Id { get; set; }

        public DateTime Date { get; set; }

        public int Hour { get; set; }

        public int EndHour { get; set; }

        public int Minute { get; set; }

        public int EndMinute { get; set; }

        public string Content { get; set; } = string.Empty;

        public string? Titre { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime ModifiedAt { get; set; }

        public int? IdViseeFk { get; set; }

        public string? ViseeContexte { get; set; }
    }
}
