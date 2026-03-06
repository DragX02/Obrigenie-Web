using Blazored.LocalStorage;

namespace Obrigenie.Services
{
    // Gère l'état d'authentification pour le client Blazor WebAssembly.
    // Fournit des méthodes pour persister et récupérer le jeton JWT et l'adresse e-mail de l'utilisateur
    // dans le stockage local du navigateur, pour vérifier si l'utilisateur est actuellement connecté,
    // et pour décoder le rôle de l'utilisateur depuis la charge utile JWT sans nécessiter
    // d'aller-retour avec le serveur.
    // Ce service est enregistré en tant que Scoped dans Program.cs et injecté dans les pages et layouts
    // qui doivent réagir à l'état d'authentification.
    public class AuthService
    {
        // Le service Blazored.LocalStorage utilisé pour lire et écrire des valeurs dans le stockage local du navigateur.
        // Injecté via l'injection de dépendances par constructeur.
        private readonly ILocalStorageService _localStorage;

        // La clé de stockage local sous laquelle le jeton Bearer JWT est stocké.
        private const string TokenKey = "jwt_token";

        // La clé de stockage local sous laquelle l'adresse e-mail de l'utilisateur authentifié est stockée.
        private const string EmailKey = "user_email";

        // Initialise le service avec la dépendance de stockage local requise.
        public AuthService(ILocalStorageService localStorage)
        {
            _localStorage = localStorage;
        }

        // Persiste le jeton Bearer JWT dans le stockage local du navigateur afin qu'il survive aux rechargements de page.
        // Appelé immédiatement après la réception d'une réponse de connexion ou d'inscription réussie.
        public async Task SaveTokenAsync(string token)
        {
            // Écrire le jeton en tant que chaîne brute (non sérialisée en JSON) pour rester compact
            await _localStorage.SetItemAsStringAsync(TokenKey, token);
        }

        // Récupère le jeton Bearer JWT stocké depuis le stockage local du navigateur.
        // Retourne null si aucun jeton n'a été enregistré (c'est-à-dire si l'utilisateur n'est pas connecté).
        public async Task<string?> GetTokenAsync()
        {
            return await _localStorage.GetItemAsStringAsync(TokenKey);
        }

        // Persiste l'adresse e-mail de l'utilisateur authentifié dans le stockage local du navigateur.
        // Appelé en même temps que SaveTokenAsync afin que l'e-mail soit disponible pour l'affichage
        // sans nécessiter un appel API séparé.
        public async Task SaveEmailAsync(string email)
        {
            await _localStorage.SetItemAsStringAsync(EmailKey, email);
        }

        // Récupère l'adresse e-mail stockée depuis le stockage local du navigateur.
        // Retourne null si l'e-mail n'a pas été enregistré.
        public async Task<string?> GetEmailAsync()
        {
            return await _localStorage.GetItemAsStringAsync(EmailKey);
        }

        // Supprime à la fois le jeton JWT et l'adresse e-mail stockée du stockage local,
        // déconnectant effectivement l'utilisateur côté client.
        // Appelé par le bouton de déconnexion dans MainLayout et la page AccessCodePage.
        public async Task RemoveTokenAsync()
        {
            // Supprimer le jeton pour que IsLoggedInAsync retourne false lors de la prochaine vérification
            await _localStorage.RemoveItemAsync(TokenKey);
            // Supprimer également l'e-mail mis en cache pour éviter l'affichage de données périmées après reconnexion
            await _localStorage.RemoveItemAsync(EmailKey);
        }

        // Vérifie si l'utilisateur est actuellement authentifié en contrôlant qu'un jeton JWT
        // non vide existe dans le stockage local. Ne valide pas la signature ou l'expiration du jeton.
        public async Task<bool> IsLoggedInAsync()
        {
            var token = await GetTokenAsync();
            // Une chaîne de jeton non nulle et non vide est considérée comme preuve d'une session connectée
            return !string.IsNullOrEmpty(token);
        }

        // Décode la charge utile JWT côté client pour extraire la revendication de rôle de l'utilisateur.
        // Cela évite un aller-retour serveur pour les décisions d'interface basées sur les rôles (ex. : afficher le
        // panneau d'administration). Note : la signature n'est PAS vérifiée côté client ; le serveur
        // valide toujours le jeton à chaque appel API protégé.
        //
        // Le format JWT est trois parties encodées en base64url séparées par des points :
        //   en-tête.charge_utile.signature
        //
        // Le JwtSecurityTokenHandler d'ASP.NET Core mappe ClaimTypes.Role à la clé courte "role"
        // dans la charge utile JSON, ce que cette méthode lit.
        public async Task<string?> GetRoleAsync()
        {
            var token = await GetTokenAsync();

            // Pas de jeton signifie que l'utilisateur n'est pas connecté ; retourner null immédiatement
            if (string.IsNullOrEmpty(token)) return null;

            try
            {
                // Diviser le JWT en ses trois composants séparés par des points
                var parts = token.Split('.');

                // Un JWT valide doit avoir exactement 3 parties ; sinon il est malformé
                if (parts.Length != 3) return null;

                // La deuxième partie (index 1) est la charge utile JSON encodée en Base64url
                var payload = parts[1];

                // Base64url utilise '-' et '_' à la place de '+' et '/' ; le rembourrage '=' peut être supprimé.
                // Rajouter le rembourrage '=' requis pour que Convert.FromBase64String puisse le décoder.
                payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');

                // Décoder les octets base64 et les interpréter comme du JSON UTF-8
                var json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(payload));

                // Analyser le JSON et rechercher la propriété "role"
                using var doc = System.Text.Json.JsonDocument.Parse(json);

                // Le JwtSecurityTokenHandler d'ASP.NET Core mappe ClaimTypes.Role à la clé "role"
                if (doc.RootElement.TryGetProperty("role", out var role))
                    return role.GetString();
            }
            catch
            {
                // Toute erreur de décodage ou d'analyse (base64 invalide, JSON malformé, etc.) est
                // silencieusement ignorée ; la méthode retourne null pour indiquer un rôle inconnu.
            }

            return null;
        }
    }
}
