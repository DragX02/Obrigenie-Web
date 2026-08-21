using System.Globalization;
using System.Text;

namespace Obrigenie.Services
{
    // ─────────────────────────────────────────────────────────────────────────
    // Générateur PDF minimal (PDF 1.4), sans dépendance externe.
    //
    // Il produit exactement ce dont l'export du calendrier a besoin : du texte
    // Helvetica (normal / gras), des traits et des rectangles, sur des pages A4
    // portrait ou paysage. Le texte reste vectoriel (sélectionnable, net à
    // l'impression), contrairement à une capture d'écran convertie en image.
    //
    // Le repère exposé a son origine en HAUT à gauche (comme le DOM) ; la
    // conversion vers le repère PDF (origine en bas à gauche) est interne.
    // ─────────────────────────────────────────────────────────────────────────
    public sealed class PdfWriter
    {
        // Dimensions d'une page A4 en points PostScript (1 pt = 1/72 pouce)
        private const float A4Court = 595.28f;
        private const float A4Long  = 841.89f;

        // Un flux de contenu par page ; l'index courant est la dernière page ajoutée
        private readonly List<StringBuilder> pages = new();

        // Largeur et hauteur utiles de la page, orientation déjà appliquée
        public float PageWidth  { get; }
        public float PageHeight { get; }

        public PdfWriter(bool landscape)
        {
            PageWidth  = landscape ? A4Long  : A4Court;
            PageHeight = landscape ? A4Court : A4Long;
            pages.Add(new StringBuilder());
        }

        // Flux de contenu de la page en cours d'écriture
        private StringBuilder Page => pages[^1];

        // Ajoute une page vierge et poursuit le dessin dessus.
        public void NewPage() => pages.Add(new StringBuilder());

        // Image JPEG partagée par toutes les pages, ou null quand le document n'en a pas.
        // Le PDF affiche un JPEG sans le décoder (filtre DCTDecode) : ses octets sont
        // recopiés tels quels, ce qui évite d'embarquer un décodeur d'image.
        private byte[]? imageJpeg;
        private int imageLargeurPx;
        private int imageHauteurPx;

        // Dessine le JPEG donné dans le rectangle indiqué, (x, y) étant son coin
        // supérieur gauche. Un seul JPEG par document : c'est tout ce dont l'en-tête
        // a besoin, et la table des objets reste simple.
        public void Image(float x, float y, float largeur, float hauteur,
                          byte[] jpeg, int largeurPx, int hauteurPx)
        {
            imageJpeg      = jpeg;
            imageLargeurPx = largeurPx;
            imageHauteurPx = hauteurPx;

            // L'opérateur cm porte la taille puis le coin inférieur gauche de l'image ;
            // q/Q isolent cette transformation du reste de la page.
            Page.Append($"q {N(largeur)} 0 0 {N(hauteur)} {N(x)} {N(Y(y + hauteur))} cm /Im1 Do Q\n");
        }

        // Convertit une ordonnée "depuis le haut" en ordonnée PDF "depuis le bas".
        private float Y(float y) => PageHeight - y;

        // Formate un nombre pour le flux PDF : point décimal, jamais de virgule.
        private static string N(float v) => v.ToString("0.##", CultureInfo.InvariantCulture);

        // ── Dessin ───────────────────────────────────────────────────────────

        // Écrit une ligne de texte dont (x, y) est le coin supérieur gauche.
        public void Text(float x, float y, float taille, string texte,
                         bool gras = false, string couleur = "0 0 0")
        {
            if (string.IsNullOrEmpty(texte)) return;

            // y est donné au sommet du texte : on descend d'une hauteur de police
            // pour obtenir la ligne de base attendue par l'opérateur Tm.
            var baseLine = Y(y + taille * 0.8f);

            Page.Append($"BT {couleur} rg /{(gras ? "F2" : "F1")} {N(taille)} Tf ")
                .Append($"1 0 0 1 {N(x)} {N(baseLine)} Tm ")
                .Append($"({Echapper(texte)}) Tj ET\n");
        }

        // Trace un trait entre deux points.
        public void Line(float x1, float y1, float x2, float y2,
                         float epaisseur = 0.5f, string couleur = "0.6 0.6 0.6")
        {
            Page.Append($"{couleur} RG {N(epaisseur)} w ")
                .Append($"{N(x1)} {N(Y(y1))} m {N(x2)} {N(Y(y2))} l S\n");
        }

        // Trace le contour d'un rectangle dont (x, y) est le coin supérieur gauche.
        public void Rect(float x, float y, float largeur, float hauteur,
                         float epaisseur = 0.5f, string couleur = "0.6 0.6 0.6")
        {
            Page.Append($"{couleur} RG {N(epaisseur)} w ")
                .Append($"{N(x)} {N(Y(y + hauteur))} {N(largeur)} {N(hauteur)} re S\n");
        }

        // Remplit un rectangle dont (x, y) est le coin supérieur gauche.
        public void FillRect(float x, float y, float largeur, float hauteur, string couleur)
        {
            Page.Append($"{couleur} rg ")
                .Append($"{N(x)} {N(Y(y + hauteur))} {N(largeur)} {N(hauteur)} re f\n");
        }

        // ── Mesure et découpe du texte ───────────────────────────────────────

        // Largeur approchée d'une chaîne en Helvetica. Le PDF n'embarque pas les
        // métriques de la police : 0.5 em par caractère est une moyenne suffisante
        // pour décider des retours à la ligne (le rendu final reste correct même
        // si une ligne est un peu plus courte que la largeur disponible).
        public static float LargeurApprox(string texte, float taille) => texte.Length * taille * 0.5f;

        // Découpe un texte en lignes tenant dans la largeur donnée.
        // Un mot plus long que la largeur est coupé brutalement plutôt que de déborder.
        public static List<string> Decouper(string texte, float taille, float largeurMax)
        {
            var lignes = new List<string>();
            if (string.IsNullOrWhiteSpace(texte)) return lignes;

            // Chaque saut de ligne du texte source est respecté
            foreach (var paragraphe in texte.Replace("\r", "").Split('\n'))
            {
                var courante = "";

                foreach (var mot in paragraphe.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                {
                    var essai = courante.Length == 0 ? mot : courante + " " + mot;

                    if (LargeurApprox(essai, taille) <= largeurMax) { courante = essai; continue; }

                    if (courante.Length > 0) { lignes.Add(courante); courante = ""; }

                    // Mot seul trop long : on le coupe en morceaux de la largeur disponible
                    var reste = mot;
                    while (LargeurApprox(reste, taille) > largeurMax && reste.Length > 1)
                    {
                        int coupe = Math.Max(1, (int)(largeurMax / (taille * 0.5f)));
                        lignes.Add(reste[..Math.Min(coupe, reste.Length)]);
                        reste = reste[Math.Min(coupe, reste.Length)..];
                    }
                    courante = reste;
                }

                lignes.Add(courante);
            }

            return lignes;
        }

        // Tronque un texte à la largeur donnée en ajoutant des points de suspension.
        public static string Tronquer(string texte, float taille, float largeurMax)
        {
            if (string.IsNullOrEmpty(texte) || LargeurApprox(texte, taille) <= largeurMax) return texte;

            int max = Math.Max(1, (int)(largeurMax / (taille * 0.5f)) - 1);
            return texte.Length <= max ? texte : texte[..max] + "...";
        }

        // ── Sérialisation ────────────────────────────────────────────────────

        // Assemble le document complet et retourne ses octets.
        public byte[] Build()
        {
            // Les chaînes du PDF sont écrites en WinAnsi : un octet par caractère.
            var enc = Encoding.Latin1;
            var ms  = new MemoryStream();
            var positions = new Dictionary<int, long>();

            void Ecrire(string s)
            {
                var octets = enc.GetBytes(s);
                ms.Write(octets, 0, octets.Length);
            }

            void Objet(int id, string corps)
            {
                positions[id] = ms.Length;
                Ecrire($"{id} 0 obj\n{corps}\nendobj\n");
            }

            Ecrire("%PDF-1.4\n");

            // Objets 1 à 4 : catalogue, arbre des pages et les deux polices. Vient
            // ensuite l'image si le document en contient une, puis les pages et leurs flux.
            int prochainId = 5;
            int idImage = imageJpeg != null ? prochainId++ : 0;

            var idsPages = new List<int>();
            for (int i = 0; i < pages.Count; i++) { idsPages.Add(prochainId); prochainId += 2; }

            Objet(1, "<< /Type /Catalog /Pages 2 0 R >>");
            Objet(2, $"<< /Type /Pages /Kids [{string.Join(" ", idsPages.Select(id => $"{id} 0 R"))}] /Count {pages.Count} >>");
            Objet(3, "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>");
            Objet(4, "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>");

            // Image : les octets JPEG sont écrits bruts, sans passer par l'encodage texte
            if (imageJpeg != null)
            {
                positions[idImage] = ms.Length;
                Ecrire($"{idImage} 0 obj\n<< /Type /XObject /Subtype /Image /Width {imageLargeurPx} " +
                       $"/Height {imageHauteurPx} /ColorSpace /DeviceRGB /BitsPerComponent 8 " +
                       $"/Filter /DCTDecode /Length {imageJpeg.Length} >>\nstream\n");
                ms.Write(imageJpeg, 0, imageJpeg.Length);
                Ecrire("\nendstream\nendobj\n");
            }

            for (int i = 0; i < pages.Count; i++)
            {
                int idPage    = idsPages[i];
                int idContenu = idPage + 1;
                var contenu   = pages[i].ToString();

                // L'image n'est déclarée dans les ressources que si le document en porte une
                var ressourceImage = imageJpeg != null ? $" /XObject << /Im1 {idImage} 0 R >>" : "";

                Objet(idPage,
                    $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {N(PageWidth)} {N(PageHeight)}] " +
                    $"/Resources << /Font << /F1 3 0 R /F2 4 0 R >>{ressourceImage} >> /Contents {idContenu} 0 R >>");

                positions[idContenu] = ms.Length;
                Ecrire($"{idContenu} 0 obj\n<< /Length {enc.GetByteCount(contenu)} >>\nstream\n{contenu}endstream\nendobj\n");
            }

            // Table des références croisées : position de chaque objet dans le fichier
            int nbObjets  = prochainId - 1;
            long debutXref = ms.Length;

            Ecrire($"xref\n0 {nbObjets + 1}\n0000000000 65535 f \n");
            for (int id = 1; id <= nbObjets; id++)
                Ecrire($"{positions[id]:D10} 00000 n \n");

            Ecrire($"trailer\n<< /Size {nbObjets + 1} /Root 1 0 R >>\nstartxref\n{debutXref}\n%%EOF\n");

            return ms.ToArray();
        }

        // ── Nettoyage du texte ───────────────────────────────────────────────

        // Prépare une chaîne pour un flux PDF : caractères hors WinAnsi remplacés
        // (émojis, flèches, tirets typographiques…) puis parenthèses échappées.
        private static string Echapper(string texte)
        {
            var sb = new StringBuilder(texte.Length);

            foreach (var c in texte)
            {
                switch (c)
                {
                    case '\\': sb.Append("\\\\"); break;
                    case '(':  sb.Append("\\(");  break;
                    case ')':  sb.Append("\\)");  break;
                    case '\n':
                    case '\r':
                    case '\t': sb.Append(' ');    break;

                    // Équivalents ASCII des caractères typographiques courants
                    case '→': sb.Append("->"); break;
                    case '–':
                    case '—': sb.Append('-');  break;
                    case '’':
                    case '‘': sb.Append('\''); break;
                    case '“':
                    case '”': sb.Append('"');  break;
                    case '…': sb.Append("..."); break;
                    case '·': sb.Append('-');  break;

                    default:
                        // WinAnsi couvre le Latin-1 : tout le reste (émojis…) est ignoré
                        if (c >= ' ' && c <= 'ÿ') sb.Append(c);
                        break;
                }
            }

            return sb.ToString();
        }

        // Retire les caractères non imprimables en PDF sans les échapper : utilisé
        // avant de mesurer ou de découper un texte, pour que la largeur estimée
        // corresponde à ce qui sera réellement écrit.
        public static string Nettoyer(string? texte)
        {
            if (string.IsNullOrEmpty(texte)) return string.Empty;

            var sb = new StringBuilder(texte.Length);
            foreach (var c in texte)
            {
                if (c == '\n') { sb.Append('\n'); continue; }
                if (c == '\r' || c == '\t') { sb.Append(' '); continue; }
                if (c == '→') { sb.Append("->"); continue; }
                if (c == '…') { sb.Append("..."); continue; }
                if (c >= ' ' && c <= 'ÿ') sb.Append(c);
            }
            return sb.ToString();
        }
    }
}
