# ADR 0001 — C# WinForms compilé avec le csc de Windows, exe unique

- **Statut** : accepté (2026-11-12)
- **Contexte** : l'ami destinataire veut un logiciel qui se lance sans installation sur Windows 10/11, sans pilote ni runtime à ajouter. Aucun Python ni SDK n'est disponible sur la machine de développement.
- **Décision** : C# (langage 5) pour .NET Framework 4, compilé par `csc.exe` livré avec Windows via `build.ps1`. Un seul `IC7760Link.exe`, catalogue CI-V intégré en ressource, réglages dans `data\settings.ini` à côté de l'exe (repli sur %APPDATA%).
- **Conséquences** : pas de NuGet, pas de async/await ni d'interpolation de chaînes (C# 5) ; interface WinForms dessinée à la main (contrôles `Btn`, `Knob`, `MeterBar`, `FreqDisplay`, `ScopeView`) ; manifeste DPI-aware.
