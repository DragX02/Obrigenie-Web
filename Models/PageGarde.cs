namespace Obrigenie.Models
{
    public class PageGarde
    {
        public int Id { get; set; }
        public string Nom { get; set; } = "";
        public string Contenu { get; set; } = "";
        public bool PourTous { get; set; }
        public bool EstAMoi { get; set; }
        public bool Modifiable { get; set; }
        public DateTime ModifiedAt { get; set; }
    }

    public class PageGardeDonnees
    {
        public string Orientation { get; set; } = "portrait";
        public bool AvecCadre { get; set; } = false;
        public string TypeLigne { get; set; } = "solid";
        public string Couleur { get; set; } = "#333333";
        public int Epaisseur { get; set; } = 4;
        public int Marge { get; set; } = 20;
        public string Symbole { get; set; } = "⭐";
        public List<ZoneTexte> Textes { get; set; } = new List<ZoneTexte>();
        public List<ZoneImage> Images { get; set; } = new List<ZoneImage>();

        public int Largeur()
        {
            if (Orientation == "paysage")
                return 594;
            return 420;
        }

        public int Hauteur()
        {
            if (Orientation == "paysage")
                return 420;
            return 594;
        }
    }

    public class ZoneTexte
    {
        public string Texte { get; set; } = "";
        public string Police { get; set; } = "Arial";
        public int Taille { get; set; } = 18;
        public string Couleur { get; set; } = "#222222";
        public bool Gras { get; set; } = false;
        public bool Italique { get; set; } = false;
        public double X { get; set; }
        public double Y { get; set; }
    }

    public class ZoneImage
    {
        public string Source { get; set; } = "";
        public double X { get; set; }
        public double Y { get; set; }
        public double Largeur { get; set; } = 150;
        public double Ratio { get; set; } = 1;
        public double Rotation { get; set; } = 0;
    }

    public class ImageGarde
    {
        public string Source { get; set; } = "";
        public string Nom { get; set; } = "";
        public string Categorie { get; set; } = "";
        public bool Perso { get; set; }
    }

    public class TailleImage
    {
        public int Largeur { get; set; }
        public int Hauteur { get; set; }
        public string DataUrl { get; set; } = "";
    }

    public class DebutDeplacement
    {
        public ZoneTexte? Zone { get; set; }
        public ZoneImage? Image { get; set; }
        public string Mode { get; set; } = "deplacer";
        public double ClientX { get; set; }
        public double ClientY { get; set; }
    }
}
