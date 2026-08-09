# Architecture de Factur

## Vue d'ensemble

```
┌─────────────────────────────┐
│  Navigateur (SPA React 18)  │
│  MUI + i18n (fr/ar) + axios │
└──────────────┬──────────────┘
               │  /api  (JWT Bearer)
┌──────────────▼──────────────┐
│      Factur.Api (net9.0)    │
│  Controllers · Middleware   │
│  Serilog · Swagger (dev)    │
└──────────────┬──────────────┘
┌──────────────▼──────────────┐
│    Factur.Application       │
│  Services, DTOs, interfaces │
└──────────────┬──────────────┘
┌──────────────▼──────────────┐
│     Factur.Domain           │
│  Entités, enums, règles     │
└──────────────┬──────────────┘
┌──────────────▼──────────────┐
│   Factur.Infrastructure     │
│  EF Core · SQLite · JWT     │
│  Exports · E-mail · Audit   │
└──────────────┬──────────────┘
               │ SQLite (factur.db)
```

L'API sert aussi le frontend compilé depuis `src/Factur.Api/wwwroot`
(`UseDefaultFiles` + `MapFallbackToFile("index.html")`), ce qui permet un
déploiement **monolithique** (une image Docker).

## Backend

### Structure
- **`Factur.Api`** — composition racine, `Program.cs` (pipeline : exceptions → HTTP, Serilog, CORS, Swagger, JWT), contrôleurs `Auth / Clients / Products / Invoices / Company / Reports / Files`.
- **`Factur.Application`** — services métier (cas d'usage) et DTOs, indépendants de tout framework.
- **`Factur.Domain`** — entités pures (`Client`, `Product`, `Invoice`, `InvoiceLine`, `Payment`, `Company`, `User`, `AuditLog`), enums.
- **`Factur.Infrastructure`** — implémentation EF Core + SQLite, `JwtService`, exports (PDF/XLSX/DOCX/CSV), `EmailService`, journal d'audit.

### Flux HTTP → erreurs
`ExceptionHandlingMiddleware` mappe :
- `UnauthorizedAccessException` → **401**
- `NotFoundException` / `KeyNotFoundException` → **404**
- `BusinessRuleException` / `ValidationException` / `InvalidOperationException` → **400**
- autre → **500**

### Base de données
- SQLite, chaîne par défaut `Data Source=factur.db` (surchargeable via `ConnectionStrings__DefaultConnection`).
- Au démarrage : `MigrateAsync()` puis `SeedAsync()` (société, admin, 3 produits, 2 clients) si tables vides.
- La migration `20260806122850_InitialCreate` est appliquée automatiquement.

### Cycle de vie des factures
```
Brouillon ──finaliser──► Finalisee ──paiement(s)──► Payee
    │                        │
    └─annuler─► Annulee      └─(payée : verrouillée)
```
- L'annulation n'est autorisée que sur un **brouillon**.
- Le numéro est généré à la **finalisation** : `{PrefixSociété}-{AAAA-MM}-{séquence 6}`.
- Une facture payée ne peut être ni modifiée ni supprimée.

### Pièges résolus (à retenir)
1. **EF Core + enfants ajoutés** : des enfants non sauvegardés avec une clé `Guid` non par défaut, ajoutés à un parent déjà suivi, sont découverts `Modified` → `UPDATE 0 ligne` → `DbUpdateConcurrencyException`. Corrigé en marquant explicitement `EntityState.Added` (`InvoiceService.UpdateAsync`, `ImportLinesAsync`).
2. **SQLite + doublons intra-lot** : le contrôle d'unicité via `AnyAsync` ne voit pas les doublons du même lot → `UNIQUE constraint failed`. Corrigé avec un `HashSet` (`ClientService.ImportAsync`, `ProductService.ImportAsync`).
3. **`LogoPath`** : renvoyé avec `/` (`uploads/{fichier}`), jamais `Path.Combine` (séparateurs OS).

## Frontend

- **`frontend/src/api/`** — `types.ts` (DTOs alignés sur le backend), `client.ts` (axios, interceptor Bearer, `extractError`, déconnexion 401).
- **`frontend/src/store/auth.ts`** — zustand + persistance (`factur_token`, `factur_user`).
- **`frontend/src/i18n/`** — français + arabe, langue persistée (`factur_lang`), passage RTL.
- **`frontend/src/pages/`** — Login, Invoices (liste/formulaire/détail), Clients, Products, Reports, Company, Users (admin), Audit (admin).
- **`frontend/src/layout/AppLayout.tsx`** — header 72px, navigation horizontale, sélecteur de langue, notifications.

### Build / dev
- Dev : Vite sur `5173`, proxy `/api` → `http://localhost:5274`.
- Prod : `npm run build` émet dans `src/Factur.Api/wwwroot` (`outDir` relatif), `emptyOutDir: true`.

## Docker

Voir `Dockerfile` (multi-stage : Node build frontend → SDK .NET publish → runtime ASP.NET, port `8080`) et `docker-compose.yml` (volumes : `/data` SQLite, `/app/uploads`, `/app/logs`).

## Tests

- `tests/Factur.Tests` — tests d'intégration sur une base temporaire (`ApiFactory`), 83 tests.
- Couverture : **96,1 %** de lignes (rapport cobertura dans `tests/Factur.Tests/TestResults/coverage.cobertura.xml`).
