using Blazored.LocalStorage;

namespace Obrigenie.Services
{
    public class LanguageService
    {
        private readonly ILocalStorageService _localStorage;
        private string _lang = "FR";
        private bool _initialized = false;

        public event Action? OnChange;

        public string Current => _lang;

        public LanguageService(ILocalStorageService localStorage)
        {
            _localStorage = localStorage;
        }

        public async Task InitAsync()
        {
            if (_initialized) return;
            var saved = await _localStorage.GetItemAsStringAsync("lang");
            _lang = saved is "FR" or "EN" or "NL" ? saved : "FR";
            _initialized = true;
        }

        public async Task SetAsync(string lang)
        {
            if (lang is not ("FR" or "EN" or "NL")) return;
            _lang = lang;
            await _localStorage.SetItemAsStringAsync("lang", lang);
            OnChange?.Invoke();
        }

        public string T(string key) =>
            _translations.TryGetValue(key, out var d) && d.TryGetValue(_lang, out var v) ? v : key;

        private static readonly Dictionary<string, Dictionary<string, string>> _translations = new()
        {
            ["nav.calendar"]    = new() { ["FR"] = "Agenda",        ["EN"] = "Calendar",     ["NL"] = "Kalender"       },
            ["nav.referents"]   = new() { ["FR"] = "Référentiels",  ["EN"] = "References",   ["NL"] = "Referenties"    },
            ["nav.licenses"]    = new() { ["FR"] = "Licences",      ["EN"] = "Licenses",     ["NL"] = "Licenties"      },
            ["nav.data"]        = new() { ["FR"] = "Données",       ["EN"] = "Data",         ["NL"] = "Gegevens"       },
            ["nav.test"]        = new() { ["FR"] = "Test",          ["EN"] = "Test",         ["NL"] = "Test"           },
            ["theme.light"]     = new() { ["FR"] = "Mode clair",    ["EN"] = "Light mode",   ["NL"] = "Lichte modus"   },
            ["theme.dark"]      = new() { ["FR"] = "Mode sombre",   ["EN"] = "Dark mode",    ["NL"] = "Donkere modus"  },
            ["action.logout"]   = new() { ["FR"] = "Déconnexion",   ["EN"] = "Logout",       ["NL"] = "Uitloggen"      },
            ["action.account"]  = new() { ["FR"] = "Mon compte",    ["EN"] = "My account",   ["NL"] = "Mijn account"   },
            ["server.online"]   = new() { ["FR"] = "Serveur connecté", ["EN"] = "Server connected", ["NL"] = "Server verbonden" },
            ["server.offline"]  = new() { ["FR"] = "Hors ligne",    ["EN"] = "Offline",      ["NL"] = "Offline"        },
            ["server.check"]    = new() { ["FR"] = "Vérification...", ["EN"] = "Checking...", ["NL"] = "Controleren..."  },
            ["account.title"]   = new() { ["FR"] = "Mon compte",    ["EN"] = "My account",   ["NL"] = "Mijn account"   },
            ["account.lang"]    = new() { ["FR"] = "Langue de l'application", ["EN"] = "Application language", ["NL"] = "Taal van de applicatie" },
            ["account.langFR"]  = new() { ["FR"] = "Français",      ["EN"] = "French",       ["NL"] = "Frans"          },
            ["account.langEN"]  = new() { ["FR"] = "Anglais",       ["EN"] = "English",      ["NL"] = "Engels"         },
            ["account.langNL"]  = new() { ["FR"] = "Néerlandais",   ["EN"] = "Dutch",        ["NL"] = "Nederlands"     },
            ["account.email"]   = new() { ["FR"] = "Adresse e-mail", ["EN"] = "Email address", ["NL"] = "E-mailadres"  },
            ["account.saved"]   = new() { ["FR"] = "Langue enregistrée", ["EN"] = "Language saved", ["NL"] = "Taal opgeslagen" },
            ["account.back"]    = new() { ["FR"] = "Retour",        ["EN"] = "Back",         ["NL"] = "Terug"          },
            ["cal.view.day"]    = new() { ["FR"] = "Jour",           ["EN"] = "Day",          ["NL"] = "Dag"            },
            ["cal.view.week"]   = new() { ["FR"] = "Semaine",        ["EN"] = "Week",         ["NL"] = "Week"           },
            ["cal.view.week+"]  = new() { ["FR"] = "Semaine+",       ["EN"] = "Week+",        ["NL"] = "Week+"          },
            ["cal.view.month"]  = new() { ["FR"] = "Mois",           ["EN"] = "Month",        ["NL"] = "Maand"          },
            ["cal.view.period"] = new() { ["FR"] = "Trimestre",      ["EN"] = "Period",       ["NL"] = "Periode"        },
            ["cal.week"]        = new() { ["FR"] = "Semaine",        ["EN"] = "Week",         ["NL"] = "Week"           },
            ["cal.week.from"]   = new() { ["FR"] = "du",             ["EN"] = "from",         ["NL"] = "van"            },
            ["cal.week.to"]     = new() { ["FR"] = "au",             ["EN"] = "to",           ["NL"] = "tot"            },
            ["cal.loading"]     = new() { ["FR"] = "Chargement de la période...", ["EN"] = "Loading period...", ["NL"] = "Periode laden..." },
            ["cal.day.mon"]     = new() { ["FR"] = "Lun",            ["EN"] = "Mon",          ["NL"] = "Ma"             },
            ["cal.day.tue"]     = new() { ["FR"] = "Mar",            ["EN"] = "Tue",          ["NL"] = "Di"             },
            ["cal.day.wed"]     = new() { ["FR"] = "Mer",            ["EN"] = "Wed",          ["NL"] = "Wo"             },
            ["cal.day.thu"]     = new() { ["FR"] = "Jeu",            ["EN"] = "Thu",          ["NL"] = "Do"             },
            ["cal.day.fri"]     = new() { ["FR"] = "Ven",            ["EN"] = "Fri",          ["NL"] = "Vr"             },
        };
    }
}
