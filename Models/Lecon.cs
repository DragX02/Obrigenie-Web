namespace Obrigenie.Models
{
    // Préparation de leçon, telle que la remplit l'enseignant.
    //
    // Reprend le formulaire papier « Préparation de leçon type » : un en-tête
    // (titre, enseignant, durée, nombre de séances, niveaux), les compétences
    // visées, puis le déroulement découpé en phases.
    //
    // Les préparations sont stockées côté serveur (table lecon) et n'appartiennent
    // qu'à leur auteur. La page /lecons les affiche, les modifie, et les rend en
    // PDF ou à l'imprimante au format du formulaire papier.
    public class Lecon
    {
        // L'identifiant attribué par le serveur (0 pour une préparation pas encore enregistrée)
        public int Id { get; set; }

        // Titre de la leçon. Seul champ obligatoire : c'est lui qui identifie la
        // préparation dans la liste. 200 caractères maximum côté serveur.
        public string Titre { get; set; } = string.Empty;

        // Nom de l'enseignant tel qu'il doit apparaître sur la feuille imprimée.
        // Distinct du compte connecté : une préparation peut être écrite pour un
        // collègue ou un stagiaire. 150 caractères maximum.
        public string Enseignant { get; set; } = string.Empty;

        // Durée de la leçon, en texte libre : « 50 min », « 2 x 50 min »,
        // « une matinée ». 100 caractères maximum.
        public string Duree { get; set; } = string.Empty;

        // Nombre de séances que couvre la préparation (1 par défaut)
        public int NombreSeances { get; set; } = 1;

        // Niveaux concernés, saisis librement : une même leçon peut en viser
        // plusieurs. 200 caractères maximum.
        public string Niveaux { get; set; } = string.Empty;

        // Compétences visées, en texte libre sur plusieurs lignes.
        // 4000 caractères maximum côté serveur.
        public string Competences { get; set; } = string.Empty;

        // Visée du référentiel choisie dans la cascade, ou null quand la section
        // Compétences n'est pas rattachée au référentiel. Le détail lisible vit
        // dans Competences ; cette clé garde le lien réel vers le référentiel.
        public int? IdViseeFk { get; set; }

        // Horodatage UTC de la première création de cette préparation
        public DateTime CreatedAt { get; set; }

        // Horodatage UTC de la dernière modification ; c'est lui qui ordonne la liste
        public DateTime ModifiedAt { get; set; }

        // Les phases du déroulement, dans leur ordre d'affichage
        public List<LeconPhase> Phases { get; set; } = new();

        // Copie indépendante, pour que la fenêtre d'édition travaille sur un
        // brouillon : fermer sans enregistrer doit laisser la liste intacte.
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

    // Une phase du déroulement : « Phase 1 : … Temps : … » sur le formulaire papier.
    public class LeconPhase
    {
        // L'identifiant attribué par le serveur. Les phases sont réécrites en bloc
        // à chaque enregistrement, cet identifiant n'est donc jamais renvoyé tel quel.
        public int Id { get; set; }

        // Rang d'affichage, à partir de 1 : le numéro montré à l'écran et sur la
        // feuille. Le serveur le renumérote à l'enregistrement.
        public int Ordre { get; set; }

        // Ce qui se passe pendant la phase, en texte libre sur plusieurs lignes.
        // 1000 caractères maximum côté serveur.
        public string Intitule { get; set; } = string.Empty;

        // Temps imparti, en texte libre (« 10 min », « 1/4 h »). 50 caractères maximum.
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
