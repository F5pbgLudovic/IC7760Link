# ADR 0003 — « Scène » de taille fixe mise à l'échelle d'un bloc

- **Statut** : accepté (2026-11-13)
- **Contexte** : exigence de redimensionnement sans élément tronqué, avec proportions conservées et sans ascenseur.
- **Décision** : toute l'interface vit dans un `Panel stage` de taille de conception 1860×980. Après construction, tous les contrôles sont « snapshotés » (bornes, dock, polices, marges). À chaque `Resize`, `LayoutStage()` calcule `s = min(cw/1860, ch/980)`, applique `ApplyScale` récursivement et centre la scène (bandes noires en plein écran). `WM_SIZING` verrouille le rapport de la fenêtre. Les polices passent par `T.Fn(nom, pt, style)` (cache, mise à l'échelle), les tailles en pixels des contrôles dessinés par `T.Scale`.
- **Conséquences** : tout nouveau contrôle doit être créé **avant** `BuildStage()` ou être sensible à l'échelle. Pas d'AutoScroll.
