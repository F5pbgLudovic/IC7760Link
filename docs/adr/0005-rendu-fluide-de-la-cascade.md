# ADR 0005 — Rendu fluide de la cascade et du spectre

- **Statut** : accepté (2026-11-16)
- **Contexte** : le poste envoie environ 20 trames par seconde ; avancer d'une ligne par trame donne un défilement saccadé.
- **Décision** : la cascade est un bitmap 32 bits épinglé (une ligne ajoutée par trame, historique recalé quand la fenêtre de fréquence change). Entre deux trames, `Animate()` (appelée par le timer de l'interface) fait glisser l'image d'une fraction de ligne (`DrawImage` bilinéaire sous-pixel) et adoucit la courbe du spectre par lissage exponentiel. Interpolation de Catmull-Rom horizontale vers la largeur d'affichage, maximum conservé quand il y a plus de points que de pixels. Heure UTC attachée aux lignes (numéro de ligne modulo 4 hauteurs de texte). En mode Centre, le curseur est calé sur le milieu de la fenêtre reçue (pas sur la fréquence locale) pour éviter tout à-coup.
- **Conséquences** : le glissement peut être coupé (menu clic droit « Défilement fluide »). Le rafraîchissement continu coûte quelques ms par image.
