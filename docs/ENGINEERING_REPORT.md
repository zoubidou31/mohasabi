# Rapport d'ingénierie — Mohasabi 1.0.3

Date : 11/08/2026
Produit : **Mohasabi** (Assistant comptable) — version 1.0.3
Périmètre : description technique de la solution, des correctifs livrés dans le
cadre de la campagne d'acceptation et des résultats de vérification.

---

## 1. Présentation du produit

Mohasabi est une application locale monoposte de facturation conforme à la
fiscalité algérienne (TVA 19 %, 9 %, Exonéré, IFU). Le produit est une SPA
(React 18 / TypeScript / MUI) servie par une API locale ASP.NET Core 9
(Clean Architecture), le tout embarqué dans un launcher Windows (WinForms +
WebView2). Aucune authentification : les données restent 100 % locales (SQLite).

## 2. Vue d'ensemble de la solution

```
┌──────────────────────────────┐
│  Mohasabi.Launcher (WinForms)│  Splash animé + WebView2 + démarrage API
│  tools\Mohasabi.Launcher     │  Marqueur update-pending · arg --skip-update
└──────────────┬───────────────┘
               │ http://localhost (jeton éphémère)
┌──────────────▼───────────────┐
│      Factur.Api  (net9.0)    │  Controllers · Middleware · Serilog
│  Sert la SPA compilée (/wwwroot) ─ monolithique (1 image Docker)
└──────────────┬───────────────┘
┌──────────────▼───────────────┐
│     Factur.Application       │  Cas d'usage, DTOs, interfaces (IServices.cs)
└──────────────┬───────────────┘
┌──────────────▼───────────────┐
│      Factur.Domain           │  Entités pures, enums (chaînes JSON)
└──────────────┬───────────────┘
┌──────────────▼───────────────┐
│   Factur.Infrastructure      │  EF Core · SQLite · Exports · Update · Backup
└──────────────┬───────────────┘
               │ SQLite (%APPDATA%\Mohasabi\data\factur.db)
```

Les projets conservent le préfixe interne `Factur.*` (nom historique) ; le nom
commercial et l'interface utilisateur sont entièrement **Mohasabi**.

## 3. Découpage en projets (Clean Architecture)

| Projet | Rôle |
|---|---|
| `src/Factur.Api` | Composition racine, `Program.cs` (exceptions → HTTP, Serilog, CORS, Swagger en dev, jeton éphémère), contrôleurs |
| `src/Factur.Application` | Services métier, DTOs, interfaces — sans dépendance framework |
| `src/Factur.Domain` | Entités, enums, règles métier pures |
| `src/Factur.Infrastructure` | EF Core + SQLite, exports (PDF/XLSX/DOCX/CSV), e-mail, mise à jour, sauvegarde/restauration, audit |
| `tools/Mohasabi.Launcher` | Launcher WinForms + WebView2 (hors solution principale) |
| `frontend/` | SPA React 18 + TS + MUI + Zustand + i18n (fr/en) |
| `tests/Factur.Tests` | Tests d'intégration (`WebApplicationFactory`) |

## 4. Couche API

- Pipeline : `ExceptionHandlingMiddleware` mappe `UnauthorizedAccessException` →
  401, `NotFoundException`/`KeyNotFoundException` → 404, exceptions métier →
  400, autres → 500.
- Contrôleurs : Auth (jeton éphémère), Clients, Products, Invoices, Company,
  Reports, Files, Update, Settings, Backup/Restore.
- API locale protégée par un jeton éphémère (CSRF / appels externes bloqués).
- Nouveautés de la campagne :
  - `GET /api/update/install/status` (état de l'installation en temps réel) ;
  - `GET /api/reports/monthly/invoices?year=&month=&page=&pageSize=` (détail
    mensuel paginé côté serveur).

## 5. Couche métier (Application)

- `src/Factur.Application/DTOs/` : `InvoiceDtos`, `ClientDtos`, `ProductDtos`,
  `ReportDtos`, `UpdateDtos` (ajout de `UpdateCheckResult.SizeBytes` et de
  `UpdateInstallStatusDto`).
- `src/Factur.Application/Interfaces/IServices.cs` : contrats des services ;
  ajouts `IUpdateService.GetInstallStatus()` et
  `IReportService.GetMonthlyInvoicesPagedAsync(...)`.
- `src/Factur.Application/Common/Mapping/Mapper.cs` : `ToSummaryDto()` et
  `ToDto(...)` — signature étendue à `(client, invoiceCount, totalSpent,
  outstanding, lastInvoiceDate)`.

## 6. Données (SQLite / EF Core)

- Base `factur.db`, `Data Source` surchargeable ; `MigrateAsync()` puis
  `SeedAsync()` au démarrage.
- Persistance dans `%APPDATA%\Mohasabi\data` (installateur) — préservée entre
  les versions.
- Enums exposés en chaînes (`JsonStringEnumConverter`).
- Correctif majeur de la campagne : le champ **Solde client**. Avant, la
  colonne « Solde » affichait `TotalSpent − TotalSpent = 0` (champ unique
  alimenté par une seule source). Désormais `ClientDto.Outstanding` =
  `Σ SoldeRestant` des factures actives (hors Annulée et Brouillon),
  calculé dans `ClientService.ComputeStatsAsync` et propagé par le `Mapper`.
  Tous les appelants de `ToDto` ont été mis à jour.

## 7. Cycle de vie des factures

- États : `Brouillon → Finalisee → Payee` ; `Annulee` uniquement depuis un
  brouillon ; une facture payée est verrouillée.
- Numérotation générée à la finalisation : `{Préfixe-Société}-{AAAA-MM}-{seq 6}`.
- Paiements partiels (comptant, chèque, virement, carte, crédit) bornés à
  `SoldeRestant` ; remise, frais de port, autres frais, avoir, duplication.
- `SoldeRestant = Max(0, TotalTTC − MontantPaye)` (entité `Invoice`).

## 8. Rapports et pagination serveur

- `ReportService` : tableau de bord, rapport mensuel, déclaration TVA par taux,
  liste des impayés, meilleurs clients.
- Nouveau `GetMonthlyInvoicesPagedAsync(year, month, page, pageSize, ct)` →
  `PagedResult<InvoiceSummaryDto>` : exclut `Annulee`, trie par numéro.
- Le tableau mensuel de la page Rapports consomme désormais cet endpoint
  (pagination serveur réelle, 7 lignes/page), au lieu de paginer côté client
  une liste complète.

## 9. Sous-système de mise à jour

- Vérification : manifest `version.json` + installateur publiés sur GitHub
  Releases (HTTPS uniquement).
- `UpdateService.DownloadInstallerAsync` : téléchargement **streaming avec
  progression** (`CopyBufferSize`), suivi par `UpdateInstallTracker`
  (statique, phases `Downloading` / `Verifying` / `Launching`, messages en
  français).
- Intégrité : empreinte SHA-256 du manifest comparée ; en cas d'échec, le
  fichier est supprimé et `UpdateInstallTracker.Fail(...)` enregistre l'erreur.
- `POST /api/update/install` : téléchargement, vérification, puis lancement de
  l'installateur. Nouveau paramètre `launchAfterUpdate` : si faux, argument
  `/NOLAUNCH` transmis à l'installateur ; la relance après installation reste
  assurée par le marqueur `update-pending` et l'argument `--skip-update` du
  launcher.
- Nouveau `GET /api/update/install/status` → `UpdateInstallStatusDto`
  (phase, pourcentage, octets téléchargés, message, erreur).

## 10. Frontend — structure

- `frontend/src/api/` : `types.ts` (DTOs alignés sur le backend, dont
  `InvoiceStatus = 'Brouillon' | 'Finalisee' | 'Payee' | 'Annulee'`) et
  `client.ts` (axios, interceptor jeton, `extractError`).
- `frontend/src/stores/` : `updateStore.ts` (zustand) — `installStatus`, taille,
  `installNow(launchAfterUpdate?)`.
- `frontend/src/layout/AppLayout.tsx` : en-tête, navigation, notifications,
  pied de page fixe avec version, **dialog de mise à jour**.
- `frontend/src/pages/` : Invoices, Clients, Products, Reports, Company,
  Options ; `frontend/src/components/` : StatusBadge, SearchSelect,
  TablePaginationBar, PageHeader.

## 11. Frontend — i18n

- Locales `fr` et `en` dans `frontend/src/i18n/index.ts`, langue persistée et
  sélectionnable (Options → Général). Aucune locale arabe n'est présente dans
  le produit courant (le README d'architecture est obsolète sur ce point).
- Nouvelles clés de la campagne : `update.size`, `update.estimatedTime`,
  `update.whatNew`, `update.launchAfterUpdate`, `update.downloading`
  (`{{percent}}`), `update.verifying`, `update.launching`,
  `update.progressStatus` (`{{downloaded}}`/`{{total}}`), `update.unknownSize`.

## 12. Frontend — raccourcis clavier

- `utils/shortcuts.ts` : `Ctrl+N` (nouvelle facture), `Ctrl+S` (enregistrer),
  `Ctrl+J` (nouvelle facture), `Ctrl+F` (recherche).
- **Garde-fou ajouté** : `isEditableTarget()` désactive les raccourcis lorsque
  le focus est sur un champ de saisie (`INPUT`, `TEXTAREA`, `SELECT`,
  `contentEditable`), y compris les éléments masqués des sélecteurs MUI.
- Les raccourcis sont documentés dans la page Options.

## 13. Frontend — thème clair / sombre

- Thème système / clair / sombre via palette MUI + variables CSS
  (`frontend/src/styles/global.css`).
- Audit de tous les écrans : PageHeader, StatusBadge (palettes light/dark
  distinctes), SearchSelect, TablePaginationBar et les 7 pages — aucun défaut
  de lisibilité résiduel.
- Nettoyage : le cas `'En attente'` de `statusVariant()` était mort (aucun
  statut correspondant dans le backend, qui n'envoie que Brouillon/Finalisee/
  Payee/Annulee) ; il a été retiré de `frontend/src/components/StatusBadge.tsx`.

## 14. Frontend — pagination et UX des listes

- Liste des factures : `pageSize` par défaut passé de 20 à **7**.
- Page Rapports : liste des impayés 20 → **7** ; tableau mensuel paginé côté
  serveur (**7**/page) via `GET /api/reports/monthly/invoices`, avec
  `TablePaginationBar` et remise à la page 0 au changement de mois/année.
- Interprétation confirmée : la règle « 7 lignes/page » s'applique aux
  Factures et Rapports, pas aux Clients/Produits.

## 15. Dialog de mise à jour (interface utilisateur)

- État complet : taille du fichier (formaté en octets/Ko/Mo), ETA estimée,
  release notes **sanitisées** (`parseReleaseNotes()` : 3 à 6 puces,
  correction du double-encodage Latin-1→UTF-8 via `fixEncoding()`),
  case « relancer Mohasabi après l'installation », erreur affichée dans une
  `Alert` en cas d'échec.
- Pendant l'installation : polling `GET /update/install/status` toutes les
  600 ms ; `LinearProgress` déterminé (avec %) ou indéterminé selon la phase ;
  libellé phase + octets téléchargés/totaux mis à jour en continu.

## 16. Sécurité

- API locale derrière un jeton éphémère (CSRF / appels externes bloqués).
- URLs de mise à jour restreintes à HTTPS.
- Exports durcis contre l'injection de formules (Excel/CSV : `FormulaSanitizer`).
- Validation e-mail ; uploads PNG/JPEG ≤ 2 Mo ; rate-limit.
- Correctifs antérieurs documentés : injection de chemin dans la suppression
  de sauvegarde et injection de journal dans la restauration (CodeQL) résolus.

## 17. Installateur et lanceur

- `installer/installer.iss` (Inno Setup) : `Mohasabi_setup.exe`, cible
  `{userpf}\Mohasabi`, WebView2 Runtime embarqué, données utilisateur
  préservées.
- `tools/Mohasabi.Launcher` : démarre l'API locale, affiche le splash animé
  (logo Mohasabi, compteur + ligne de reçu), puis ouvre la fenêtre maximisée ;
  gère le marqueur `update-pending` et l'argument `--skip-update`.
- Version homogène 1.0.3 (frontend `package.json`, installateur, pied de page).

## 18. Persistance, sauvegarde et restauration

- Préférences : `%APPDATA%\Mohasabi\settings.json` via `AppPaths.ResolveRoot`.
- Sauvegardes automatiques : ZIP horodaté (base SQLite en état cohérent via
  backup SQLite en ligne + logo/tampon + préférences), vérifiées (intégrité
  base, liste des fichiers, empreinte SHA-256), rétention configurable
  (3/5/10/tout), bouton « Sauvegarder maintenant », ouverture du dossier.
- Restauration : liste des sauvegardes (date, taille, statut), validation
  complète préalable, sauvegarde d'urgence automatique, confirmation explicite,
  redémarrage maîtrisé et retour arrière en cas d'échec.

## 19. Qualité et vérification

Exécuté après tous les correctifs :

| Contrôle | Résultat |
|---|---|
| `dotnet build Mohasabi.slnx -c Release --nologo -v q` | ✅ 0 erreur, 0 avertissement |
| `npm run build` (prod, sortie dans `src/Factur.Api/wwwroot`) | ✅ réussi |
| `npx tsc -b --pretty false` | ✅ aucun type |
| `dotnet test tests/Factur.Tests/Factur.Tests.csproj --no-restore` | ✅ 170/170 |
| `npm audit` | ✅ 0 vulnérabilité |
| `dotnet list Mohasabi.slnx package --vulnerable --include-transitive` | ✅ aucun |

Le warning `CS8767` (nullabilité de `expectedSha256` dans `UpdateService`)
rencontré en cours de route a été résolu en déclarant le paramètre `string?` ;
la reconstruction est sans avertissement.

## 20. Limites connues et pistes d'amélioration

1. **Documentation obsolète** : `docs/ARCHITECTURE.md` et le README citent
   encore « Factur » (nom interne), « JWT » et une locale arabe absente. À
   réconcilier (documentation uniquement).
2. **Chunk Vite > 500 kB** : bundle unique de 780 kB (238 kB gzip) — un
   code-splitting par route réduirait le chargement initial.
3. **Suite de tests** : 170 tests d'intégration couvrent l'API, les exports et
   la sécurité ; pas de tests E2E frontend ni de tests dédiés au nouveau
   chemin de progression de mise à jour (à couvrir si la couverture doit être
   étendue).
4. **Traqueur de progression en statique** : `UpdateInstallTracker` est un état
   en mémoire partagé — acceptable pour une app monoposte, mais à adapter pour
   tout futur mode multi-processus.
