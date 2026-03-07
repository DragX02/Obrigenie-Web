namespace Obrigenie.Models
{
    // Représente une note horodatée écrite par l'utilisateur pour un jour et une plage horaire spécifiques.
    // Les notes sont affichées dans la grille horaire d'un seul jour et sous forme de petits indicateurs dans les vues semaine/mois.
    // Elles sont persistées sur le serveur et chargées via ApiService.
    public class Note
    {
        // L'identifiant unique attribué par le serveur pour cette note.
        // Une valeur de 0 indique une nouvelle note qui n'a pas encore été enregistrée.
        public int Id { get; set; }

        // La date du calendrier à laquelle appartient cette note.
        // Stockée en UTC à minuit pour éviter les problèmes de décalage de fuseau horaire lors de la sérialisation JSON.
        public DateTime Date { get; set; }

        // L'heure de début de la note (format 24h, 0-23).
        // Détermine dans quelle rangée de la grille horaire la note est placée lorsqu'elle est vue en mode d'un seul jour.
        public int Hour { get; set; }

        // L'heure de fin de la note (borne supérieure exclusive, format 24h, 1-24).
        // Une valeur de 0 signifie que l'heure de fin n'a pas été définie ; l'interface la traite alors comme Heure + 1.
        public int EndHour { get; set; }

        // Le contenu textuel de la note. Maximum 2000 caractères (appliqué par la zone de texte de l'interface).
        public string Content { get; set; } = string.Empty;

        // L'horodatage UTC de la première création de cette note sur le serveur.
        public DateTime CreatedAt { get; set; }

        // L'horodatage UTC de la modification la plus récente de cette note sur le serveur.
        public DateTime ModifiedAt { get; set; }
    }
}
