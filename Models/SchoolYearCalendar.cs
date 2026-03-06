namespace Obrigenie.Models
{
    // Représente l'ensemble des données du calendrier scolaire utilisées dans toute l'application.
    // Cet objet est le résultat de niveau supérieur retourné par CalendarService et contient :
    //   - la date de rentrée scolaire (utilisée pour la numérotation des semaines scolaires)
    //   - la liste complète des périodes de congé pour l'année scolaire courante et la suivante
    // Il est mis en cache dans le composant de la page Index et transmis à SchoolPeriodHelper pour
    // les calculs de limites de période et la génération d'étiquettes de période.
    public class SchoolYearCalendar
    {
        // La date à laquelle commence l'année scolaire courante (la date de "Rentrée").
        // Utilisée comme point d'ancrage (semaine S1) pour l'algorithme de numérotation des semaines scolaires.
        // Lorsque l'API ne retourne aucune entrée de Rentrée pour l'année courante, une date par défaut
        // du 26 août de l'année de début de l'année scolaire courante est utilisée.
        public DateTime SchoolYearStart { get; set; }

        // La liste complète et ordonnée des périodes de congé (et marqueurs de Rentrée) pour toutes
        // les années scolaires couvertes par les données de l'API.
        // Inclut à la fois l'année scolaire courante et la suivante, afin que la navigation au-delà
        // des limites d'année fonctionne sans nécessiter un nouvel appel API.
        public List<Holiday> Holidays { get; set; } = new();
    }
}
