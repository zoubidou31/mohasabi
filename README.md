# Mohasabi — Assistant comptable

Application de facturation conforme à la fiscalité algérienne (TVA 19 %, 9 %, Exonéré, IFU) : API **ASP.NET Core 9** (Clean Architecture) + frontend **React 18 / TypeScript / MUI**, déployables ensemble dans une seule image Docker.

---

## Fonctionnalités

- **Factures** : brouillon → finalisée → payée / annulée ; facture, pro-forma, avoir ; numérotation `FAC-YYYY-MM-XXXXXX` (préfixe société + série) ; remise, frais de port, autres frais ; paiements partiels (comptant, chèque, virement, carte, crédit) ; dupliquer ; avoir ; export **PDF / XLSX / DOCX / CSV** ; envoi par e-mail.
- **Clients & produits** : fiche fiscale algérienne (NIF 13-15, NIS, RIB, RC, ART), import CSV/Excel dédupliqué, statistiques par client.
- **Rapports** : tableau de bord, rapport mensuel, déclaration TVA par taux, liste des impayés, meilleurs clients.
- **Société** : identité, NIF/NIS/RIB/CCP/banque, logo & tampon (upload), conditions de paiement, arrondi bancaire.
- **Confidentialité** : aucune authentification, données 100 % locales (SQLite) sur le poste de l'utilisateur.
- **i18n** : français et arabe (RTL).

## Architecture

| Projet | Rôle |
|---|---|
| `src/Factur.Api` | Web API (ASP.NET Core 9), Swagger, Serilog, sert le frontend compilé (`wwwroot`) |
| `src/Factur.Application` | Cas d'usage, DTOs, interfaces |
| `src/Factur.Domain` | Entités, enums (chaînes JSON), règles métier |
| `src/Factur.Infrastructure` | EF Core (SQLite), exports, e-mail, mise à jour, traces de création/modification |
| `src/Factur.Tests` | Tests d'intégration API |
| `frontend/` | SPA React 18 + TS + MUI + i18n (fr/ar) |

Détails : [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md).

## Démarrage rapide (local)

Prérequis : .NET SDK 9, Node.js ≥ 20.

```bash
# 1. Backend (démarre sur http://localhost:5274, crée la base factur.db)
dotnet run --project src/Factur.Api

# 2. Frontend (dev avec proxy /api → 5274, sur http://localhost:5173)
cd frontend
npm install
npm run dev
```

Aucune authentification : l'application ouvre directement les données locales.

### Build monolithique

```bash
cd frontend && npm run build     # émet le bundle vers src/Factur.Api/wwwroot
dotnet run --project src/Factur.Api   # sert l'API + le frontend sur http://localhost:5274
```

## Docker

Une seule image contient l'API et le frontend compilé.

```bash
# Construire et lancer sur http://localhost:8080
docker compose up --build -d

# Configuration : copier .env.example vers .env puis ajuster (port, SMTP)
copy .env.example .env
```

Données persistées dans des volumes : base SQLite (`/data`), uploads (`/app/uploads`), journaux (`/app/logs`).

## Tests & couverture

```bash
dotnet test tests/Factur.Tests.csproj        # suite de tests d'intégration
dotnet test tests/Factur.Tests.csproj -p:CollectCoverage=true -p:CoverletOutputFormat=cobertura
# rapport : tests/Factur.Tests/TestResults/coverage.cobertura.xml
```

## API

- Swagger (dev) : `http://localhost:5274/swagger`
- Mise à jour : `GET /api/update/check`, `POST /api/update/install`
- Contrats des endpoints : `src/Factur.Api/Controllers/*.cs`

## Règles métier clés

- NIF : 13 à 15 chiffres ; NIS : 9 à 15 ; RIB : 23 caractères.
- Cycle de facture : `Brouillon → Finalisee → Payee`, annulation uniquement sur brouillon ; une facture payée ne peut être ni modifiée ni supprimée.
- Montants arrondis à 2 décimales (arrondi bancaire optionnel).
- `JsonStringEnumConverter` : les enums sont exposés en chaînes (`"Payee"`, `"Normal"`, …).
