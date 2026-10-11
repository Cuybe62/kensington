# Architecture et analyse de faisabilité

## Décision structurante

La séparation physique ajoute un exécutable de capture à l'add-in demandé. Microsoft indique
qu'une seule fenêtre peut enregistrer une classe Raw Input par processus et déconseille
cet enregistrement dans une bibliothèque chargée par un autre programme. Enregistrer dans
SolidWorks pourrait détourner les entrées attendues par le logiciel ou un autre complément.
[Source : RegisterRawInputDevices](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerrawinputdevices).

```text
UI.exe STA
  RawInputManager → diagnostic de tous les périphériques
                  → sélection du hDevice → GestureGate → MotionAccumulator
  worker IPC : consume ~16 ms → canal nommé → accusé de réception

SolidWorks.exe
  worker IPC : validation → MotionAccumulator
  STA SolidWorks : Timer ~16 ms → NavigationEngine → ICadAdapter → IModelView
```

Le moteur ne référence ni COM ni WinForms. L'interface CAD n'expose que `IsAvailable` et
`Rotate` pour éviter de prétendre prendre en charge les fonctions de la deuxième étape.
L'énumération réserve None/Rotate/Pan/Zoom/Roll, mais seuls None et Rotate sont utilisés.

## Risques et statut

| Risque | Choix MVP / validation restante |
|---|---|
| Les deux souris déplacent le curseur | Capture distincte acquise par conception; suppression sélective non résolue |
| ClipCursor bloque les deux mains | Option désactivée par défaut, annulation sur autre souris, 5 s maximum |
| Clic natif d'activation | Aucun masquage; tester le bouton choisi et KensingtonWorks |
| Collision Raw Input avec l'hôte | Capture dans son propre processus |
| Mauvais thread COM | Seulement le timer STA créé dans ConnectToSW appelle le CAD |
| Afflux 1000+ Hz | Accumulation verrouillée, aucune file d'événements non bornée, lecture d'un buffer natif réutilisé |
| Retard ou blocage du CAD | Trames de plus de 150 ms rejetées, aucune boucle de rattrapage |
| Débranchement / hDevice recyclé | Annulation et sélection manuelle à chaque changement de topologie |
| VID/PID identiques | Pas d'identification individuelle par VID/PID seul |
| Documents et commandes modales | Effacement au changement de vue/document; états modaux à valider sur l'hôte |
| Timers non temps réel | Mesure du débit réel, pas de promesse de 120 Hz |
| Compatibilité SDK | Compilation locale obligatoire contre les interops installés |

La restauration avec `SetCursorPos` et les hooks bas niveau ne constituent pas ici une solution
validée : corréler deux flux ne garantit pas l'identité physique d'un événement fusionné.
Aucun contournement de ce type n'est présenté comme garantissant l'indépendance.

## Étapes livrées et comment les vérifier

1. **Solution et séparation** : `TrackballCAD.sln`, `Directory.Build.props`, six `.csproj`.
   Compiler le diagnostic sans SDK SolidWorks, puis la solution complète (commandes README).
2. **Lecture indépendante** : `Input/NativeMethods.cs`, `RawInputManager.cs`, `DeviceManager.cs`,
   `RawInputDevice.cs`, `DeviceIdentifier.cs`, `TrackballState.cs`.
   Le diagnostic doit distinguer les handles et chemins en bougeant chaque souris séparément.
3. **Activation et accumulation** : `Navigation/GestureGate.cs`, `MotionFrame.cs`,
   `NavigationEngine.cs`, `ICadAdapter.cs`, `PipeAddress.cs`, `Settings/NavigationSettings.cs`.
   Compiler/exécuter `TrackballCAD.Tests`; vérifier annulation, conservation et péremption.
4. **Interface et transfert** : `UI/Program.cs`, `DiagnosticsWindow.cs`, `NavigationClient.cs`,
   `WindowsContext.cs`. Tester débranchement, focus, bouton, connexion et confinement.
5. **Rotation COM** : `SolidWorks/SolidWorksAddin.cs`, `SolidWorksAdapter.cs`, `NavigationServer.cs`,
   `scripts/Register-Addin.ps1`. Compiler avec SDK, enregistrer, charger et tester une pièce.
6. **Validation** : `tests/TrackballCAD.Tests/Program.cs`, workflow Windows et `docs/validation.md`.
   Les sources sont complètes dans ces fichiers; aucune étape n'exige du code à recopier depuis le chat.
   `scripts/Start-Diagnostics.ps1` automatise la compilation du diagnostic, celle des tests,
   leur exécution et l'ouverture de l'interface sans SDK SolidWorks. La CI vérifie aussi
   la syntaxe des scripts avec le parseur Windows PowerShell.

Ne pas passer au pan/zoom/roll tant que les étapes matérielles ne sont pas acceptées.
