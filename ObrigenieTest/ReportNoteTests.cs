using Obrigenie.Models;
using Obrigenie.Services;

namespace ObrigenieTest;

/// <summary>
/// Tests for <see cref="ReportNote"/>.
///
/// Reporter une leçon la recopie sur une autre date et laisse sur l'originale la
/// mention « Reporté au … ». Faute de colonne dédiée en base, cette mention vit
/// dans le texte de la note : elle doit donc se réécrire sans jamais s'empiler,
/// se relire à l'identique, et disparaître du texte partout où il est affiché ou
/// remis dans la zone de saisie — sans quoi l'utilisateur verrait le marqueur brut
/// et finirait par l'effacer ou le dupliquer à la première modification.
/// </summary>
public class ReportNoteTests
{
    private static readonly DateTime Cible = new(2025, 10, 6);

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
    public void Marquer_PuisLire_RendLeTexteEtLaDate()
    {
        var marque = ReportNote.Marquer("Lecture suivie chapitre 3", Cible);
        var (texte, cible) = ReportNote.Lire(marque);

        // Le texte revient tel qu'il a été saisi : c'est lui qu'on remet dans la zone de saisie.
        Assert.Equal("Lecture suivie chapitre 3", texte);
        Assert.Equal(Cible, cible);
    }

    [Fact]
    public void Marquer_NoteSansTexte_NeGardeQueLaMention()
    {
        var marque = ReportNote.Marquer("", Cible);

        // Une leçon peut n'avoir qu'une visée : la mention ne doit pas traîner de ligne vide.
        Assert.Equal(ReportNote.Libelle(Cible), marque);
        Assert.Equal(Cible, ReportNote.Cible(marque));
        Assert.Equal(string.Empty, ReportNote.Texte(marque));
    }

    [Fact]
    public void Marquer_DeuxFois_NeLaisseQuUneSeuleMention()
    {
        var seconde = new DateTime(2025, 10, 13);

        var marque = ReportNote.Marquer(ReportNote.Marquer("Dictée", Cible), seconde);

        // Reporter une leçon déjà reportée écrase la mention précédente au lieu de l'empiler.
        Assert.Equal("Dictée", ReportNote.Texte(marque));
        Assert.Equal(seconde, ReportNote.Cible(marque));
        Assert.Equal(1, marque.Split('\n').Count(l => l.Contains("Report")));
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
    public void Marquer_TexteTresLong_LaisseLaPlaceALaMention()
    {
        // Le serveur tronque à 2000 caractères : c'est le texte qui cède, pas la mention,
        // sinon la trace du report serait coupée en base.
        var marque = ReportNote.Marquer(new string('a', 2000), Cible);

        Assert.True(marque.Length <= 2000);
        Assert.Equal(Cible, ReportNote.Cible(marque));
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
            Content       = ReportNote.Marquer("Dictée préparée", Cible),
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
    public void Infobulle_SuitLaPresenceDuMarqueur()
    {
        var reportee = new Note { Content = ReportNote.Marquer("Dictée", Cible) };
        var ordinaire = new Note { Content = "Dictée" };

        Assert.Equal("Reporté au 06/10/2025", ReportNote.Infobulle(reportee));
        Assert.Null(ReportNote.Infobulle(ordinaire));
    }
}
