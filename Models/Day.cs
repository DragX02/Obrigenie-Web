namespace Obrigenie.Models
{
    // Représente un seul jour du calendrier avec toutes les données nécessaires pour le rendre dans n'importe quel mode d'affichage.
    // Les instances sont créées par les méthodes d'aide d'Index.razor et renseignées avec les cours et les notes
    // récupérés depuis l'API.
    public class Day
    {
        // La date exacte que cet objet représente.
        public DateTime Date { get; set; }

        // Le nom localisé du jour de la semaine (ex. : "lundi", "mardi") formaté en français.
        // Utilisé comme étiquette d'en-tête dans la vue d'un seul jour.
        public string DayOfWeek { get; set; } = string.Empty;

        // Le numéro du jour du mois sous forme de chaîne (ex. : "1", "15", "31").
        // Affiché dans le coin supérieur gauche de chaque cellule de jour dans les vues en grille.
        public string DayOfMonth { get; set; } = string.Empty;

        // Vrai lorsque ce jour tombe pendant une période de congé scolaire (excluant les marqueurs "Rentrée").
        // Les jours de congé sont rendus avec une classe CSS distincte pour les différencier visuellement.
        public bool IsHoliday { get; set; }

        // Le nom complet du congé qui couvre ce jour (ex. : "Conge d'automne (Toussaint)").
        // Chaîne vide lorsque le jour n'est pas un jour de congé.
        // Utilisé comme source pour la propriété calculée ShortHolidayName.
        public string HolidayName { get; set; } = string.Empty;

        // Vrai lorsque ce jour coïncide avec le marqueur de "Rentrée" scolaire.
        // Ces jours reçoivent une classe CSS spéciale pour les distinguer des jours de congé ordinaires,
        // car l'entrée Rentrée chevauche la fin des vacances d'été dans les données.
        public bool IsSchoolStart { get; set; }

        // La liste des cours programmés pour ce jour, chargée depuis l'API.
        // Renseignée de manière asynchrone après la création du modèle de jour.
        public List<Course> Courses { get; set; } = new();

        // La liste des notes écrites par l'utilisateur pour ce jour, chargée depuis l'API.
        // Renseignée de manière asynchrone, soit par jour soit dans une requête de plage groupée.
        public List<Note> Notes { get; set; } = new();

        // Retourne une version courte et lisible du nom du congé pour l'affichage dans
        // les cellules de jour compactes des vues en grille semaine et mois.
        // Effectue une série de remplacements de chaînes pour raccourcir les noms officiels longs
        // (ex. : "Vacances de printemps (Paques)" → "Paques").
        // Retourne une chaîne vide lorsque le jour n'a pas de nom de congé.
        public string ShortHolidayName
        {
            get
            {
                // Retourner une chaîne vide immédiatement lorsqu'il n'y a pas de nom de congé à raccourcir
                if (string.IsNullOrEmpty(HolidayName)) return string.Empty;

                // Appliquer une chaîne de remplacements du plus long/spécifique au plus court/générique
                return HolidayName
                    .Replace("Vacances d'hiver (Noel)",          "Noel")
                    .Replace("Vacances d'hiver",                  "Hiver")
                    .Replace("Conge d'automne (Toussaint)",       "Toussaint")
                    .Replace("Vacances d'automne (Toussaint)",    "Toussaint")
                    .Replace("Conge de detente (Carnaval)",       "Carnaval")
                    .Replace("Vacances de detente (Carnaval)",    "Carnaval")
                    .Replace("Vacances de printemps (Paques)",    "Paques")
                    .Replace("Vacances de printemps",             "Printemps")
                    .Replace("Les vacances d'ete debutent le",    "Ete")
                    .Replace("Fin de l'annee scolaire",           "Ete")
                    .Replace("Rentree scolaire",                  "Rentree")
                    .Replace("Jour de l'Armistice",               "Armistice")
                    .Replace("Fete de la Communaute francaise",   "Fete CF")
                    .Replace("Fete des morts",                    "Toussaint")
                    .Replace("Lundi de Paques",                   "Paques")
                    .Replace("Jeudi de l'Ascension",              "Ascension")
                    .Replace("Lundi de Pentecote",                "Pentecote")
                    .Replace("Mardi gras",                        "Carnaval");
            }
        }
    }
}
