# Obrigenie — Agenda pédagogique pour enseignants

<div align="center">

**Choisir la langue / Choose language / Kies taal**

[🇫🇷 Français](#français) · [🇬🇧 English](#english) · [🇧🇪 Nederlands](#nederlands)

</div>

---

# Français

## Présentation

**Obrigenie** est une application web complète destinée aux enseignants belges pour gérer leur horaire hebdomadaire, leurs notes horodatées et leur planification pédagogique.

- **Frontend :** Blazor WebAssembly (.NET 8)
- **Backend :** API REST ASP.NET Core 8
- **Base de données :** PostgreSQL

## Fonctionnalités

| Fonctionnalité | Description |
|---|---|
| Agenda multi-vues | Journée (horaire 06h–22h), semaine, semaine + week-end, mois, période scolaire |
| Cours récurrents | Planification par jour de semaine (encodage bitmask), plage de dates, couleur personnalisée |
| Notes horodatées | Créneaux horaires 06h–22h, par utilisateur, texte sanitisé contre le XSS |
| Calendrier scolaire | Détection automatique des congés et périodes par scraping du calendrier de la FWB |
| OAuth | Connexion Google et Microsoft en plus de l'email/mot de passe classique |
| Confirmation e-mail | Lien de confirmation valable 24 h pour les nouveaux comptes |
| Gestion des licences | Codes licence hachés SHA-256, assignables par les admins, expiration optionnelle |
| Panel admin | CRUD licences, déclenchement du scraper, gestion utilisateurs |
| Thème clair/sombre | Basculement par bouton, préférence stockée localement |

## Architecture

```
Navigateur
  └── Blazor WASM (Obrigenie)
        ├── AuthHeaderHandler  — ajoute le token JWT Bearer sur chaque requête
        ├── ApiService         — client HTTP typé pour tous les appels REST
        ├── AuthService        — stockage JWT et décodage du rôle côté client
        └── CalendarService    — calendrier scolaire avec cache localStorage

              ↕ HTTPS (terminaison TLS via nginx)

API ASP.NET Core 8 (seragenda)
  ├── AuthController     — login, inscription, OAuth, confirmation e-mail, échange JWT
  ├── CoursesController  — CRUD des cours de l'utilisateur
  ├── NotesController    — CRUD des notes horodatées
  ├── AccessController   — validation et vérification de licence
  ├── AdminController    — opérations réservées aux admins
  ├── RefController      — données de référence (cours / niveaux / domaines)
  └── ViseesMaitriser    — objectifs pédagogiques (paginé)

              ↕ Entity Framework Core

Base de données PostgreSQL
  ├── utilisateur         — comptes utilisateurs
  ├── user_course         — cours récurrents
  ├── user_note           — notes horodatées
  ├── license             — pool de codes licence
  ├── cours               — catalogue des matières
  ├── cours_niveau        — table de liaison matière ↔ niveau ↔ enseignant
  ├── domaine             — domaines pédagogiques par combinaison matière-niveau
  └── calendrier_scolaire — événements de l'année scolaire (scraping FWB)
```

## Démarrage rapide

### Prérequis

- [SDK .NET 8](https://dotnet.microsoft.com/download/dotnet/8.0)
- [PostgreSQL 15+](https://www.postgresql.org/download/)
- Un compte [Brevo](https://www.brevo.com/) pour le relais SMTP

### 1 — Restaurer les paquets

```bash
cd G:/csharpcallender/Obrigenie
dotnet restore Obrigenie.sln

cd G:/csharp/serappagenda/seragenda
dotnet restore seragenda.sln
```

### 2 — Configurer le backend

Créer `seragenda/appsettings.Development.json` (ignoré par git) :

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=seragenda;Username=postgres;Password=votremotdepasse"
  },
  "JwtSettings": {
    "SecretKey": "cle-secrete-minimum-32-caracteres"
  },
  "EmailSettings": {
    "Host": "smtp-relay.brevo.com",
    "Port": "587",
    "Username": "login-smtp-brevo",
    "Password": "cle-smtp-brevo",
    "FromEmail": "noreply@votredomaine.com",
    "FromName": "Obrigenie"
  },
  "GoogleAuth": { "ClientId": "...", "ClientSecret": "..." },
  "MicrosoftAuth": { "ClientId": "...", "ClientSecret": "..." },
  "AppSettings": { "FrontendUrl": "http://localhost:5276" },
  "Cors": { "AllowedOrigins": [ "http://localhost:5276" ] }
}
```

### 3 — Appliquer les migrations

```bash
cd G:/csharp/serappagenda/seragenda/seragenda
dotnet ef database update
```

### 4 — Lancer le backend

```bash
dotnet run --project seragenda
# API disponible sur http://localhost:5276
# Swagger sur http://localhost:5276/swagger (dev uniquement)
```

### 5 — Lancer le frontend

```bash
cd G:/csharpcallender/Obrigenie
dotnet run
```

> En production, le backend sert directement les fichiers statiques Blazor WASM. Un seul processus suffit.

## Référence API

Tous les points d'accès protégés exigent `Authorization: Bearer <jwt>`.

### Authentification

| Méthode | Route | Auth | Description |
|---|---|---|---|
| POST | `/api/auth/login` | Non | Connexion email + mot de passe (limité : 5 req/15 min) |
| POST | `/api/auth/register` | Non | Création de compte + envoi d'e-mail de confirmation |
| GET | `/api/auth/confirm` | Non | Confirmation d'adresse e-mail via token |
| GET | `/api/auth/google` | Non | Démarrer le flux OAuth Google |
| GET | `/api/auth/microsoft` | Non | Démarrer le flux OAuth Microsoft |
| GET | `/api/auth/exchange` | Non | Échanger le cookie `auth_pending` contre un JWT |

### Cours, Notes, Licences

| Méthode | Route | Auth | Description |
|---|---|---|---|
| GET/POST/DELETE | `/api/courses` | JWT | Gestion des cours récurrents |
| GET | `/api/courses/date/{date}` | JWT | Cours d'une date précise |
| GET/POST/DELETE | `/api/notes` | JWT | Gestion des notes horodatées |
| GET | `/api/notes/range` | JWT | Notes sur une plage de dates (max 62 j) |
| POST | `/api/access/validate` | JWT | Valider et assigner un code licence |
| GET | `/api/access/check` | JWT | Vérifier l'état de la licence |

### Données de référence (sélections en cascade)

| Méthode | Route | Auth | Description |
|---|---|---|---|
| GET | `/api/ref/cours` | JWT | Toutes les matières du catalogue |
| GET | `/api/ref/niveaux/{codeCours}` | JWT | Niveaux disponibles pour une matière |
| GET | `/api/ref/domaines/{codeCours}/{codeNiveau}` | JWT | Domaines pour une matière + niveau |

### Admin

| Méthode | Route | Auth | Description |
|---|---|---|---|
| GET/POST/PUT/DELETE | `/api/admin/licenses` | ADMIN | Gestion complète des licences |
| GET | `/api/update-scolaire` | ADMIN | Déclencher le scraper du calendrier scolaire |
| GET | `/api/health` | Non | Vérification de disponibilité |

## Authentification

### JWT

- Signé HMAC-SHA256 avec `JwtSettings:SecretKey`
- Durée de validité : **7 jours** (`ClockSkew = TimeSpan.Zero`)
- Claims : `sub` (id), `email`, `role` (`ADMIN` ou `PROF`), `prenom`
- Stocké dans le **localStorage** du navigateur
- Ajouté automatiquement par `AuthHeaderHandler` à chaque requête HTTP

### Flux OAuth

```
Clic "Se connecter avec Google"
  → GET /api/auth/google  (redirection vers Google)
  → Consentement de l'utilisateur
  → Callback backend → JWT dans cookie HttpOnly auth_pending
  → Redirection vers /auth-callback (frontend)
  → AuthCallback.razor appelle GET /api/auth/exchange
  → JWT retourné → stocké dans localStorage
  → Application rechargée
```

### Limitation de débit

Auth endpoints : **5 requêtes par 15 minutes** par IP → HTTP 429 si dépassé.

## Base de données

### Bitmask jours de semaine

Les cours stockent leurs jours planifiés sous forme d'un entier bitmask :

| Jour | Valeur |
|---|---|
| Lundi | 1 |
| Mardi | 2 |
| Mercredi | 4 |
| Jeudi | 8 |
| Vendredi | 16 |
| Samedi | 32 |
| Dimanche | 64 |

Exemple : Lundi + Mercredi + Vendredi = `1 + 4 + 16 = 21`

## Déploiement

### Construire et publier

```bash
dotnet publish seragenda/seragenda.csproj -c Release -o ./publish
```

### Service systemd

```ini
[Unit]
Description=Obrigenie API
After=network.target

[Service]
WorkingDirectory=/var/www/serapi
ExecStart=/usr/bin/dotnet /var/www/serapi/seragenda.dll
Restart=always
User=www-data
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=http://localhost:5276
```

### nginx

```nginx
location / {
    proxy_pass         http://localhost:5276;
    proxy_set_header   X-Forwarded-For   $proxy_add_x_forwarded_for;
    proxy_set_header   X-Forwarded-Proto $scheme;
    proxy_set_header   Host              $host;
}
```

## Tests

```bash
# Tests unitaires backend (xUnit + Moq + EF Core InMemory)
cd G:/csharp/serappagenda/seragenda
dotnet test

# Tests frontend (bUnit + xUnit + Moq)
cd G:/csharpcallender/Obrigenie
dotnet test ObrigenieTest/ObrigenieTest.csproj
```

| Projet | Tests | Couverture |
|---|---|---|
| Backend (ObriGenieTest) | 34 tests | Flux e-mail, confirmation, OAuth, validation token |
| Frontend (ObrigenieTest) | 43 tests | Modèles, calendrier scolaire, AuthService, ConfirmEmail |

## Sécurité

- `appsettings*.json` exclu du git
- La clé JWT est obligatoire au démarrage (exception si absente)
- Mots de passe hachés avec BCrypt — jamais stockés en clair
- Codes licence stockés en SHA-256 — le texte en clair n'est retourné qu'une seule fois
- Notes HTML-strippées à la sauvegarde (protection XSS)
- Entrées validées et caractères dangereux rejetés avant toute opération base de données

---

# English

## Overview

**Obrigenie** is a full-stack web application for Belgian school teachers to manage their weekly course schedule, timed notes, and curriculum planning.

- **Frontend:** Blazor WebAssembly (.NET 8)
- **Backend:** ASP.NET Core 8 REST API
- **Database:** PostgreSQL

## Features

| Feature | Description |
|---|---|
| Multi-view calendar | Single day (06:00–22:00), week, week + weekend, month, school-period views |
| Recurring courses | Day-of-week bitmask scheduling, date range, custom color |
| Timed notes | Hourly slots 06:00–22:00, per user, XSS-sanitized text |
| School calendar | Automatic holiday and period detection via FWB calendar scraper |
| OAuth login | Google and Microsoft SSO alongside classic email/password |
| Email confirmation | 24-hour confirmation link for new accounts (Brevo SMTP) |
| License management | SHA-256 hashed license codes, admin-assignable, optional expiry |
| Admin panel | License CRUD, scraper trigger, user management |
| Dark / light theme | Toggle button, preference stored in localStorage |

## Architecture

```
Browser
  └── Blazor WASM (Obrigenie)
        ├── AuthHeaderHandler  — attaches JWT Bearer on every request
        ├── ApiService         — typed HTTP wrapper for all REST calls
        ├── AuthService        — JWT storage & client-side role decoding
        └── CalendarService    — school calendar with localStorage cache

              ↕ HTTPS (nginx TLS termination)

ASP.NET Core 8 API (seragenda)
  ├── AuthController     — login, register, OAuth, email confirm, JWT exchange
  ├── CoursesController  — user course schedule CRUD
  ├── NotesController    — user timed notes CRUD
  ├── AccessController   — license validation and status
  ├── AdminController    — admin-only operations
  ├── RefController      — curriculum reference data (cours/niveaux/domaines)
  └── ViseesMaitriser    — educational objectives (paginated)

              ↕ Entity Framework Core

PostgreSQL Database
  ├── utilisateur         — user accounts
  ├── user_course         — recurring course entries
  ├── user_note           — timed notes
  ├── license             — license key pool
  ├── cours               — curriculum subject catalogue
  ├── cours_niveau        — course ↔ level ↔ teacher linking table
  ├── domaine             — pedagogical domains per course-level pair
  └── calendrier_scolaire — scraped school-year events
```

## Quick Start

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [PostgreSQL 15+](https://www.postgresql.org/download/)
- A [Brevo](https://www.brevo.com/) account for SMTP relay

### 1 — Restore packages

```bash
cd G:/csharpcallender/Obrigenie && dotnet restore Obrigenie.sln
cd G:/csharp/serappagenda/seragenda && dotnet restore seragenda.sln
```

### 2 — Configure the backend

Create `seragenda/appsettings.Development.json` (git-ignored):

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=seragenda;Username=postgres;Password=yourpassword"
  },
  "JwtSettings": { "SecretKey": "your-secret-key-min-32-chars" },
  "EmailSettings": {
    "Host": "smtp-relay.brevo.com", "Port": "587",
    "Username": "brevo-login", "Password": "brevo-key",
    "FromEmail": "noreply@yourdomain.com", "FromName": "Obrigenie"
  },
  "GoogleAuth": { "ClientId": "...", "ClientSecret": "..." },
  "MicrosoftAuth": { "ClientId": "...", "ClientSecret": "..." },
  "AppSettings": { "FrontendUrl": "http://localhost:5276" },
  "Cors": { "AllowedOrigins": [ "http://localhost:5276" ] }
}
```

### 3 — Apply migrations

```bash
cd G:/csharp/serappagenda/seragenda/seragenda
dotnet ef database update
```

### 4 — Run

```bash
# Backend (also serves the Blazor WASM files)
dotnet run --project seragenda
# → http://localhost:5276  |  Swagger: http://localhost:5276/swagger (dev only)
```

## API Reference

All protected endpoints require `Authorization: Bearer <jwt>`.

### Authentication

| Method | Route | Auth | Description |
|---|---|---|---|
| POST | `/api/auth/login` | No | Email + password login (rate-limited: 5 req/15 min) |
| POST | `/api/auth/register` | No | Create account + send confirmation email |
| GET | `/api/auth/confirm` | No | Confirm email address via token |
| GET | `/api/auth/google` | No | Start Google OAuth flow |
| GET | `/api/auth/microsoft` | No | Start Microsoft OAuth flow |
| GET | `/api/auth/exchange` | No | Exchange auth_pending cookie for JWT |

### Courses, Notes, Licenses

| Method | Route | Auth | Description |
|---|---|---|---|
| GET/POST/DELETE | `/api/courses` | JWT | Manage recurring courses |
| GET | `/api/courses/date/{date}` | JWT | Courses for a specific date |
| GET/POST/DELETE | `/api/notes` | JWT | Manage timed notes |
| GET | `/api/notes/range` | JWT | Notes in a date range (max 62 days) |
| POST | `/api/access/validate` | JWT | Validate and assign a license code |
| GET | `/api/access/check` | JWT | Check license status |

### Reference Data (Cascading dropdowns)

| Method | Route | Auth | Description |
|---|---|---|---|
| GET | `/api/ref/cours` | JWT | All curriculum subjects |
| GET | `/api/ref/niveaux/{codeCours}` | JWT | Levels available for a subject |
| GET | `/api/ref/domaines/{codeCours}/{codeNiveau}` | JWT | Domains for a subject + level |

### Admin

| Method | Route | Auth | Description |
|---|---|---|---|
| GET/POST/PUT/DELETE | `/api/admin/licenses` | ADMIN | Full license management |
| GET | `/api/update-scolaire` | ADMIN | Trigger school calendar scraper |
| GET | `/api/health` | No | Liveness probe |

## Authentication

### JWT

- Signed with HMAC-SHA256 using `JwtSettings:SecretKey`
- Expiry: **7 days** (`ClockSkew = TimeSpan.Zero`)
- Claims: `sub` (user id), `email`, `role` (`ADMIN` or `PROF`), `prenom`
- Stored in browser **localStorage** — attached automatically by `AuthHeaderHandler`

### Rate Limiting

Auth endpoints: **5 requests per 15 minutes** per client IP → HTTP 429 when exceeded.

## Database — Day-of-Week Bitmask

| Day | Value |
|---|---|
| Monday | 1 |
| Tuesday | 2 |
| Wednesday | 4 |
| Thursday | 8 |
| Friday | 16 |
| Saturday | 32 |
| Sunday | 64 |

Example: Monday + Wednesday + Friday = `1 + 4 + 16 = 21`

## Deployment

```bash
# Build
dotnet publish seragenda/seragenda.csproj -c Release -o ./publish

# Restart service on server
sudo systemctl restart serapi
```

Create `/var/www/serapi/appsettings.Production.json` on the server with production credentials. The Data Protection keys directory must exist: `/var/www/serapi/dataprotection-keys`

## Testing

```bash
dotnet test                                              # backend tests (34)
dotnet test ObrigenieTest/ObrigenieTest.csproj          # frontend tests (43)
```

## Security Notes

- `appsettings*.json` files are git-ignored
- App throws at startup if JWT secret is missing
- Passwords hashed with BCrypt — never stored in plaintext
- License codes stored as SHA-256 hashes — plaintext returned only once at creation
- Notes are HTML-stripped on save (XSS protection)
- Inputs validated and dangerous characters rejected before any database operation

---

# Nederlands

## Overzicht

**Obrigenie** is een volledige webapplicatie voor Belgische leerkrachten om hun wekelijks lesrooster, getimede notities en pedagogische planning te beheren.

- **Frontend:** Blazor WebAssembly (.NET 8)
- **Backend:** ASP.NET Core 8 REST API
- **Database:** PostgreSQL

## Functionaliteiten

| Functionaliteit | Beschrijving |
|---|---|
| Multi-weergave agenda | Dag (06u–22u), week, week + weekend, maand, schoolperiode-weergave |
| Terugkerende lessen | Dag-van-de-week bitmask planning, datumbereik, aangepaste kleur |
| Getimede notities | Uurslots 06u–22u, per gebruiker, XSS-gesaneerde tekst |
| Schoolkalender | Automatische detectie van vakanties en periodes via FWB-kalender scraper |
| OAuth aanmelding | Google en Microsoft SSO naast klassiek e-mail/wachtwoord |
| E-mailbevestiging | 24-uur geldig bevestigingslink voor nieuwe accounts (Brevo SMTP) |
| Licentiebeheer | SHA-256 gehashte licentiecodes, toe te wijzen door admins, optionele vervaldatum |
| Beheerderspaneel | Licentie CRUD, scraper activeren, gebruikersbeheer |
| Donker / licht thema | Schakelknop, voorkeur opgeslagen in localStorage |

## Architectuur

```
Browser
  └── Blazor WASM (Obrigenie)
        ├── AuthHeaderHandler  — voegt JWT Bearer toe aan elke aanvraag
        ├── ApiService         — getypeerde HTTP-wrapper voor alle REST-aanroepen
        ├── AuthService        — JWT-opslag en rol-decodering aan clientzijde
        └── CalendarService    — schoolkalender met localStorage-cache

              ↕ HTTPS (TLS-beëindiging via nginx)

ASP.NET Core 8 API (seragenda)
  ├── AuthController     — aanmelden, registreren, OAuth, e-mailbevestiging, JWT-uitwisseling
  ├── CoursesController  — CRUD voor lessen van de gebruiker
  ├── NotesController    — CRUD voor getimede notities
  ├── AccessController   — licentievalidatie en -status
  ├── AdminController    — bewerkingen voorbehouden aan admins
  ├── RefController      — referentiegegevens (vakken / niveaus / domeinen)
  └── ViseesMaitriser    — pedagogische doelstellingen (gepagineerd)

              ↕ Entity Framework Core

PostgreSQL-database
  ├── utilisateur         — gebruikersaccounts
  ├── user_course         — terugkerende lessen
  ├── user_note           — getimede notities
  ├── license             — pool van licentiecodes
  ├── cours               — catalogus van schoolvakken
  ├── cours_niveau        — koppeltabel vak ↔ niveau ↔ leerkracht
  ├── domaine             — pedagogische domeinen per vak-niveau combinatie
  └── calendrier_scolaire — gescrapete schooljaarsgebeurtenissen
```

## Snel aan de slag

### Vereisten

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [PostgreSQL 15+](https://www.postgresql.org/download/)
- Een [Brevo](https://www.brevo.com/) account voor SMTP-relay

### 1 — Pakketten herstellen

```bash
cd G:/csharpcallender/Obrigenie && dotnet restore Obrigenie.sln
cd G:/csharp/serappagenda/seragenda && dotnet restore seragenda.sln
```

### 2 — Backend configureren

Maak `seragenda/appsettings.Development.json` aan (niet in git):

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=seragenda;Username=postgres;Password=uwwachtwoord"
  },
  "JwtSettings": { "SecretKey": "uw-geheime-sleutel-min-32-tekens" },
  "EmailSettings": {
    "Host": "smtp-relay.brevo.com", "Port": "587",
    "Username": "brevo-login", "Password": "brevo-sleutel",
    "FromEmail": "noreply@uwdomein.be", "FromName": "Obrigenie"
  },
  "GoogleAuth": { "ClientId": "...", "ClientSecret": "..." },
  "MicrosoftAuth": { "ClientId": "...", "ClientSecret": "..." },
  "AppSettings": { "FrontendUrl": "http://localhost:5276" },
  "Cors": { "AllowedOrigins": [ "http://localhost:5276" ] }
}
```

### 3 — Migraties toepassen

```bash
cd G:/csharp/serappagenda/seragenda/seragenda
dotnet ef database update
```

### 4 — Uitvoeren

```bash
dotnet run --project seragenda
# → http://localhost:5276  |  Swagger: http://localhost:5276/swagger (alleen dev)
```

## API-referentie

Alle beveiligde eindpunten vereisen `Authorization: Bearer <jwt>`.

### Authenticatie

| Methode | Route | Auth | Beschrijving |
|---|---|---|---|
| POST | `/api/auth/login` | Nee | E-mail + wachtwoord aanmelding (beperkt: 5 req/15 min) |
| POST | `/api/auth/register` | Nee | Account aanmaken + bevestigingsmail verzenden |
| GET | `/api/auth/confirm` | Nee | E-mailadres bevestigen via token |
| GET | `/api/auth/google` | Nee | Google OAuth-stroom starten |
| GET | `/api/auth/microsoft` | Nee | Microsoft OAuth-stroom starten |
| GET | `/api/auth/exchange` | Nee | auth_pending cookie omwisselen voor JWT |

### Lessen, Notities, Licenties

| Methode | Route | Auth | Beschrijving |
|---|---|---|---|
| GET/POST/DELETE | `/api/courses` | JWT | Beheer terugkerende lessen |
| GET | `/api/courses/date/{date}` | JWT | Lessen op een specifieke datum |
| GET/POST/DELETE | `/api/notes` | JWT | Beheer getimede notities |
| GET | `/api/notes/range` | JWT | Notities binnen datumbereik (max 62 dagen) |
| POST | `/api/access/validate` | JWT | Licentiecode valideren en toewijzen |
| GET | `/api/access/check` | JWT | Licentiestatus controleren |

### Referentiegegevens (trapsgewijze keuzelijsten)

| Methode | Route | Auth | Beschrijving |
|---|---|---|---|
| GET | `/api/ref/cours` | JWT | Alle vakken uit de catalogus |
| GET | `/api/ref/niveaux/{codeCours}` | JWT | Beschikbare niveaus voor een vak |
| GET | `/api/ref/domaines/{codeCours}/{codeNiveau}` | JWT | Domeinen voor een vak + niveau |

### Beheerder

| Methode | Route | Auth | Beschrijving |
|---|---|---|---|
| GET/POST/PUT/DELETE | `/api/admin/licenses` | ADMIN | Volledig licentiebeheer |
| GET | `/api/update-scolaire` | ADMIN | Schoolkalender scraper activeren |
| GET | `/api/health` | Nee | Beschikbaarheidscontrole |

## Authenticatie

### JWT

- Ondertekend met HMAC-SHA256 via `JwtSettings:SecretKey`
- Geldigheid: **7 dagen** (`ClockSkew = TimeSpan.Zero`)
- Claims: `sub` (gebruikers-id), `email`, `role` (`ADMIN` of `PROF`), `prenom`
- Opgeslagen in **localStorage** van de browser — automatisch toegevoegd door `AuthHeaderHandler`

### Snelheidsbeperking

Auth-eindpunten: **5 aanvragen per 15 minuten** per client-IP → HTTP 429 bij overschrijding.

## Database — Dag-van-de-week bitmask

| Dag | Waarde |
|---|---|
| Maandag | 1 |
| Dinsdag | 2 |
| Woensdag | 4 |
| Donderdag | 8 |
| Vrijdag | 16 |
| Zaterdag | 32 |
| Zondag | 64 |

Voorbeeld: Maandag + Woensdag + Vrijdag = `1 + 4 + 16 = 21`

## Deployment

```bash
# Bouwen
dotnet publish seragenda/seragenda.csproj -c Release -o ./publish

# Service herstarten op de server
sudo systemctl restart serapi
```

Maak `/var/www/serapi/appsettings.Production.json` aan op de server met productiereferenties.
De map voor Data Protection-sleutels moet bestaan: `/var/www/serapi/dataprotection-keys`

## Testen

```bash
dotnet test                                              # backend tests (34)
dotnet test ObrigenieTest/ObrigenieTest.csproj          # frontend tests (43)
```

## Beveiligingsnotities

- `appsettings*.json` bestanden zijn uitgesloten van git
- De applicatie gooit een uitzondering bij het opstarten als de JWT-sleutel ontbreekt
- Wachtwoorden gehasht met BCrypt — nooit in leesbare tekst opgeslagen
- Licentiecodes opgeslagen als SHA-256 hashes — leesbare tekst slechts eenmalig teruggegeven
- Notities worden bij opslaan HTML-gestript (XSS-bescherming)
- Invoer wordt gevalideerd en gevaarlijke tekens worden geweigerd vóór elke databasebewerking

---

<div align="center">
<sub>Obrigenie — Made for Belgian teachers · Fait pour les enseignants belges · Gemaakt voor Belgische leerkrachten</sub>
</div>
