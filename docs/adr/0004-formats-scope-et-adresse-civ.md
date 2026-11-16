# ADR 0004 — Format des trames du scope et adresse CI-V

- **Statut** : accepté, partiellement confirmé (2026-11-16)
- **Contexte** : le catalogue CI-V extrait du guide ne contient pas les pages décrivant les trames `27 00` du scope ni l'indexation de `1A 03` (largeur de filtre).
- **Décision** : formats supposés identiques à la famille IC-7300/7610 (division de 50 points, en-tête mode + deux fréquences + hors-plage, amplitude 0-160 ; largeur indexée 0-40). **Confirmé sur un vrai poste** : trame `27 00 <rx> <div> <max>` avec en-tête de 12 octets, 15 divisions. L'adresse CI-V réelle observée est **94h** (et non B2h) : le logiciel adopte automatiquement l'adresse de la première trame reçue d'une autre adresse, relance la séquence d'ouverture, et propose 94/B2 en liste ; le monitoring affiche l'adresse détectée et sait la chercher (7 vitesses, lecture de l'identifiant par diffusion).
- **Conséquences** : l'index de largeur de filtre reste à confirmer sur le poste. Les trames brutes du scope sont masquées du journal normal (volume) sauf en diagnostic.
