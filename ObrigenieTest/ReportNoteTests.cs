using Obrigenie.Models;
using Obrigenie.Services;

namespace ObrigenieTest;

/// <summary>
/// Tests for <see cref="ReportNote"/>.
///
/// Reporter une leçon la recopie sur une autre date et laisse sur l'originale la
/// mention « Reporté au … ». Faute de colonne dédiée en base, cette mention vit
/// dans le texte de la note : elle doit disparaître du texte partout où il est
/// affiché ou remis dans la zone de saisie, sans quoi l'utilisateur verrait le
/// marqueur brut et finirait par l'effacer à la première modification.
///
/// Le client ne fait que LIRE le marqueur : son écriture appartient au serveur, et
/// c'est seragendaTest/ReportNoteTests qui la couvre. Les marqueurs employés ici
/// sont donc écrits littéralement — c'est aussi ce qui vérifie que les deux côtés
/// s'accordent sur la forme exacte stockée en base.
/// </summary>
public class ReportNoteTests
{
    private static readonly DateTime Cible = new(2025, 10, 6);

    // Forme exacte du marqueur tel que le serveur l'écrit dans le texte de la note
    private const string Marqueur = "↪ Reporté au 06/10/2025";

    [Fact]
    public void Lire_SansMarqueur_RendLeTexteIntact()
    {
        var (texte, cible) = ReportNote.Lire("Lecture suivie chapitre 3");

        Assert.Equal("Lecture suivie chapitre 3", texte);
        Assert.Null(cible);
    }

    [Fact]
    public void Lire_ContenuVide_NeCasseRien()
    {
        Assert.Equal((string.Empty, null), ReportNote.Lire(null));
        Assert.Equal((string.Empty, null), ReportNote.Lire(""));
    }

    [Fact]
    public void Lire_AvecMarqueur_SepareLeTexteEtLaDate()
    {
        var (texte, cible) = ReportNote.Lire($"Lecture suivie chapitre 3\n{Marqueur}");

        // Le texte revient tel qu'il a été saisi : c'est lui qu'on remet dans la zone de saisie.
        Assert.Equal("Lecture suivie chapitre 3", texte);
        Assert.Equal(Cible, cible);
    }

    [Fact]
    public void Lire_MarqueurSeul_NeRendAucunTexte()
    {
        // Une leçon peut n'avoir qu'une visée : la mention ne doit pas laisser de ligne vide.
        Assert.Equal(Marqueur, ReportNote.Libelle(Cible));
        Assert.Equal(Cible, ReportNote.Cible(Marqueur));
        Assert.Equal(string.Empty, ReportNote.Texte(Marqueur));
    }

    [Fact]
    public void Lire_MarqueurSansAccent_EstQuandMemeReconnu()
    {
        // Selon le trajet du texte, les accents peuvent se perdre : la relecture doit tenir.
        var (texte, cible) = ReportNote.Lire("Dictée\n↪ Reporte au 06/10/2025");

        Assert.Equal("Dictée", texte);
        Assert.Equal(Cible, cible);
    }

    [Fact]
    public void Lire_LigneRessemblanteMaisDateIllisible_ResteDuTexte()
    {
        // Une phrase de l'utilisateur ne doit pas disparaître parce qu'elle commence pareil.
        var contenu = "Reporté au prochain cours de gym";
        var (texte, cible) = ReportNote.Lire(contenu);

        Assert.Equal(contenu, texte);
        Assert.Null(cible);
    }

    [Fact]
    public void Copier_ReprendHoraireEtViseeSurLaNouvelleDate()
    {
        var source = new Note
        {
            Id            = 42,
            Date          = new DateTime(2025, 9, 29),
            Hour          = 10, Minute = 15,
            EndHour       = 11, EndMinute = 45,
            Content       = $"Dictée préparée\n{Marqueur}",
            IdViseeFk     = 7,
            ViseeContexte = "Cours : Français",
        };

        var copie = ReportNote.Copier(source, Cible);

        // La copie est une nouvelle note : sans identifiant, elle est créée au lieu d'écraser l'originale.
        Assert.Equal(0, copie.Id);
        Assert.Equal(Cible.Date, copie.Date.Date);
        Assert.Equal(DateTimeKind.Utc, copie.Date.Kind);

        // Tout ce qui évite de retaper la leçon est repris tel quel.
        Assert.Equal(10, copie.Hour);
        Assert.Equal(15, copie.Minute);
        Assert.Equal(11, copie.EndHour);
        Assert.Equal(45, copie.EndMinute);
        Assert.Equal(7, copie.IdViseeFk);
        Assert.Equal("Cours : Français", copie.ViseeContexte);

        // La copie est la leçon reportée : elle ne porte pas la mention de report.
        Assert.Equal("Dictée préparée", copie.Content);
        Assert.Null(ReportNote.Cible(copie.Content));
    }

    [Fact]
    public void Copier_NeModifiePasLaNoteDOrigine()
    {
        var source = new Note { Id = 42, Date = new DateTime(2025, 9, 29), Hour = 9, Content = "Calcul mental" };

        ReportNote.Copier(source, Cible);

        Assert.Equal(42, source.Id);
        Assert.Equal(new DateTime(2025, 9, 29), source.Date);
        Assert.Equal("Calcul mental", source.Content);
    }

    [Fact]
    public void DateCopie_PorteeJournee_MeneDroitALaDateChoisie()
    {
        // Copier une leçon ou une journée prend le jour lui-même comme référence :
        // la leçon atterrit exactement sur la date cochée, quel que soit son jour de semaine.
        var note = new Note { Date = new DateTime(2025, 9, 29) };   // lundi

        var arrivee = ReportNote.DateCopie(note, new DateTime(2025, 9, 29), new DateTime(2025, 10, 9));

        Assert.Equal(new DateTime(2025, 10, 9), arrivee);           // jeudi
    }

    [Theory]
    // Une semaine copiee trois semaines plus loin : chaque lecon garde son jour de semaine.
    [InlineData("2025-09-29", "2025-10-20")]   // lundi   -> lundi
    [InlineData("2025-10-01", "2025-10-22")]   // mercredi -> mercredi
    [InlineData("2025-10-03", "2025-10-24")]   // vendredi -> vendredi
    public void DateCopie_PorteeSemaine_ConserveLeJourDeSemaine(string depart, string arrivee)
    {
        var lundiSource = new DateTime(2025, 9, 29);
        var lundiCible  = new DateTime(2025, 10, 20);
        var note = new Note { Date = DateTime.Parse(depart) };

        var obtenue = ReportNote.DateCopie(note, lundiSource, lundiCible);

        Assert.Equal(DateTime.Parse(arrivee), obtenue);
        Assert.Equal(note.Date.DayOfWeek, obtenue.DayOfWeek);
    }

    [Fact]
    public void DateCopie_IgnoreLHeureDesDates()
    {
        // Les dates transitent avec une composante horaire selon leur provenance :
        // elle ne doit jamais faire glisser une copie sur le jour voisin.
        var note = new Note { Date = new DateTime(2025, 9, 29, 23, 30, 0) };

        var arrivee = ReportNote.DateCopie(note,
            new DateTime(2025, 9, 29, 22, 0, 0),
            new DateTime(2025, 10, 6, 1, 0, 0));

        Assert.Equal(new DateTime(2025, 10, 6), arrivee);
    }

    [Fact]
    public void Infobulle_SuitLaPresenceDuMarqueur()
    {
        var reportee = new Note { Content = $"Dictée\n{Marqueur}" };
        var ordinaire = new Note { Content = "Dictée" };

        Assert.Equal("Reporté au 06/10/2025", ReportNote.Infobulle(reportee));
        Assert.Null(ReportNote.Infobulle(ordinaire));
    }
}
