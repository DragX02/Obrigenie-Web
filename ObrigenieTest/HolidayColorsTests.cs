using Obrigenie.Services;

namespace ObrigenieTest;

public class HolidayColorsTests
{
    [Theory]
    [InlineData("Vacances d'hiver (Noël)")]
    [InlineData("Vacances d'hiver (Noel)")]
    [InlineData("VACANCES D'HIVER (NOËL)")]
    public void Pour_MemeConge_MemeCouleurQuelleQueSoitLEcriture(string nom)
    {
        Assert.Equal(HolidayColors.Pour("Noel"), HolidayColors.Pour(nom));
    }

    [Fact]
    public void Pour_CongesDifferents_CouleursDifferentes()
    {
        Assert.NotEqual(HolidayColors.Pour("Conge d'automne (Toussaint)"),
                        HolidayColors.Pour("Vacances d'hiver (Noel)"));
    }

    [Fact]
    public void Pour_NomInconnu_ResteStable()
    {
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
        var couleur = HolidayColors.Pour("Vacances d'hiver (Noel)");

        Assert.Equal(couleur + "2E", HolidayColors.Fond("Vacances d'hiver (Noel)"));
        Assert.Equal(couleur + "33", HolidayColors.Fond("Vacances d'hiver (Noel)", "33"));
    }

    [Fact]
    public void VersPdf_ConvertitEnComposantesNormalisees()
    {
        var pdf = HolidayColors.VersPdf("Rentree scolaire");

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
