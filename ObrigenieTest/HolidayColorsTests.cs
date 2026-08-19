using Obrigenie.Services;

namespace ObrigenieTest;

/// <summary>
/// Tests for <see cref="HolidayColors"/>.
///
/// Every view — month cells, week columns, the day banner, the trimester table and
/// the PDF exports — colours a holiday from its name alone. The mapping therefore has
/// to be stable (the same holiday keeps its colour across views, reloads and browsers)
/// and tolerant of the naming variants found in the calendar table.
/// </summary>
public class HolidayColorsTests
{
    [Theory]
    [InlineData("Vacances d'hiver (Noël)")]
    [InlineData("Vacances d'hiver (Noel)")]
    [InlineData("VACANCES D'HIVER (NOËL)")]
    public void Pour_MemeConge_MemeCouleurQuelleQueSoitLEcriture(string nom)
    {
        // Accents et casse varient selon la source des données : la couleur, non.
        Assert.Equal(HolidayColors.Pour("Noel"), HolidayColors.Pour(nom));
    }

    [Fact]
    public void Pour_CongesDifferents_CouleursDifferentes()
    {
        // L'intérêt de la couleur est de distinguer les congés d'un coup d'œil.
        Assert.NotEqual(HolidayColors.Pour("Conge d'automne (Toussaint)"),
                        HolidayColors.Pour("Vacances d'hiver (Noel)"));
    }

    [Fact]
    public void Pour_NomInconnu_ResteStable()
    {
        // Un congé ajouté par l'utilisateur n'a pas de mot-clé connu : sa couleur vient
        // d'une empreinte du nom, qui doit donner le même résultat à chaque appel.
        var premiere = HolidayColors.Pour("Journee sportive de l'ecole");

        Assert.Equal(premiere, HolidayColors.Pour("Journee sportive de l'ecole"));
        Assert.Matches("^#[0-9A-Fa-f]{6}$", premiere);
    }

    [Fact]
    public void Pour_NomVide_RetourneUneCouleurValide()
    {
        Assert.Matches("^#[0-9A-Fa-f]{6}$", HolidayColors.Pour(null));
        Assert.Matches("^#[0-9A-Fa-f]{6}$", HolidayColors.Pour(""));
    }

    [Fact]
    public void Fond_AjouteLaTransparence()
    {
        // Le fond des cellules réutilise la couleur du texte en version translucide,
        // pour rester lisible sur le thème clair comme sur le thème sombre.
        var couleur = HolidayColors.Pour("Vacances d'hiver (Noel)");

        Assert.Equal(couleur + "2E", HolidayColors.Fond("Vacances d'hiver (Noel)"));
        Assert.Equal(couleur + "33", HolidayColors.Fond("Vacances d'hiver (Noel)", "33"));
    }

    [Fact]
    public void VersPdf_ConvertitEnComposantesNormalisees()
    {
        // Le flux PDF attend trois nombres entre 0 et 1 séparés par des espaces,
        // avec un point décimal quelle que soit la culture du navigateur.
        var pdf = HolidayColors.VersPdf("Rentree scolaire");   // #2E7D32

        Assert.Equal("0.18 0.49 0.2", pdf);
    }

    [Fact]
    public void VersPdf_TousLesCongesDonnentTroisComposantesValides()
    {
        foreach (var nom in new[] { "Toussaint", "Noel", "Carnaval", "Paques", "Ete", "Inconnu" })
        {
            var composantes = HolidayColors.VersPdf(nom).Split(' ');

            Assert.Equal(3, composantes.Length);
            Assert.All(composantes, c =>
            {
                var valeur = float.Parse(c, System.Globalization.CultureInfo.InvariantCulture);
                Assert.InRange(valeur, 0f, 1f);
            });
        }
    }

    [Theory]
    [InlineData("Rentrée", "Rentree")]
    [InlineData("Pâques", "Paques")]
    [InlineData("Noël", "Noel")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void SansAccents_RamenneAuxLettresDeBase(string? entree, string attendu)
    {
        Assert.Equal(attendu, TexteUtil.SansAccents(entree));
    }
}
