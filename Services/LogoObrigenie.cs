namespace Obrigenie.Services
{
    // Logo Obrigenie embarqué pour les exports PDF.
    //
    // Le PDF sait afficher un JPEG tel quel (filtre DCTDecode) : les octets sont
    // recopiés sans décodage, ce qui évite d'embarquer un décodeur PNG dans le wasm.
    // L'image est donc conservée ici en base64 plutôt que chargée depuis wwwroot,
    // pour qu'un export n'ait aucune requête réseau à faire.
    //
    // Source : wwwroot/icon-192.png, aplati sur fond blanc et réduit à 64x64.
    public static class LogoObrigenie
    {
        // Largeur et hauteur de l'image en pixels
        public const int Largeur = 64;
        public const int Hauteur = 64;

        private const string Base64 =
          "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";

        // Octets JPEG du logo, décodés à la première utilisation.
        public static byte[] Jpeg { get; } = Convert.FromBase64String(Base64);
    }
}
