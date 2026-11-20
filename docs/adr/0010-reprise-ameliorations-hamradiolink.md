# ADR 0010 — Reprise des améliorations de HamRadioLink (TONE, alerte ROS, étalonnage, mise à jour)

Date : 2026-11-20. Statut : adopté.

## Contexte
HamRadioLink (autre projet de F5PBG, 125 postes) a reçu en V1.1-1.2 plusieurs fonctions utiles à un poste unique. Reprise demandée pour IC-7760 Link ; journal de trafic, tables de postes Yaesu/Kenwood, thèmes et Aligner ne sont pas repris.

## Décision
- Code repris et adapté dans `src\Audio.cs` (cartes son WASAPI, `TuneTone`, `RigMonitor` sans notch/DNR), `src\Tools.cs` (`Calib`, `MicRoute`, `TuneMic`, `RosGuard`, `Updater`, `CatLog`, `Lang`) et `src\ToolsForm.cs` (fenêtre Outils, bouton ⚑).
- Bouton **TONE** (pas « TUNE » : TUNE reste le coupleur `1C 01 02`). Puissance de sécurité = `14 0A` réduite à `ros.pct` %.
- Étalonnage : `Rig.ToWatts/ToSwr` passent par `Calib.Interp` (points `cal.po` / `cal.swr`, défauts = tables du manuel) ; graduations recalculées par `MainForm.ApplyCal`.
- Mise à jour : dépôt F5pbgLudovic/IC7760Link, actif dans la release `IC7760Link.exe` ; clés `maj.auto`, `maj.ignore` ; variable d'essai `IC7760_UPDATE_API`.
- Toutes les clés (`tune.*`, `ros.*`, `micro.*`, `cal.*`, `maj.*`, `meter.hold`) sont des réglages d'application, donc dans l'export.

## Limites
Rien n'est essayé sur poste réel : tonalité sur la carte USB, VOX/PTT, lecture du ROS en émission.