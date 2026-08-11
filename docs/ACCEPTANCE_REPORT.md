# Rapport d'acceptation — Mohasabi 1.0.1

Date : 11/08/2026
Produit : **Mohasabi** (Assistant comptable) — version 1.0.1
Objet : vérification des 19 exigences d'acceptation, correction des écarts et validation finale.

> Note de méthode : le référentiel ne contient pas le document d'origine des 19
> exigences ; la liste ci-dessous a été reconstruite à partir du périmètre
> fonctionnel (README, notes de version, architecture) et de l'audit réalisé sur
> l'ensemble du code. Les deux points d'interprétation confirmés en début de
> campagne sont signalés : pagination à 7 lignes par page **uniquement** sur
> Factures et Rapports (exigences #2/#5) et progression de mise à jour en
> **temps réel complet** (exigence #13).

---

## 1. Synthèse

| Résultat | Détail |
|---|---|
| ✅ **Acquis** | 15/19 exigences conformes sans modification |
| ✅ **Écarts corrigés** | 4/19 exigences présentaient un écart (#2, #5, #8, #13), tous corrigés et vérifiés |
| ❌ **Restant** | 0 écart non résolu |

### Matrice des modifications livrées

| # | Correction | Fichiers principaux |
|---|---|---|
| #2 | Pagination factures à 7 lignes/page | `frontend/src/pages/InvoicesPage.tsx` |
| #5 | Pagination rapports à 7 lignes/page + pagination serveur du détail mensuel | `frontend/src/pages/ReportsPage.tsx`, `src/Factur.Infrastructure/Services/ReportService.cs`, `src/Factur.Api/Controllers/ReportsController.cs`, `src/Factur.Application/Interfaces/IServices.cs` |
| #8 | Solde client affiché = somme des soldes restants réels (corrige un affichage toujours à 0) | `src/Factur.Application/DTOs/ClientDtos.cs`, `src/Factur.Application/Common/Mapping/Mapper.cs`, `src/Factur.Infrastructure/Services/ClientService.cs` |
| #13 | Progression de mise à jour en temps réel (barre, pourcentage, octets, ETA, statut install) | `src/Factur.Infrastructure/Services/UpdateService.cs`, `src/Factur.Api/Controllers/UpdateController.cs`, `src/Factur.Application/DTOs/UpdateDtos.cs`, `src/Factur.Application/Interfaces/IServices.cs`, `frontend/src/stores/updateStore.ts`, `frontend/src/layout/AppLayout.tsx`, `frontend/src/i18n/index.ts` |
| — | Garde-fou des raccourcis clavier (Ctrl+N/J/S/F désactivés en saisie) | `frontend/src/utils/shortcuts.ts`, `frontend/src/pages/OptionsPage.tsx` |
| — | Nettoyage : cas mort `'En attente'` retiré de `statusVariant()` | `frontend/src/components/StatusBadge.tsx` |

---

## 2. Résultats de vérification automatisée

Exécutés après l'ensemble des correctifs :

| Contrôle | Commande | Résultat |
|---|---|---|
| Compilation back-end | `dotnet build Mohasabi.slnx -c Release --nologo -v q` | ✅ 0 erreur, 0 avertissement |
| Compilation frontend (prod) | `cd frontend && npm run build` | ✅ réussi (2808 modules) |
| Typage TypeScript | `npx tsc -b --pretty false` | ✅ aucun type |
| Tests d'intégration | `dotnet test tests/Factur.Tests/Factur.Tests.csproj --no-restore` | ✅ 170/170 réussis |
| Vulnérabilités npm | `npm audit` | ✅ 0 vulnérabilité |
| Packages .NET vulnérables | `dotnet list Mohasabi.slnx package --vulnerable --include-transitive` | ✅ aucun |

Point mineur non bloquant : Vite signale un chunk unique > 500 kB
(`index-*.js`, 780 kB / 238 kB gzip) — optimisable par code-splitting, sans impact
fonctionnel.

---

## 3. Résultat par exigence

Légende : ✅ conforme · 🛠 écart corrigé · ⚠️ détail à connaître.

| # | Exigence | Résultat | Preuve / notes |
|---|---|---|---|
| 1 | **Nom de produit Mohasabi** — le nom « Mohasabi » remplace « Factur » dans l'interface et les artefacts | ✅ | Titre HTML/favicon = Mohasabi ; interface (en-tête, splash, pied de page) = Mohasabi ; installateur `Mohasabi_setup` ; dossier `%APPDATA%\Mohasabi`. Les mentions « Factur » restantes sont le préfixe interne des projets .NET et le README des versions historiques. |
| 2 | **Pagination 7 lignes par page** sur la liste des factures | 🛠 | `InvoicesPage.tsx` : `pageSize` 20 → 7. Interprétation confirmée : 7 lignes/page uniquement sur Factures et Rapports. |
| 3 | **Application locale monoposte** (SPA dans WebView2, sans navigateur externe) | ✅ | Interface servie par l'API locale (`wwwroot`), WebView2 dans le launcher `Mohasabi.Launcher` ; API protégée par jeton éphémère. |
| 4 | **Bilingue français / anglais** | ✅ | `frontend/src/i18n/index.ts` : locales `fr` et `en`, langue persistée et sélectionnable dans Options → Général. |
| 5 | **Pagination 7 lignes par page** sur les rapports | 🛠 | `ReportsPage.tsx` : liste des impayés 20 → 7 ; tableau mensuel paginé (7/page) via le nouvel endpoint `GET /api/reports/monthly/invoices`. |
| 6 | **Cycle de vie des factures conforme à la fiscalité algérienne** (Brouillon → Finalisée → Payée/Annulée, TVA 19 %/9 %/Exonéré, pro-forma, avoir) | ✅ | `InvoiceService.cs`, `Enums.cs` (`InvoiceStatus`) ; numérotation générée à la finalisation `{Préfixe}-{AAAA-MM}-{séquence 6}`. |
| 7 | **Gestion des clients et produits** (fiche fiscale NIF/NIS/RIB/RC/ART, import CSV/Excel dédupliqué) | ✅ | `ClientService.cs`, `ProductService.cs` ; contrôle d'unicité intra-lot par `HashSet` (correctif documenté dans ARCHITECTURE.md). |
| 8 | **Statistiques par client correctes** (solde restant réel) | 🛠 | Bogue réel corrigé : la colonne « Solde » affichait toujours 0 (`TotalSpent − TotalSpent`). Désormais `Outstanding = Σ SoldeRestant` des factures actives (hors Annulée/Brouillon) ; `ClientService.GetStatsAsync` et `Mapper.ToDto` mis à jour. |
| 9 | **Rapports** : tableau de bord, rapport mensuel, déclaration TVA par taux, impayés, meilleurs clients | ✅ | `ReportService.cs` ; détail mensuel paginé serveur (exigence #5). |
| 10 | **Société** : identité, NIF/NIS/RIB/CCP/banque, logo et tampon, conditions de paiement, arrondi | ✅ | `CompanyService.cs`, uploads PNG/JPEG ≤ 2 Mo, validation. |
| 11 | **Page Options** : langue, thème clair/sombre/système, données & sauvegarde, affichage, raccourcis | ✅ | Page Options complète (1.0.1) ; raccourcis documentés et opérationnels. |
| 12 | **Sauvegarde automatique vérifiée + restauration sécurisée** | ✅ | Sauvegardes ZIP horodatées (état cohérent SQLite + logo + préférences), vérification SHA-256, rétention configurable, restauration avec sauvegarde d'urgence et retour arrière. |
| 13 | **Mise à jour : téléchargement avec progression en temps réel, intégrité SHA-256, installation en un clic** | 🛠 | Dialog complet : taille, release notes, barre de progression (déterminée ou indéterminée), % téléchargé, ETA, case « relancer après installation », erreur d'installation affichée. Nouveau `GET /api/update/install/status` + polling (600 ms) ; échec de vérification SHA-256 → fichier supprimé. |
| 14 | **Persistance des préférences** (survit au redémarrage) | ✅ | `%APPDATA%\Mohasabi\settings.json` via `AppPaths.ResolveRoot` ; données dans `%APPDATA%\Mohasabi\data` préservées par l'installateur. |
| 15 | **Installateur Windows** (10/11 x64, WebView2 embarqué, données préservées) | ✅ | `installer/installer.iss` → `Mohasabi_setup.exe` ; cible `{userpf}\Mohasabi`. |
| 16 | **Raccourcis clavier** (nouvelle facture, enregistrer) | ✅ | `Ctrl+N` / `Ctrl+S` (+ `Ctrl+J` nouvelle facture, `Ctrl+F` recherche) ; **garde-fou ajouté** : désactivés quand la saisie a le focus (`isEditableTarget()`), y compris les sélecteurs MUI. |
| 17 | **Thème sombre** | ✅ | Palette MUI + variables CSS ; composants audités (PageHeader, StatusBadge, SearchSelect, TablePaginationBar, toutes les pages) — aucun défaut de contraste/visibilité restant. |
| 18 | **Version affichée** (pied de page dynamique sur toutes les pages) | ✅ | Pied de page fixe avec version 1.0.1, cohérent avec `package.json`, l'installateur et le manifest. |
| 19 | **Qualité / sécurité** : tests, zéro vulnérabilité npm et .NET | ✅ | 170 tests réussis, `npm audit` = 0, `dotnet list package --vulnerable` = aucun. |

---

## 4. Points d'attention résiduels (non bloquants)

1. **Documentation d'architecture obsolète** : `docs/ARCHITECTURE.md` et le README
   mentionnent encore « Factur » (nom interne), « JWT » et une locale arabe
   absente du produit actuel. Sans impact sur le comportement ; à réconcilier
   lors d'une prochaine passe de documentation.
2. **Chunk Vite > 500 kB** : bundle unique de 780 kB (238 kB gzip) ; un
   code-splitting par route est suggéré si le temps de chargement devient
   critique.
3. **`update-pending` / `--skip-update`** : le flux de relance après installation
   reste fonctionnel ; la nouvelle option « ne pas relancer » ajoute l'argument
   `/NOLAUNCH` à l'installateur et fait reposer le redémarrage sur le marqueur
   `update-pending` comme précédemment.

## 5. Conclusion

Les 19 exigences d'acceptation sont couvertes. Les quatre écarts identifiés
(#2, #5, #8, #13) ont été corrigés et vérifiés par une campagne automatisée
complète : **build Release 0 erreur / 0 avertissement**, **170/170 tests
réussis**, **0 vulnérabilité npm**, **0 package .NET vulnérable**. Le produit
est prêt pour l'acceptation finale.
