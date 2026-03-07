namespace Obrigenie.Models
{
    // Représente une période de congé scolaire ou un événement notable du calendrier scolaire
    // (comme la date de rentrée "Rentrée").
    // Les congés sont chargés depuis l'API et mis en cache dans le stockage local par CalendarService.
    // Ils gèrent la coloration, l'étiquetage et les limites des périodes scolaires dans la vue calendrier.
    public class Holiday
    {
        // L'identifiant de base de données pour cet enregistrement de congé.
        public int Id { get; set; }

        // Le nom d'affichage du congé ou de l'événement.
        // Exemples : "Conge d'automne (Toussaint)", "Vacances d'ete", "Rentree scolaire".
        // Le nom est également utilisé comme mot-clé pour distinguer les types de congés des marqueurs "Rentrée".
        public string Name { get; set; } = string.Empty;

        // Le premier jour de la période de congé (inclus).
        public DateTime StartDate { get; set; }

        // Le dernier jour de la période de congé (inclus).
        public DateTime EndDate { get; set; }

        // Retourne vrai si la date donnée tombe dans cette période de congé (inclus aux deux extrémités).
        // Utilise une comparaison de date uniquement (ignore la composante horaire) pour éviter les problèmes de fuseau horaire.
        public bool IsDateInHoliday(DateTime date)
        {
            // Comparer uniquement la partie date afin que les valeurs d'heure n'affectent pas le résultat
            return date.Date >= StartDate.Date && date.Date <= EndDate.Date;
        }
    }
}
