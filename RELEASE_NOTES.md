# Notes de version — Mohasabi

## 1.0.4 (Raccourcis clavier personnalisables)

**Mohasabi — Assistant comptable** : ajout de la personnalisation des raccourcis
clavier et fiabilisation de la saisie et des exports PDF.

### Raccourcis clavier
- Nouvelle section **Raccourcis** (Paramètres → Raccourcis) : assigner, modifier et
  réinitialiser les raccourcis des actions principales (Nouvelle facture, Journal,
  Sauvegarder, Rechercher).
- **Détection des conflits** : deux actions ne peuvent pas partager la même combinaison ;
  un avertissement est affiché et la sauvegarde est bloquée tant que le conflit persiste.

### Saisie et garde-fous
- Les raccourcis globaux sont **désactivés pendant la saisie** (champs texte, y compris
  les sélecteurs MUI, les sélecteurs de date et les champs numériques) afin de ne pas
  intercepter la frappe.
- **DevTools (F12)** désactivé en production pour éviter toute fuite d'outils de
  débogage dans l'application livrée.

### Exports PDF
- Rendu correct des **caractères CJK** (chinois, japonais, coréen) via un repli de police
  dédié, sans casser la mise en page du document.

### Qualité
- Compilation TypeScript corrigée (0 erreur) sur l'ensemble du frontend.

## 1.0.3 (Maintenance : conformité au référentiel d'acceptation)

**Mohasabi — Assistant comptable** : passe de maintenance alignant l'application
sur les 19 exigences d'acceptation (audit complet, 170 tests automatisés).

### Facturation & rapports
- Pagination des factures et des rapports à **7 lignes par page** (liste des
  factures, impayés et détail mensuel).
- Correction du **solde client** : la colonne « Solde » affiche désormais le total
  réel des montants restants à payer (somme des soldes restants des factures
  actives) au lieu de toujours afficher 0.

### Mise à jour
- **Progression en temps réel** du téléchargement et de l'installation : barre
  (déterminée ou indéterminée), pourcentage, octets, temps restant estimé, statut
  de l'installation, case « relancer après installation » (argument `/NOLAUNCH`).
- Échec de vérification SHA-256 → le fichier téléchargé est supprimé.

### Interface
- **Garde-fou des raccourcis clavier** : `Ctrl+N/J/S/F` désactivés pendant la saisie
  (y compris les sélecteurs MUI) pour ne pas intercepter la frappe.
- Nettoyage : cas mort « En attente » retiré de l'affichage des statuts.

### Robustesse
- Index de performance sur les jointures facture/statuts et les recherches de
  factures (migration ajoutée, sans perte de données).

### Qualité
- 170 tests d'intégration réussis, 0 vulnérabilité npm, 0 package .NET vulnérable,
  compilation Release 0 erreur / 0 avertissement.

## 1.0.1 (Options, sauvegarde automatique et performances)

**Mohasabi — Assistant comptable** : nouvelle page Options (préférences générales,
sauvegarde automatique des données, écran de démarrage), protection de la base de
données par sauvegardes automatiques vérifiées et restauration sécurisée, plus une
montée en charge pour les grands volumes (pagination côté serveur).

### Page Options
- Nouvelle page « Options » accessible par une icône dédiée dans l'en-tête (à côté
  des notifications). La sélection de la langue quitte l'en-tête pour Options → Général.
- Sections professionnelles : Général (langue, thème clair/sombre/système),
  Données & Sauvegarde, Affichage & Expérience (écran de démarrage activable), Raccourcis.
- Bouton « Enregistrer les modifications » : les préférences persistent après redémarrage.

### Sauvegarde automatique
- Sauvegarde automatique activable (5 min → 1 fois par jour, défaut 30 min) dans un
  dossier utilisateur dédié (%APPDATA%\Mohasabi\Backups).
- Copie de la base SQLite par mécanisme SQLite de sauvegarde en ligne (état cohérent),
  fichiers téléversés (logo, tampon), et préférences — le tout archivé en ZIP horodaté.
- Chaque sauvegarde est vérifiée (intégrité de la base, liste des fichiers, empreinte
  SHA-256) avant d'afficher « Protégé ».
- Rétention configurable (3, 5, 10 sauvegardes ou tout conserver ; défaut 5).
- Bouton « Sauvegarder maintenant », statut de la dernière sauvegarde, ouverture du dossier.

### Restauration sécurisée
- Restauration depuis la liste des sauvegardes (date, taille, statut).
- Avant toute restauration : validation complète de la sauvegarde, puis sauvegarde
  d'urgence automatique des données courantes (chemin de retour possible).
- Les données ne sont jamais remplacées silencieusement : confirmation explicite,
  redémarrage maîtrisé, validation de la base restaurée et retour arrière en cas d'échec.

### Robustesse
- Dialogue d'erreur professionnel en cas de problème inattendu (« Redémarrer Mohasabi »
  / « Fermer », détails techniques masqués par défaut).
- Détection d'une session précédente non fermée normalement : message non bloquant
  informant que les données sont protégées par la dernière sauvegarde.
- Pagination côté serveur pour les factures, clients et produits (grands volumes),
  recherche et sélecteurs avec recherche à la volée.

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
