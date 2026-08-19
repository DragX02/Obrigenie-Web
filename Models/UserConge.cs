namespace Obrigenie.Models
{
    // Correction personnelle du calendrier des congés scolaires.
    //
    // Le calendrier officiel (api/values) est commun à tous et alimenté automatiquement ;
    // certaines dates y sont inexactes. Chaque utilisateur enregistre ses propres
    // corrections, appliquées uniquement à son calendrier :
    //   - IdCalendrierFk renseigné + Masque = false → remplace les dates/le nom du congé officiel
    //   - IdCalendrierFk renseigné + Masque = true  → masque complètement le congé officiel
    //   - IdCalendrierFk null                       → congé ajouté par l'utilisateur
    public class UserConge
    {
        // L'identifiant attribué par le serveur (0 pour une correction pas encore enregistrée)
        public int Id { get; set; }

        // Identifiant du congé officiel corrigé, ou null pour un congé ajouté de toutes pièces
        public int? IdCalendrierFk { get; set; }

        // Nom affiché du congé ; 100 caractères maximum côté serveur
        public string Nom { get; set; } = string.Empty;

        // Premier jour de la période (inclus)
        public DateTime DateDebut { get; set; }

        // Dernier jour de la période (inclus)
        public DateTime DateFin { get; set; }

        // Vrai lorsque le congé officiel doit simplement disparaître du calendrier
        public bool Masque { get; set; }
    }
}
