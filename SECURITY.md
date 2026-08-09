# Politique de sécurité — Mohasabi

Merci de consulter les consignes ci-dessous pour signaler une vulnérabilité ou un souci de sécurité.

## Mesure de protection intégrée

Mohasabi est une application **locale monoposte** (single-user) : l'API ASP.NET Core n'écoute
uniquement sur l'interface de boucle locale (`127.0.0.1`) et est protégée, au lancement, par
un **jeton d'authentification éphémère** généré par le lanceur à chaque session. Le code
frontal injecté dans la fenêtre WebView2 est le seul à posséder ce jeton ; toute requête
provenant d'un autre processus ou d'une page web est rejetée avec `401 Unauthorized`.

## Signalement d'une vulnérabilité

**Ne publiez jamais de rapport de vulnérabilité publique ouvert.**

Veuillez signaler tout problème de sécurité de façon privée en ouvrant une
**GitHub Security Advisory** (rapport confidentiel) :

- Page : <https://github.com/zoubidou31/mohasabi/security/advisories/new>

Cela permettra au mainteneur de recevoir un avis confidentiel et de publier un correctif
coordonné avant la divulgation publique.

Alternativement, vous pouvez envoyer un e-mail privé à l'adresse du mainteneur indiquée sur
le profil GitHub (<https://github.com/zoubidou31>) en incluant :

1. La description de la vulnérabilité (type, lieu, impact).
2. Une reproduction pas à pas.
3. La version concernée (affichée dans l'interface : `Paramètres → À propos`).
3. Vos coordonnées pour être tenu(e) informé(e) du correctif.

## Portée des programmes d'indemnisation

À ce jour, ce projet ne bénéficie pas de programme de récompense de vulnérabilités (bug bounty).
Les contributions de la communauté sont néanmoins grandement appréciées et créditées.

## Versions concernées

| Version | Supportée |
|---------|-----------|
| 1.0.x   | ✅ Oui    |

## Politique de diffusion des correctifs

- Un correctif de sécurité est publié dans une nouvelle version balancée (`x.x.1` ou ultérieure).
- Les correctifs critiques sont diffusés dans les 30 jours ouvrés suivant la validation.
- La divulgation publique n'intervient qu'après publication du correctif et d'une
  note de version détaillant le risque et les mesures d'atténuation.
