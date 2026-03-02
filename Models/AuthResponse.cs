namespace Obrigenie.Models
{
    /// <summary>
    /// Represents the response returned by the server after a successful login or registration.
    /// Contains the JWT bearer token and basic user information.
    /// </summary>
    public class AuthResponse
    {
        /// <summary>
        /// The JSON Web Token (JWT) used to authenticate subsequent API requests.
        /// Stored in browser local storage by AuthService.
        /// </summary>
        public string Token { get; set; } = string.Empty;

        /// <summary>
        /// The email address of the authenticated user.
        /// Stored in local storage so it can be displayed in the UI.
        /// </summary>
        public string Email { get; set; } = string.Empty;

        /// <summary>
        /// The last name (nom) of the user. May be null for OAuth accounts that do not provide it.
        /// </summary>
        public string? Nom { get; set; }

        /// <summary>
        /// The first name (prenom) of the user. May be null for OAuth accounts that do not provide it.
        /// </summary>
        public string? Prenom { get; set; }
    }

    /// <summary>
    /// Data Transfer Object sent to the server when a user submits the login form.
    /// Contains the credentials required for email/password authentication.
    /// </summary>
    public class LoginDto
    {
        /// <summary>
        /// The user's email address used as the login identifier.
        /// </summary>
        public string Email { get; set; } = string.Empty;

        /// <summary>
        /// The user's plain-text password. Transmitted over HTTPS; never stored on the client.
        /// </summary>
        public string Password { get; set; } = string.Empty;
    }

    /// <summary>
    /// Data Transfer Object sent to the server when a new user submits the registration form.
    /// Includes all fields required to create a new account.
    /// </summary>
    public class RegisterDto
    {
        /// <summary>
        /// The email address for the new account. Used as the unique login identifier.
        /// </summary>
        public string Email { get; set; } = string.Empty;

        /// <summary>
        /// The desired password. Must meet the minimum security requirements
        /// (at least 6 characters, one uppercase letter, one digit).
        /// </summary>
        public string Password { get; set; } = string.Empty;

        /// <summary>
        /// A second entry of the password to confirm the user did not make a typo.
        /// Must match the Password field before the form is submitted.
        /// </summary>
        public string ConfirmPassword { get; set; } = string.Empty;

        /// <summary>
        /// The user's last name (family name).
        /// </summary>
        public string Nom { get; set; } = string.Empty;

        /// <summary>
        /// The user's first name (given name).
        /// </summary>
        public string Prenom { get; set; } = string.Empty;
    }
}
