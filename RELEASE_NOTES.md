# Notes de version — Mohasabi

## 1.0.0 (première version officielle)

**Mohasabi — Assistant comptable** : la première version officielle de l'application
de facturation conforme à la fiscalité algérienne (TVA 19 %, 9 %, Exonéré).

### Expérience au lancement
- Écran de démarrage (Splash Screen) court et animé : logo Mohasabi, nom de
  l'application, sous-titre « Assistant comptable », version affichée, animation
  « compteur + ligne de reçu » liée à la facturation.
- À la fin du splash, l'application s'ouvre directement en fenêtre maximisée.

### Interface
- Application locale monoposte : interface intégrée (WebView2) sans navigateur externe.
- Interface entièrement traduite en français et en anglais.
- Navigation : factures, clients, produits, rapports, paramètres.
- Affichage de la version de l'application dans un pied de page (visible sur toutes les pages).
- Carte « Développé par » avec coordonnées de contact.

### Facturation
- Factures conformes à la fiscalité algérienne (TVA 19 %, 9 %, Exonéré).
- Gestion des clients, produits, catégories et rapports.

### Mises à jour
- Vérification automatique des mises à jour au démarrage avec notification dans l'en-tête.
- Section « Mise à jour » dans les Paramètres : version courante, bouton de vérification,
  installation en un clic.
- Téléchargement, vérification de l'intégrité du fichier (empreinte SHA-256 publiée dans
  le manifest) puis installation silencieuse.
- Mises à jour servies depuis GitHub Releases (HTTPS) : source officielle `zoubidou31/mohasabi`.

### Sécurité
- API locale protégée par un jeton éphémère (protection CSRF / appels externes bloqués).
- URLs de mise à jour restreintes à HTTPS (hôte local toléré uniquement pour les tests).
- Durcissement des exports : neutralisation des formules Excel/CSV (injection de formules),
  validation e-mail, uploads PNG/JPEG limités à 2 Mo, rate-limit.

### Installateur
- Windows 10/11 (x64) : `Mohasabi_setup.exe` (~287 Mo, autonome, WebView2 Runtime embarqué).
- Dossier d'installation `%LOCALAPPDATA%\Mohasabi`, données utilisateur préservées dans
  `%APPDATA%\Mohasabi\data` lors des mises à jour.
- Manifest de mise à jour `version.json` publié comme asset de la Release GitHub.

### Marque
- Rebranding complet : **Mohasabi** (nom de l'application, sous-titre « Assistant comptable »).
- Nouvelle icône de l'application (`mohasabi.ico`) et images d'installation aux couleurs
  de la marque (vert).

### Tests
- Suite de tests back-end : contrôles de sécurité, exports, validation — 0 vulnérabilité
  npm, 0 package vulnérable .NET.
