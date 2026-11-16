# Journal des versions / Changelog

Format : une section par version, la plus récente en haut. Dates = 2026.

## 2.6 — 16 novembre
- Publication sur GitHub : documentation (README, AIDE, CHANGELOG, CLAUDE.md, décisions d'architecture dans `docs/adr`), licence GPL v3.
- Le zip contient maintenant AIDE.md et LICENSE.

## 2.5 — 16 novembre
- Adresse CI-V par défaut **94h** (liste déroulante 94 / B2) ; le diagnostic utilise aussi 94 par défaut.

## 2.4
- SET et CI-V déplacés dans Options (⚙) : « Réglages avancés du poste » et « Monitoring et détection CI-V ». ÉMISSION (PTT) occupe la place libérée.

## 2.3
- PICS actif : les fréquences affichées sur le spectre se placent sur les crêtes maintenues et restent plus longtemps.
- Monitoring CI-V : adresse CI-V détectée sur la liaison, alerte si le logiciel écrit à une autre adresse, bouton « Détecter le poste… » (7 vitesses, lecture de l'identifiant).

## 2.2
- Heure UTC (HH:mm:ss) à gauche de la cascade, un repère toutes les 4 lignes de texte.
- VITESSE de x0,1 à x5 (clic gauche +, clic droit −), MOY de 1 à 20 (clic droit +, clic gauche −), maintien des crêtes réglable par clic droit sur PICS (normal ou 1 à 10 s).
- Bouton FIXE/BANDE supprimé (CENTRE et FIXE du scope conservés).

## 2.1
- Mode Centre : curseur et bande passante strictement au milieu, sans à-coup quand la fréquence change.
- Options > Couleurs de l'interface (arrière-plan, panneaux, textes, accents, chiffres de fréquence MAIN/SUB…).

## 2.0
- Menu Options (⚙) : couleurs du waterfall (10 couleurs, palette 64 couleurs + RVB + hexa, couleurs du curseur, de la courbe, des crêtes, de la bande passante).
- Export / import simplifié (Exporter / Importer / Annuler).

## 1.9
- Bouton BANDE/FIXE (supprimé en 2.2).

## 1.8
- Export / import inclut les réglages du poste (potentiomètres, boutons, filtres, fréquences, scope) et les renvoie au poste à la connexion.

## 1.7
- Cascade et spectre plus fluides (défilement sous-pixel entre deux trames, courbe adoucie), échelle de fréquence plus précise, première fenêtre d'export/import de la configuration.

## 1.6 / 1.5 / 1.4
- Vumètres à aiguille style IC-7760 (MAIN et SUB, pleine largeur, aiguilles épaisses) ; MET au clic gauche ; clic droit = barres / aiguilles.
- Adresse CI-V adoptée automatiquement quand le poste répond avec une autre adresse (cas réel : 94h au lieu de B2h) — c'était la cause d'une cascade vide.

## 1.1
- Fenêtre redimensionnable en conservant l'échelle (« scène »), sans ascenseur.
- `Diagnostic.bat` / `--diag`, aide pour le premier essai sur un vrai poste.

## 1.0
- Première version : télécommande CI-V, cascade haute qualité, poste virtuel, autotest, catalogue de 530 commandes.
