# ADR 0008 — Distribution : zip numéroté, GitHub, GPL v3

- **Statut** : accepté (2026-11-16)
- **Décision** : un zip `IC7760Link-X.Y.zip` à chaque mise à jour (version dans `MainForm.Version`), contenant `IC7760Link.exe`, `LISEZMOI.md`, `AIDE.md`, `LICENSE`, `AIDE-POUR-L-AMI.txt`, `Diagnostic.bat`, `ic7760-catalogue-CI-V.txt` — **jamais** le dossier `data` (réglages locaux). Dépôt public `F5pbgLudovic/IC7760Link`, une release par version (exe + zip), licence GPL v3, projet indépendant non affilié à ICOM. `CHAT.md` et `REPRISE.md` restent locaux (ignorés par git).
- **Conséquences** : aucune signature de code (avertissement SmartScreen possible).
