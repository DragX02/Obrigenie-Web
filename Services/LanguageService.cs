using Blazored.LocalStorage;

namespace Obrigenie.Services
{
    // Service de localisation minimal pour FR / EN / NL.
    // Enregistré Scoped dans Program.cs (= singleton en WASM).
    // Les composants s'abonnent à OnChange pour se re-rendre quand la langue change.
    public class LanguageService
    {
        private readonly ILocalStorageService _localStorage;
        private string _lang = "FR";
        private bool _initialized = false;

        // Déclenché après chaque appel à SetAsync; les composants font StateHasChanged.
        public event Action? OnChange;

        public string Current => _lang;

        public LanguageService(ILocalStorageService localStorage)
        {
            _localStorage = localStorage;
        }

        // Charge la langue depuis localStorage. Idempotent : ne relit pas si déjà fait.
        public async Task InitAsync()
        {
            if (_initialized) return;
            var saved = await _localStorage.GetItemAsStringAsync("lang");
            _lang = saved is "FR" or "EN" or "NL" ? saved : "FR";
            _initialized = true;
        }

        // Change la langue, persiste dans localStorage et notifie les abonnés.
        public async Task SetAsync(string lang)
        {
            if (lang is not ("FR" or "EN" or "NL")) return;
            _lang = lang;
            await _localStorage.SetItemAsStringAsync("lang", lang);
            OnChange?.Invoke();
        }

        // Retourne la traduction pour une clé; retourne la clé si absente.
        public string T(string key) =>
            _translations.TryGetValue(key, out var d) && d.TryGetValue(_lang, out var v) ? v : key;

        private static readonly Dictionary<string, Dictionary<string, string>> _translations = new()
        {
            // Navigation
            ["nav.calendar"]    = new() { ["FR"] = "Agenda",        ["EN"] = "Calendar",     ["NL"] = "Kalender"       },
            ["nav.referents"]   = new() { ["FR"] = "Référentiels",  ["EN"] = "References",   ["NL"] = "Referenties"    },
            ["nav.licenses"]    = new() { ["FR"] = "Licences",      ["EN"] = "Licenses",     ["NL"] = "Licenties"      },
            ["nav.data"]        = new() { ["FR"] = "Données",       ["EN"] = "Data",         ["NL"] = "Gegevens"       },
            ["nav.test"]        = new() { ["FR"] = "Test",          ["EN"] = "Test",         ["NL"] = "Test"           },
            // Thème
            ["theme.light"]     = new() { ["FR"] = "Mode clair",    ["EN"] = "Light mode",   ["NL"] = "Lichte modus"   },
            ["theme.dark"]      = new() { ["FR"] = "Mode sombre",   ["EN"] = "Dark mode",    ["NL"] = "Donkere modus"  },
            // Actions
            ["action.logout"]   = new() { ["FR"] = "Déconnexion",   ["EN"] = "Logout",       ["NL"] = "Uitloggen"      },
            ["action.account"]  = new() { ["FR"] = "Mon compte",    ["EN"] = "My account",   ["NL"] = "Mijn account"   },
            // Serveur
            ["server.online"]   = new() { ["FR"] = "Serveur connecté", ["EN"] = "Server connected", ["NL"] = "Server verbonden" },
            ["server.offline"]  = new() { ["FR"] = "Hors ligne",    ["EN"] = "Offline",      ["NL"] = "Offline"        },
            ["server.check"]    = new() { ["FR"] = "Vérification...", ["EN"] = "Checking...", ["NL"] = "Controleren..."  },
            // Compte
            ["account.title"]   = new() { ["FR"] = "Mon compte",    ["EN"] = "My account",   ["NL"] = "Mijn account"   },
            ["account.lang"]    = new() { ["FR"] = "Langue de l'application", ["EN"] = "Application language", ["NL"] = "Taal van de applicatie" },
            ["account.langFR"]  = new() { ["FR"] = "Français",      ["EN"] = "French",       ["NL"] = "Frans"          },
            ["account.langEN"]  = new() { ["FR"] = "Anglais",       ["EN"] = "English",      ["NL"] = "Engels"         },
            ["account.langNL"]  = new() { ["FR"] = "Néerlandais",   ["EN"] = "Dutch",        ["NL"] = "Nederlands"     },
            ["account.email"]   = new() { ["FR"] = "Adresse e-mail", ["EN"] = "Email address", ["NL"] = "E-mailadres"  },
            ["account.saved"]   = new() { ["FR"] = "Langue enregistrée", ["EN"] = "Language saved", ["NL"] = "Taal opgeslagen" },
            ["account.back"]    = new() { ["FR"] = "Retour",        ["EN"] = "Back",         ["NL"] = "Terug"          },
        };
    }
}
