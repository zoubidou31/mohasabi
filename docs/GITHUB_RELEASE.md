# Publication GitHub Release — Mohasabi

> **Version courante : 1.0.1.** Le guide ci-dessous documente la première publication
> (1.0.0) et la procédure pour les versions suivantes (§8) ; les étapes restent
> identiques pour toute version `vX.Y.Z` (bump de `Directory.Build.props`, tag,
> assets depuis `release\update-source\`).

Guide de publication de `Mohasabi 1.0.0` (première version officielle) sur GitHub
Releases et branchement du système de mise à jour intégré sur GitHub (HTTPS).

---

## 1. Modifications apportées (résumé pour le Release 1.0.0)

| Fichier | Changement |
|---|---|
| `src/Factur.Infrastructure/Services/UpdateService.cs` | Manifest enrichi (`sha256`) ; URLs validées (HTTPS requis, hôte local toléré) ; **vérification SHA-256 du fichier téléchargé** avec suppression en cas de non-concordance |
| `src/Factur.Application/DTOs/UpdateDtos.cs` | `UpdateCheckResult.Sha256` |
| `src/Factur.Application/Interfaces/IServices.cs` | `DownloadInstallerAsync(downloadUrl, expectedSha256, ct)` |
| `src/Factur.Api/Controllers/UpdateController.cs` | SHA-256 attendu transmis ; URL fournie par la requête refusée sauf hôte local ; nettoyage de l'installateur après installation |
| `installer/launcher.json` | `manifestUrl` → `https://github.com/zoubidou31/mohasabi/releases/latest/download/version.json` |
| `build-release.ps1` | Paramètres `-Version`, `-GitHubRepo`, `-ManifestOnly` ; `version.json` généré avec `sha256` + `downloadUrl` GitHub |
| `.gitignore` | Exclut binaires, `.cache`, bases dev, secrets |
| `.github/workflows/release.yml` | CI/CD : build + publication d'une Release à chaque tag `v*` |
| `RELEASE_NOTES.md`, `README.md` | Corrections (taille réelle de l'installateur, suppression des mentions JWT/authentification inexistantes) |

Le **Setup 1.0.0 existant n'a pas été modifié ni recompilé** (empreinte conservée, voir §7).

## 2. Mécanique du système de mise à jour

1. Au démarrage, le launcher lit `launcher.json` (`manifestUrl`) et appelle `GET /api/update/check`.
2. L'API télécharge le manifest (`version.json`) ; si `version` > version installée, le frontend affiche la notification et le bouton « Mettre à jour ».
3. `POST /api/update/install` télécharge l'installateur dans `%TEMP%\MohasabiUpdate\`, **vérifie son empreinte SHA-256** contre le manifest, puis :
   - écrit le marqueur `update-pending` dans `%APPDATA%\Mohasabi`,
   - lance l'installateur en silencieux (`/VERYSILENT /SUPPRESSMSGBOXES /NORESTART`),
   - quitte l'API après 2 s.
4. Le launcher détecte le marqueur, ferme la fenêtre, l'installateur remplace les fichiers (données utilisateur dans `%APPDATA%` **préservées**), l'app redémarre.

## 3. Sécurité de la mise à jour

- Transport : `https://github.com/zoubidou31/mohasabi/releases/latest/download/...` (HTTPS ; redirections GitHub vers `objects.githubusercontent.com` suivies automatiquement).
- **Intégrité : SHA-256 vérifié avant lancement de l'installateur** ; fichier supprimé en cas de non-concordance.
- URL du manifest comme de téléchargement contraintes à HTTPS (hôte local toléré uniquement pour les tests) ; une URL fournie par la requête est rejetée sauf vers un hôte local.
- Aucun secret, token ou clé privée dans le code, l'installateur, le manifest ni le dépôt (scan effectué : `.env.example` ne contient qu'un placeholder SMTP).
- `.gitignore` empêche d'engager `.env`, clés, bases dev et binaires.

## 4. Compatibilité Windows 7 / 10 / 11

- **Windows 10 et 11 : supportés.** Composants : .NET 9 (`net9.0`), WebView2 Runtime Evergreen ≥ 109, installateur Inno Setup `MinVersion=10.0`.
- **Windows 7 : techniquement impossible avec la pile actuelle**, et ce pour deux raisons indépendantes, documentées par Microsoft :
  1. **.NET 9** ne supporte ni Windows 7 ni Windows 8.1 (dernière version compatible : .NET 6, support terminé le 12/11/2024). Microsoft : « *No version of .NET is supported on Windows 7 and Windows 8.1.* »
  2. **WebView2 Runtime** : les versions 109 sont les dernières à supporter Windows 7/8/8.1 ; les SDK ≥ 1.0.1519.0 (utilisé ici : 1.0.4129.x) l'exigent à partir de Windows 10. Microsoft Edge blog, 2022-12-09.
  - Le runtime Evergreen embarqué (v151.0.4129.72 au moment du test) ne peut pas être remplacé par une « Fixed Version » Win7 puisque l'application .NET 9 elle-même ne démarre pas sous Windows 7.
- **Conclusion** : cibler Windows 10/11 uniquement. Un retour à Windows 7 exigerait une réécriture sur .NET Framework 4.8 + WebView2 Fixed 109 + réadaptation du code — hors périmètre, à écarter.

## 5. Performance

- Runtime WebView2 téléchargé **une seule fois** puis mis en cache local (`.cache\webview2\`, empreinte vérifiée à chaque build).
- Installateur autonome (runtime .NET inclus) — pas de dépendance d'exécution réseau ; le runtime WebView2 n'est installé que s'il manque (`IsWebView2Missing`).
- Mise à jour incrémentale : seul le nouvel installateur (~274 Mo) est téléchargé, une seule fois, puis nettoyé après installation.

## 6. Limites — ce qui ne peut pas être garanti

- **Pas de certificat de signature de code** : la protection repose sur le SHA-256 publié dans le manifest servi en HTTPS par GitHub. Un attaquant ayant accès en écriture au dépôt pourrait altérer manifest + binaire. Une signature Authenticode (code-signing) leverait cette limite — à prévoir ultérieurement.
- **Reverse engineering** : impossible à empêcher à 100 % ; l'objectif visé (aucun secret embarque, aucune clé de licence) est déjà respecté.
- **Windows 7** : non supporté (voir §4).

## 7. Étapes exactes — publication du Release 1.0.0 (sans recompilation)

Le Setup testé (`Mohasabi_setup.exe`, SHA-256 `8FDA6625B9B75B98EE7D8294C1D2FA53C115B4BF44822D4144B47FD386ACA380`) est publié tel quel.

> **Important (1.0.0)** : ce Setup a été construit avec la vérification SHA-256 active.
> La version 1.0.0 est la première version officielle ; aucune version antérieure n'est
> conservée dans le système de versioning. L'URL de mise à jour est activée par défaut
> dans `launcher.json` (voir étape 0).

**Étape 0 — Activer les mises à jour sur le poste déjà installé**

Modifier le fichier :
`C:\Users\mohzo\AppData\Local\Programs\Mohasabi\launcher.json`
contenu :
```json
{
  "manifestUrl": "https://github.com/zoubidou31/mohasabi/releases/latest/download/version.json"
}
```
puis redémarrer l'application. (Les nouvelles installations à partir du prochain build auront cette URL par défaut.)

**Étape 1 — Vérifier que le manifest est prêt**

Le manifest a déjà été généré : `release\update-source\version.json`
```json
{
  "version": "1.0.0",
  "downloadUrl": "https://github.com/zoubidou31/mohasabi/releases/latest/download/Mohasabi_setup.exe",
  "sha256": "8FDA6625B9B75B98EE7D8294C1D2FA53C115B4BF44822D4144B47FD386ACA380",
  "releaseNotes": "Version initiale publiée."
}
```
Régénérable à tout moment sans build :
```powershell
./build-release.ps1 -ManifestOnly -GitHubRepo zoubidou31/mohasabi -Version 1.0.0 -ReleaseNotes "Version initiale publiée."
```

**Étape 2 — Créer la Release GitHub (interface web)**

1. Ouvrir https://github.com/zoubidou31/mohasabi/releases/new
2. Tag : `v1.0.0` ; cible : branche contenant les sources 1.0.0.
3. Titre : `Mohasabi v1.0.0`.
4. Notes : copier le contenu de `RELEASE_NOTES.md`.
5. **Assets** (glisser-déposer depuis `release\update-source\`) :
   - `Mohasabi_setup.exe` (287 298 468 octets)
   - `Mohasabi_README.txt`
   - `version.json`
6. Publish release.

**Étape 3 — Vérification**

- Ouvrir dans le navigateur : `https://github.com/zoubidou31/mohasabi/releases/latest/download/version.json` (doit retourner le manifest).
- Dans l'application : Paramètres → Mise à jour → Vérifier ; la notification doit indiquer que la version est à jour (`updateAvailable = false`).

## 8. Versions futures — CI/CD automatique

Le workflow `.github\workflows\release.yml` construit et publie automatiquement une Release à chaque tag `v*` (ex. `v1.0.1`).

1. Mettre à jour `Directory.Build.props` (`<Version>1.0.1</Version>`) et `RELEASE_NOTES.md`.
2. Committer, pousser, créer le tag et le pousser :
```powershell
git add .
git commit -m "Version 1.0.1"
git tag v1.0.1
git push origin main --tags
```
3. Le workflow : Setup .NET 9 + Node 20 + Inno Setup (choco) → `build-release.ps1 -GitHubRepo zoubidou31/mohasabi -Version 1.0.1 -ReleaseNotes ...` → `gh release create v1.0.1` avec les 3 assets.

> Maintenance : `build-release.ps1` pointe vers une URL Microsoft figée pour le runtime WebView2. Si le téléchargement échoue (404), mettre à jour `$webView2Url` et `$webView2Sha256` (adresse à récupérer sur la page officielle Microsoft « Download WebView2 Runtime »).
