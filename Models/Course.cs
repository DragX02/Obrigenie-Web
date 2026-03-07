namespace Obrigenie.Models
{
    // Représente un cours récurrent (classe/leçon) qui apparaît dans le calendrier scolaire.
    // Un cours a un planning hebdomadaire fixe encodé sous forme de masque de bits, une plage horaire et une couleur d'affichage.
    // Les cours sont chargés depuis l'API pour une date spécifique et affichés dans les vues jour et semaine.
    public class Course
    {
        // L'identifiant unique attribué par le serveur pour ce cours.
        public int Id { get; set; }

        // Le nom d'affichage du cours (ex. : "Mathématiques", "Français").
        // Affiché dans les blocs de cours du calendrier.
        public string Name { get; set; } = string.Empty;

        // La couleur de fond utilisée pour rendre ce bloc de cours dans le calendrier.
        // Stockée sous forme de chaîne de couleur CSS (ex. : "#FF5733" ou "rgba(255,87,51,1)").
        // Par défaut rouge (#FF0000) lorsqu'aucune couleur n'est assignée.
        public string Color { get; set; } = "#FF0000";

        // La première date du calendrier à laquelle ce cours se produit.
        // Utilisée pour restreindre le cours à la bonne année scolaire.
        public DateTime StartDate { get; set; }

        // La dernière date du calendrier à laquelle ce cours se produit.
        // Le cours n'apparaîtra pas pour les dates au-delà de cette valeur.
        public DateTime EndDate { get; set; }

        // L'heure de début quotidienne du cours (ex. : TimeSpan(8, 30, 0) pour 08:30).
        // Utilisée pour positionner le bloc de cours dans la grille horaire de la vue d'un seul jour.
        public TimeSpan StartTime { get; set; }

        // L'heure de fin quotidienne du cours (ex. : TimeSpan(10, 0, 0) pour 10:00).
        // Utilisée pour calculer l'étendue verticale du bloc de cours dans la grille horaire.
        public TimeSpan EndTime { get; set; }

        // Un masque de bits qui encode les jours de la semaine où ce cours se produit.
        // Valeurs de bits : Lundi=1, Mardi=2, Mercredi=4, Jeudi=8,
        //                  Vendredi=16, Samedi=32, Dimanche=64.
        // Plusieurs jours sont combinés avec un OU bit à bit (ex. : Lun+Mer+Ven = 1+4+16 = 21).
        public int DaysOfWeek { get; set; }

        // Détermine si ce cours se produit le jour de la semaine donné
        // en vérifiant le bit correspondant dans le masque de bits DaysOfWeek.
        public bool OccursOnDay(DayOfWeek dayOfWeek)
        {
            // Chaque jour correspond à une position de bit spécifique dans l'entier DaysOfWeek.
            // Un ET bit à bit avec le masque de ce jour retourne une valeur non nulle lorsque le bit est défini.
            return dayOfWeek switch
            {
                DayOfWeek.Monday    => (DaysOfWeek & 1) != 0,   // bit 0 → Lundi
                DayOfWeek.Tuesday   => (DaysOfWeek & 2) != 0,   // bit 1 → Mardi
                DayOfWeek.Wednesday => (DaysOfWeek & 4) != 0,   // bit 2 → Mercredi
                DayOfWeek.Thursday  => (DaysOfWeek & 8) != 0,   // bit 3 → Jeudi
                DayOfWeek.Friday    => (DaysOfWeek & 16) != 0,  // bit 4 → Vendredi
                DayOfWeek.Saturday  => (DaysOfWeek & 32) != 0,  // bit 5 → Samedi
                DayOfWeek.Sunday    => (DaysOfWeek & 64) != 0,  // bit 6 → Dimanche
                _ => false                                        // jour inconnu → ne se produit jamais
            };
        }
    }
}
