namespace Obrigenie.Models
{
    public class Day
    {
        public DateTime Date { get; set; }
        public string DayOfWeek { get; set; } = string.Empty;
        public string DayOfMonth { get; set; } = string.Empty;
        public bool IsHoliday { get; set; }
        public string HolidayName { get; set; } = string.Empty;
        public bool IsSchoolStart { get; set; }
        public List<Course> Courses { get; set; } = new();
        public List<Note> Notes { get; set; } = new();

        public string ShortHolidayName
        {
            get
            {
                if (string.IsNullOrEmpty(HolidayName)) return string.Empty;
                return HolidayName
                    .Replace("Vacances d'hiver (Noel)", "Noel")
                    .Replace("Vacances d'hiver", "Hiver")
                    .Replace("Conge d'automne (Toussaint)", "Toussaint")
                    .Replace("Vacances d'automne (Toussaint)", "Toussaint")
                    .Replace("Conge de detente (Carnaval)", "Carnaval")
                    .Replace("Vacances de detente (Carnaval)", "Carnaval")
                    .Replace("Vacances de printemps (Paques)", "Paques")
                    .Replace("Vacances de printemps", "Printemps")
                    .Replace("Les vacances d'ete debutent le", "Ete")
                    .Replace("Fin de l'annee scolaire", "Ete")
                    .Replace("Rentree scolaire", "Rentree")
                    .Replace("Jour de l'Armistice", "Armistice")
                    .Replace("Fete de la Communaute francaise", "Fete CF")
                    .Replace("Fete des morts", "Toussaint")
                    .Replace("Lundi de Paques", "Paques")
                    .Replace("Jeudi de l'Ascension", "Ascension")
                    .Replace("Lundi de Pentecote", "Pentecote")
                    .Replace("Mardi gras", "Carnaval");
            }
        }
    }
}
