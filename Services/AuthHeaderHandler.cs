using System.Net.Http.Headers;

namespace Obrigenie.Services
{
    // Un gestionnaire de message HTTP délégant qui attache automatiquement le jeton Bearer JWT
    // à chaque requête HTTP sortante effectuée via le HttpClient nommé "API".
    // Ce gestionnaire est enregistré dans Program.cs via AddHttpMessageHandler et se place dans le
    // pipeline middleware entre le HttpClient et le transport HTTP réel.
    // En conséquence, tous les services qui utilisent le HttpClient injecté (ApiService, CalendarService)
    // auront l'en-tête Authorization ajouté sans aucune gestion manuelle du jeton.
    public class AuthHeaderHandler : DelegatingHandler
    {
        // L'instance AuthService utilisée pour récupérer le jeton JWT stocké dans le stockage local.
        // Injectée via l'injection de dépendances par constructeur.
        private readonly AuthService _authService;

        // Initialise le gestionnaire en stockant une référence au service d'authentification.
        public AuthHeaderHandler(AuthService authService)
        {
            _authService = authService;
        }

        // Intercepte chaque requête HTTP sortante, lit le jeton JWT depuis le stockage local,
        // et l'ajoute en tant qu'en-tête Authorization "Bearer" si un jeton non vide est trouvé.
        // L'exécution continue ensuite vers le gestionnaire suivant dans le pipeline (envoyant finalement
        // la requête au serveur).
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            // Récupérer le jeton JWT qui a été enregistré dans le stockage local après la dernière connexion
            var token = await _authService.GetTokenAsync();

            // Définir l'en-tête Authorization uniquement lorsqu'un jeton non vide est disponible.
            // Les requêtes non authentifiées (ex. : connexion, inscription) passent sans modification.
            if (!string.IsNullOrEmpty(token))
            {
                // Attacher le jeton en utilisant le schéma d'authentification standard "Bearer"
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            // Transmettre la requête (éventuellement modifiée) au gestionnaire suivant et retourner sa réponse
            return await base.SendAsync(request, cancellationToken);
        }
    }
}
