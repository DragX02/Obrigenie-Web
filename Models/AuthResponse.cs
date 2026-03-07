namespace Obrigenie.Models
{
    // Représente la réponse retournée par le serveur après une connexion ou une inscription réussie.
    // Contient le jeton Bearer JWT et les informations de base de l'utilisateur.
    public class AuthResponse
    {
        // Le JSON Web Token (JWT) utilisé pour authentifier les requêtes API suivantes.
        // Stocké dans le stockage local du navigateur par AuthService.
        public string Token { get; set; } = string.Empty;

        // L'adresse e-mail de l'utilisateur authentifié.
        // Stockée dans le stockage local afin de pouvoir être affichée dans l'interface.
        public string Email { get; set; } = string.Empty;

        // Le nom de famille (nom) de l'utilisateur. Peut être null pour les comptes OAuth qui ne le fournissent pas.
        public string? Nom { get; set; }

        // Le prénom (prénom) de l'utilisateur. Peut être null pour les comptes OAuth qui ne le fournissent pas.
        public string? Prenom { get; set; }
    }

    // Objet de transfert de données envoyé au serveur lorsqu'un utilisateur soumet le formulaire de connexion.
    // Contient les identifiants requis pour l'authentification par e-mail et mot de passe.
    public class LoginDto
    {
        // L'adresse e-mail de l'utilisateur utilisée comme identifiant de connexion.
        public string Email { get; set; } = string.Empty;

        // Le mot de passe en clair de l'utilisateur. Transmis via HTTPS ; jamais stocké côté client.
        public string Password { get; set; } = string.Empty;
    }

    // Objet de transfert de données envoyé au serveur lorsqu'un nouvel utilisateur soumet le formulaire d'inscription.
    // Inclut tous les champs nécessaires pour créer un nouveau compte.
    public class RegisterDto
    {
        // L'adresse e-mail du nouveau compte. Utilisée comme identifiant de connexion unique.
        public string Email { get; set; } = string.Empty;

        // Le mot de passe souhaité. Doit satisfaire les exigences minimales de sécurité
        // (au moins 6 caractères, une lettre majuscule, un chiffre).
        public string Password { get; set; } = string.Empty;

        // Une deuxième saisie du mot de passe pour confirmer que l'utilisateur n'a pas fait de faute de frappe.
        // Doit correspondre au champ Password avant la soumission du formulaire.
        public string ConfirmPassword { get; set; } = string.Empty;

        // Le nom de famille de l'utilisateur.
        public string Nom { get; set; } = string.Empty;

        // Le prénom de l'utilisateur.
        public string Prenom { get; set; } = string.Empty;
    }
}
