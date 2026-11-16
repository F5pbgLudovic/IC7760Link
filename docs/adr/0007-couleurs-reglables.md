# ADR 0007 — Couleurs réglables (waterfall et interface)

- **Statut** : accepté (2026-11-16)
- **Décision** : palette « Personnalisée » de 10 couleurs interpolées dans une table de 256 entrées (`Palettes.Custom`, version incrémentée à chaque changement pour recalculer la table) ; couleurs du curseur, de la courbe, des crêtes et de la bande passante dans `ScopeColors`. Les couleurs de l'interface sont des champs statiques modifiables de la classe `T` ; `Theme.Recolor` parcourt tous les contrôles ouverts et remplace ancienne couleur → nouvelle (fond, texte, `Accent` des boutons/potentiomètres/vumètres). Mémorisation dans `settings.ini`, donc dans l'export.
- **Conséquences** : les lignes déjà dessinées dans la cascade gardent leurs anciennes couleurs ; quelques couleurs fixes (fond noir de la cascade) ne sont pas réglables.
