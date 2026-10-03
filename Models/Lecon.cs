namespace Obrigenie.Models
{
    public class Lecon
    {
        public int Id { get; set; }

        public string Titre { get; set; } = string.Empty;

        public string Enseignant { get; set; } = string.Empty;

        public string Duree { get; set; } = string.Empty;

        public int NombreSeances { get; set; } = 1;

        public string Niveaux { get; set; } = string.Empty;

        public string Competences { get; set; } = string.Empty;

        public int? IdViseeFk { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime ModifiedAt { get; set; }

        public List<LeconPhase> Phases { get; set; } = new();

        public Lecon Copier() => new()
        {
            Id            = Id,
            Titre         = Titre,
            Enseignant    = Enseignant,
            Duree         = Duree,
            NombreSeances = NombreSeances,
            Niveaux       = Niveaux,
            Competences   = Competences,
            IdViseeFk     = IdViseeFk,
            CreatedAt     = CreatedAt,
            ModifiedAt    = ModifiedAt,
            Phases        = Phases.Select(p => p.Copier()).ToList(),
        };
    }

    public class LeconPhase
    {
        public int Id { get; set; }

        public int Ordre { get; set; }

        public string Intitule { get; set; } = string.Empty;

        public string Temps { get; set; } = string.Empty;

        public LeconPhase Copier() => new()
        {
            Id       = Id,
            Ordre    = Ordre,
            Intitule = Intitule,
            Temps    = Temps,
        };
    }
}
