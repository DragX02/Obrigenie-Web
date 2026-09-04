using System.Text;
using Obrigenie.Components;
using Obrigenie.Models;
using Obrigenie.Services;

namespace ObrigenieTest;

/// <summary>
/// Tests de la section Compétences d'une préparation de leçon.
///
/// Cette section n'est pas saisie à la main : elle est composée par la sélection
/// en cascade, figée au moment de l'enregistrement, puis imprimée telle quelle.
/// Sa forme est donc un contrat entre trois endroits — l'écran de la note, celui
/// de la leçon, et le PDF — et c'est elle que ces tests verrouillent.
/// </summary>
public class LeconContexteTests
{
    // La sélection de l'exemple de référence : une leçon de français de 1re primaire,
    // avec deux visées sous la compétence « Compétences ».
    private static CascadeSelector.CascadeSelection Exemple() => new()
    {
        NomNiveau     = "1ère primaire",
        NomCours      = "Français",
        NomDomaine    = "Orienter sa prise de parole, son écoute, sa lecture, son écrit",
        NomCompetence = "Compétences",
        IdVisees      = new List<int> { 12 },
        NomVisees     = new List<string>
        {
            "Déterminer un but d'écoute selon l'intention précisée et le support utilisé",
            "Utiliser les termes du support de lecture comme indices pour anticiper le contenu d'un document",
        },
    };

    [Fact]
    public void EnTexte_RendUneLigneParNiveauRenseigne()
    {
        var lignes = Exemple().EnTexte().Split('\n');

        Assert.Equal("Année : 1ère primaire", lignes[0]);
        Assert.Equal("Cours : Français", lignes[1]);
        Assert.Equal("Champ : Orienter sa prise de parole, son écoute, sa lecture, son écrit", lignes[2]);
    }

    [Fact]
    public void EnTexte_LaCompetenceIntituleLeBlocDeSesVisees()
    {
        var texte = Exemple().EnTexte();

        // La compétence choisie remplace le libellé générique « Visée » : la fiche
        // porte « Compétences : … », pas « Visée : … ».
        Assert.Contains("Compétences : Déterminer un but d'écoute", texte);
        Assert.DoesNotContain("Visée :", texte);
    }

    [Fact]
    public void EnTexte_AligneLesViseesSuivantesSousLaPremiere()
    {
        var lignes = Exemple().EnTexte().Split('\n');

        // La deuxième visée n'a pas de libellé : elle s'aligne sous la première,
        // à l'aplomb du texte et non de « Compétences : ». L'indentation est
        // conservée à l'écran comme à l'impression.
        var marge = new string(' ', "Compétences : ".Length);

        Assert.StartsWith(marge + "Utiliser les termes", lignes[^1]);
        Assert.Equal("Compétences : Déterminer un but d'écoute selon l'intention précisée et le support utilisé",
                     lignes[^2]);
    }

    [Fact]
    public void EnTexte_SansCompetenceChoisie_RetombeSurLeLibelleGenerique()
    {
        var selection = Exemple();
        selection.NomCompetence = null;

        Assert.Contains("Visée : Déterminer un but d'écoute", selection.EnTexte());
    }

    [Fact]
    public void EnTexte_OmetLesNiveauxNonRenseignes()
    {
        var selection = Exemple();
        selection.NomSousDomaine = null;
        selection.Langue         = null;

        var texte = selection.EnTexte();

        // Un champ vide ne laisse pas une ligne « Domaine : » orpheline sur la fiche
        Assert.DoesNotContain("Domaine :", texte);
        Assert.DoesNotContain("Langue :", texte);
    }

    [Fact]
    public void EnTexte_SelectionVide_NeRendRien()
    {
        Assert.Equal(string.Empty, new CascadeSelector.CascadeSelection().EnTexte());
    }

    [Fact]
    public void ADesVisees_SuitLaPresenceDUneViseeRetenue()
    {
        Assert.True(Exemple().ADesVisees);
        Assert.False(new CascadeSelector.CascadeSelection().ADesVisees);

        // Une entrée choisie hors du référentiel du champ n'a pas encore
        // d'identifiant : son seul intitulé suffit à la considérer retenue.
        var sansId = new CascadeSelector.CascadeSelection { NomVisees = new List<string> { "Nouvelle visée" } };
        Assert.True(sansId.ADesVisees);
    }
}

/// <summary>
/// Tests de l'export PDF d'une préparation de leçon.
///
/// La fiche imprimée est la raison d'être de l'écran : ces tests vérifient qu'elle
/// se construit, qu'elle porte bien le contenu saisi, et qu'une fiche à peine
/// commencée sort quand même — elle sert alors de support à remplir à la main.
/// </summary>
public class LeconPdfExporterTests
{
    private static Lecon Exemple() => new()
    {
        Titre         = "Écouter pour comprendre",
        Enseignant    = "J. Olbrechts",
        Duree         = "50 min",
        NombreSeances = 2,
        Niveaux       = "1ère primaire",
        Competences   = "Année : 1ère primaire\nCours : Français\nCompétences : Déterminer un but d'écoute",
        Phases = new List<LeconPhase>
        {
            new() { Ordre = 1, Intitule = "Mise en situation : écoute d'un extrait", Temps = "10 min" },
            new() { Ordre = 2, Intitule = "Travail par deux sur le support", Temps = "25 min" },
        },
    };

    // Le flux de contenu d'un PDF écrit ses chaînes en WinAnsi, un octet par
    // caractère : c'est donc en Latin1 qu'on y cherche un texte.
    private static string Contenu(byte[] pdf) => Encoding.Latin1.GetString(pdf);

    [Fact]
    public void Generer_ProduitUnPdfValide()
    {
        var octets = LeconPdfExporter.Generer(Exemple());

        Assert.StartsWith("%PDF-1.4", Contenu(octets));
        Assert.EndsWith("%%EOF\n", Contenu(octets));
    }

    [Fact]
    public void Generer_PorteLesLibellesDuFormulairePapier()
    {
        var contenu = Contenu(LeconPdfExporter.Generer(Exemple()));

        // Les intitulés de la fiche papier, dans l'ordre où elle les présente
        Assert.Contains("Titre de la le", contenu);
        Assert.Contains("Enseignant :", contenu);
        Assert.Contains("Nombre de s", contenu);
        Assert.Contains("Niveaux :", contenu);
        Assert.Contains("Compétences :", contenu);
        Assert.Contains("roulement de la le", contenu);
    }

    [Fact]
    public void Generer_PorteLeContenuSaisi()
    {
        var contenu = Contenu(LeconPdfExporter.Generer(Exemple()));

        // Le titre est long pour sa colonne : il se coupe en deux lignes, chacune
        // écrite séparément dans le flux. On cherche donc ses morceaux, pas la
        // chaîne entière.
        Assert.Contains("couter pour", contenu);
        Assert.Contains("comprendre", contenu);
        Assert.Contains("Olbrechts", contenu);
        Assert.Contains("50 min", contenu);

        // Chaque phase porte son numéro, son intitulé et son temps
        Assert.Contains("Phase 1 :", contenu);
        Assert.Contains("Phase 2 :", contenu);
        Assert.Contains("Mise en situation", contenu);
        Assert.Contains("25 min", contenu);
    }

    [Fact]
    public void Generer_ContexteDeCascade_GardeSesLignes()
    {
        var contenu = Contenu(LeconPdfExporter.Generer(Exemple()));

        // Le contexte composé par la cascade s'imprime ligne par ligne sous
        // « Compétences : », et non aplati sur une seule.
        Assert.Contains("Cours : Fran", contenu);
        Assert.Contains("terminer un but d'", contenu);
    }

    [Fact]
    public void Generer_FicheVierge_SortQuandMeme()
    {
        // Une préparation à peine commencée doit pouvoir s'imprimer : la feuille
        // sert alors de support à remplir à la main.
        var vierge = new Lecon
        {
            Titre  = "À préparer",
            Phases = new List<LeconPhase> { new() { Ordre = 1 } },
        };

        var octets = LeconPdfExporter.Generer(vierge);

        Assert.StartsWith("%PDF-1.4", Contenu(octets));
        Assert.Contains("Phase 1 :", Contenu(octets));
    }

    [Fact]
    public void Generer_BeaucoupDePhases_PasseALaPageSuivante()
    {
        var longue = Exemple();
        longue.Phases = Enumerable.Range(1, 40)
            .Select(n => new LeconPhase { Ordre = n, Intitule = $"Phase numéro {n}", Temps = "5 min" })
            .ToList();

        var contenu = Contenu(LeconPdfExporter.Generer(longue));

        // Le document compte plusieurs pages plutôt que de laisser le texte
        // déborder hors de la feuille
        Assert.Contains("/Count 2", contenu);
        Assert.Contains("Phase 40 :", contenu);
    }

    [Theory]
    // Les accents sont conservés : ils sont valides dans un nom de fichier
    [InlineData("Écouter pour comprendre", "Preparation - Écouter pour comprendre.pdf")]
    // Les caractères interdits dans un nom de fichier sont remplacés
    [InlineData("Fractions 1/2 : suite", "Preparation - Fractions 1-2 - suite.pdf")]
    // Une fiche sans titre garde un nom utilisable
    [InlineData("", "Preparation - lecon.pdf")]
    public void NomFichier_EstUtilisableParLeSysteme(string titre, string attendu)
    {
        Assert.Equal(attendu, LeconPdfExporter.NomFichier(new Lecon { Titre = titre }));
    }
}
