# CLAUDE.md — IC-7760 Link

Consignes pour reprendre ce projet avec un minimum de lecture. **Lire d'abord `REPRISE.md`** (état, carte des fichiers, commandes), puis seulement les fichiers utiles. Ne pas relire tout le code ni `CHAT.md` sans besoin précis.

## Règles du projet (demandées par l'utilisateur, F5PBG)
- Répondre en **français**.
- Projet versionné dans un **git local** ; chaque changement notable = un commit ; `CHAT.md` consigne les échanges ; les décisions d'architecture sont dans `docs/adr/` (un ADR par décision) ; ces règles sont dans ce fichier.
- Tester **uniquement sur le poste virtuel** (`--virtual`, `--selftest`, `--shot`). Jamais de vraie station, jamais d'émission réelle.
- **Un zip numéroté par mise à jour** : changer `Version` dans `src\MainForm.cs`, compiler, copier `LISEZMOI.md` dans `dist`, créer `IC7760Link-X.Y.zip` avec `IC7760Link.exe`, `LISEZMOI.md`, `AIDE.md`, `LICENSE` et les fichiers `AIDE-POUR-L-AMI.txt`, `Diagnostic.bat`, `ic7760-catalogue-CI-V.txt` de `dist`, **sans** le dossier `data` ; supprimer l'ancien zip ; ajouter une entrée à `CHANGELOG.md` et `LISEZMOI.md`.
- Publier sur GitHub (`F5pbgLudovic/IC7760Link`) seulement sur demande : commit, tag `vX.Y`, release avec l'exe et le zip (voir « Publication »).
- Fenêtre : échelle proportionnelle, rapport conservé, aucun ascenseur (ADR 0003). Tout nouveau contrôle doit être créé avant `BuildStage()` ou être sensible à l'échelle.
- Données de la vie privée : ne jamais mettre d'adresse e-mail ni de données personnelles dans le dépôt.

## Commandes
- Compiler : `powershell -ExecutionPolicy Bypass -File build.ps1` (csc de Windows, C# 5 : pas d'async/await, pas d'interpolation `$""`).
- Autotest : `dist\IC7760Link.exe --selftest=rapport.txt` → « TOUS LES TESTS PASSENT » (le test « PTT retour d'état » est parfois instable sous charge : relancer).
- Capture : `--shot=image.png,ms,actions` (actions séparées par `+` : dual, sub, tune, cw, tx, zoom, fixed, ft8, sideh, forms, small, big, wide, analog, theme, radio, optshot). Lancer l'exe depuis PowerShell et utiliser un chemin relatif.
- Diagnostic poste réel : `Diagnostic.bat` ou `--diag=COM5,115200,94`.
- Pas de Python sur la machine : éditer avec l'outil Edit ou PowerShell. Scripts PowerShell contenant des accents : les enregistrer en UTF-8 **avec BOM**.

## Publication (méthode qui fonctionne sur ce PC)
```
export PATH="$PATH:/c/Program Files/Git/cmd:/c/Program Files/GitHub CLI"
git -c credential.helper= -c 'credential.helper=!gh auth git-credential' push origin main   # idem pour un tag
gh release create vX.Y dist/IC7760Link.exe IC7760Link-X.Y.zip --repo F5pbgLudovic/IC7760Link --title "IC-7760 Link X.Y" --notes-file notes.md --latest
```
Message de commit dans un fichier (`git commit -F fichier`). Après la release, re-télécharger les assets et comparer les empreintes.

## Hypothèses encore non vérifiées
Indexation de la largeur de filtre `1A 03` (0-40, AM 0-49) copiée de la famille IC-7300/7610 ; écriture réelle des réglages (export/import) et émission non testées sur un poste réel.
