# Notes de version — Mohasabi

## 1.0.0 (2026-08-09)

Version initiale publiée.

### Changements de marque
- Rebranding de l'interface : **Factur → Mohasabi** (nom de l'application, sous-titres FR/EN).
- Nouvelle icône de l'application (fichier `mohasabi.ico`).
- Nouveau favicon et titre d'onglet « Mohasabi — Assistant comptable ».
- Images d'installation personnalisées (bienvenue / petite bannière) aux couleurs de la marque.

### Mises à jour
- Vérification automatique des mises à jour au démarrage avec notification dans l'en-tête.
- Section « Mise à jour » repensée dans les Paramètres : version courante, bouton de vérification, installation en un clic.
- Téléchargement de la mise à jour, redémarrage automatique et installation silencieuse.

### Divers
- Carte « Développé par » avec coordonnées de contact.
- Interface entièrement traduite en français et en anglais.
- Installateur Windows (Inno Setup) : dossier `%LOCALAPPDATA%\Mohasabi`, icône bureau + menu Démarrer, lancement après installation.

### Installateur
- `dist\release\Mohasabi_setup.exe` (~274 Mo, x64, autonome, WebView2 Runtime embarqué).
- Manifest de mise à jour `version.json` (downloadUrl + empreinte SHA-256) régénéré dans `release\update-source\` avec une copie de l'installateur ; les 3 fichiers sont publiés comme assets de la Release GitHub.

### Tests
- Suite de tests back-end : **88 passés, 0 échec**.
