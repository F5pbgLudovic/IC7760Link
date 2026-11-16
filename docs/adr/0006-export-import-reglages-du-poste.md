# ADR 0006 — Export / import de la configuration et des réglages du poste

- **Statut** : accepté (2026-11-16)
- **Contexte** : l'utilisateur veut pouvoir tout restaurer après une fausse manipulation.
- **Décision** : le texte exporté contient la configuration du logiciel (`Cfg.Dump`, clés `clé=valeur`) et, si le poste est connecté, les réglages relevés (`radio.rxN.*`, `radio.g.*`) : fréquence, mode, filtre, largeur, scope, split/dual/RIT/XIT et toutes les clés des listes `RxKeys`/`GlobalKeys`. L'import remplace la configuration, marque `radio.pending`, redémarre ; les réglages sont renvoyés ~6 s après la connexion. Les commandes d'accord/émission (`1C 01`, `1C 02`) ne sont jamais rejouées.
- **Conséquences** : les mémoires internes du poste et les réglages hors écran ne sont pas couverts ; la sauvegarde complète du poste reste celle de l'IC-7760 (carte SD / USB). L'écriture réelle de chaque commande reste à valider sur un poste réel.
