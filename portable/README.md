# TrackballCAD Portable — diagnostic de faisabilité

Prototype pour Windows PowerShell **5.1 Desktop, 64 bits, STA**, sans Visual Studio,
SDK à installer, NuGet, pilote ou droits administrateur. Le dossier `portable` est
autonome : copier son contenu dans un dossier autorisé sur le poste de l'école.

**Cette livraison réalise les étapes 1 et 2 : diagnostic et capture indépendante.**
Elle teste aussi l'accès COM en lecture seule à SolidWorks déjà ouvert. Elle ne fait
encore aucune rotation, aucun déplacement ni zoom. La navigation sera développée
après confirmation de la faisabilité sur le poste et de la version SolidWorks.

## Lancer sans modifier les politiques

Dans une console **Windows PowerShell non administrateur**, se placer dans ce dossier.
Ces commandes ouvrent chacune un processus STA; elles ne changent aucune politique :

```powershell
powershell.exe -NoProfile -STA -File .\Check-Environment.ps1
```

Rapport optionnel, utile pour communiquer les résultats :

```powershell
powershell.exe -NoProfile -STA -File .\Check-Environment.ps1 -ReportPath .\environment-report.json
```

Ouvrir SolidWorks normalement avant le diagnostic pour tester COM. Ne pas l'exécuter
en administrateur. Fermer ses dialogues et attendre qu'il soit prêt. Aucune instance
SolidWorks n'est créée automatiquement, aucun document n'est ouvert ou enregistré,
aucune vue n'est modifiée. Le rapport contient les chemins des périphériques.

Si les contrôles de capture réussissent :

```powershell
powershell.exe -NoProfile -STA -File .\Start-TrackballCAD.ps1
```

Depuis une console 64 bits, on peut préciser le chemin de Windows PowerShell :

```powershell
& "$env:WINDIR\System32\WindowsPowerShell\v1.0\powershell.exe" -NoProfile -STA -File .\Check-Environment.ps1
```

`System32` est la version 64 bits; `SysWOW64` contient la version 32 bits. Depuis un
processus 32 bits, la redirection Windows peut changer la résolution de System32 :
ouvrir **Windows PowerShell**, pas « Windows PowerShell (x86) ». Le diagnostic vérifie
les bits du processus réel. Ne pas utiliser PowerShell 7 (`pwsh`), ISE ni un hôte intégré.
Il faut une session Windows interactive, pas une session distante sans bureau interactif.

Il n'y a ni paquet AppX/MSIX, ni `Add-AppxPackage`, ni RegAsm, ni add-in à installer.
Les scripts compilent le C# à la demande avec `Add-Type` et les assemblies .NET Framework
présentes. Aucun téléchargement n'est effectué. La compilation peut utiliser les fichiers
temporaires du compte; une règle de contrôle applicatif peut la bloquer.
Après une modification du C#, relancer le script dans un nouveau processus : un type déjà
chargé ne peut pas être remplacé dans la même session.

## Quand s'arrêter

Si Windows refuse d'exécuter `Check-Environment.ps1`, le script ne peut pas diagnostiquer
son propre blocage. Lire le message de PowerShell. Ces commandes intégrées en lecture seule
peuvent aider, si leur utilisation est autorisée :

```powershell
$PSVersionTable
$ExecutionContext.SessionState.LanguageMode
Get-ExecutionPolicy -List
```

| Résultat | Blocage et suite autorisée |
|---|---|
| `Restricted`, script interdit | Le prototype ne peut pas démarrer. Transmettre le refus à l'IT. |
| `AllSigned`, signature exigée | Ces sources ne sont pas signées. Demander à l'IT si une version approuvée/signée peut être fournie. |
| `RemoteSigned` / fichier téléchargé non approuvé | Le marquage Internet peut empêcher l'exécution. Conserver les restrictions et demander la distribution autorisée à l'IT. |
| `ConstrainedLanguage` | Le C# arbitraire/Win32 est incompatible avec ce mode. Le diagnostic s'arrête avant ces opérations. |
| `Add-Type` ou assembly bloqué | L'erreur originale est affichée; aucun compilateur alternatif ou téléchargement n'est essayé. |
| Windows Forms indisponible | Pas de fenêtre de capture; communiquer l'erreur à l'IT. |
| Zéro souris Raw Input | Vérifier la connexion, le bureau distant et les pilotes déjà en place; ne rien installer. |
| COM inaccessible | SolidWorks peut être fermé, occupé, non publié dans la ROT, lancé sous une autre identité ou bloqué par les politiques. |

Aucun script ne change la politique d'exécution, ne retire le marquage Internet, n'ajoute
de certificat et ne lance de processus élevé. Ne pas recopier le script dans la console
pour contourner son interdiction. Le fonctionnement sans installation n'implique pas que
l'école autorise l'exécution de ce code.

## Identifier la Kensington

1. Démarrer la capture. Chaque ligne représente une **collection souris Raw Input**, pas
   nécessairement un appareil physique complet. Les lignes actives deviennent jaunes.
2. Bouger seulement la souris habituelle et noter sa ligne (handle, chemin).
3. Bouger seulement la boule Kensington : une autre ligne doit recevoir les mouvements.
4. Cliquer une ligne pour lire son chemin complet, puis **Use selected device** pour confirmer.
   La sélection est écrite dans `config.json` à la fermeture normale de la fenêtre.
5. Tourner l'anneau/molette dans les deux sens et observer `Wheel`, `HWheel` et `Wheel seen`.
   S'il n'y a aucun événement de molette, le diagnostic n'en invente pas : tester les boutons
   et relever le comportement du pilote existant. Un mode clavier + boule sera alors proposé
   pour le zoom à l'étape navigation.
6. Tester chaque bouton, puis les deux souris simultanément. `Held` est un masque de cinq
   boutons; `Flags / interval` réunit les transitions observées pendant l'intervalle UI.
7. Débrancher/rebrancher : la liste se rafraîchit, les compteurs et la sélection sont effacés.
   Confirmer à nouveau la trackball. Un ancien handle ne sera pas choisi automatiquement.
8. Arrêter avec **Stop / close**, la croix, ou **Échap quand la fenêtre a le focus**.

`sum dx/dy`, `Wheel` et `HWheel` sont les sommes depuis le rafraîchissement précédent
(100 ms par défaut). `last X/Y` conserve le dernier paquet; `Events` est cumulatif depuis
l'énumération. `Events/s` est mesuré par chronomètre, pas une fréquence annoncée par le matériel.
Les coordonnées absolues sont affichées mais ne sont jamais additionnées comme des deltas.
Les paquets sans handle connu sont comptés séparément et non attribués arbitrairement.

La molette est signée et n'est pas divisée systématiquement par 120. `Wheel seen` indique
une observation, pas une garantie de capacité : faire tourner l'anneau pour la vérifier.
Un clic bref entre deux rafraîchissements reste visible dans les flags d'intervalle.

## Configuration locale

```json
{
  "schemaVersion": 1,
  "diagnosticsRefreshMs": 100,
  "selectedDevice": null
}
```

Intervalle autorisé : 50 à 1000 ms. Il concerne uniquement le diagnostic, pas une future
cadence de navigation. Le périphérique confirmé est sauvegardé avec `path`, `vid`, `pid`,
`label`. VID/PID caractérise souvent un modèle, pas un exemplaire. Le chemin peut changer
après changement de port; le nom affiché est un libellé HID générique, pas un nom commercial
Kensington garanti. La sélection enregistrée est montrée mais doit être confirmée manuellement.
La persistance utilise un remplacement atomique; si le dossier est en lecture seule ou le
système de fichiers ne le permet pas, l'erreur est affichée et l'ancien fichier est conservé.

Un autre fichier JSON peut être indiqué explicitement :

```powershell
powershell.exe -NoProfile -STA -File .\Start-TrackballCAD.ps1 -ConfigPath .\config.json
```

## Limite du curseur — critère principal non satisfait

Raw Input distingue les sources; **la trackball continue à déplacer le curseur Windows**.
Le prototype laisse les deux périphériques fonctionner comme avant. Il ne satisfait donc
pas encore l'indépendance complète de la navigation et du curseur.

`RIDEV_NOLEGACY` n'est pas un mécanisme de suppression globale des mouvements d'une Kensington
précise dans un autre processus. Il n'est pas utilisé. Aucun hook de suppression, `ClipCursor`,
`SetCursorPos`, émulation de clics ou pilote n'est inclus.

Les compromis envisageables pour une future étape sont :

- navigation activée par un modificateur clavier : utile pour tester l'API, mais le curseur
  bougera toujours avec la boule;
- périphérique/pilote déjà présent exposant une collection non-souris indépendante : à vérifier
  concrètement, sans supposer que le matériel Kensington le permet;
- confinement partagé : il bloquerait aussi la souris principale et ne respecte pas l'objectif;
  il n'est pas proposé comme mode fonctionnel dans cette version portable.

Avec les API observées et une trackball reconnue comme souris ordinaire, aucune suppression
sélective robuste n'est démontrée ici sans composant supplémentaire. Si c'est indispensable,
la validation devra constater cette limite plutôt que prétendre avoir réussi.

## Organisation et étapes suivantes

```text
Check-Environment.ps1          hôte, politiques, Add-Type, Forms, Raw Input, COM
Start-TrackballCAD.ps1         configuration, chargement, boucle UI, sauvegarde
src/RawInputBridge.cs          P/Invoke, fenêtre message-only, capture et accumulateurs
src/DiagnosticsWindow.cs       petite fenêtre diagnostic, handlers entièrement C#
src/SolidWorksProbe.cs         acquisition COM et lecture de révision, rapport par étape
modules/Portable.Runtime.psm1  prérequis et compilation à la demande
modules/Portable.Configuration.psm1  JSON local
modules/SolidWorks.Probe.psm1   connexion COM existante, lecture de révision uniquement
tests/Test-Portable.ps1        compilation et tests hors matériel, sans framework tiers
config.json
README.md
LICENSE
```

La boucle est `Application.Run` sur le STA. `WM_INPUT` est lu en C#, puis transmis à
`DefWindowProc` pour le nettoyage Windows. L'accumulation est verrouillée; aucune exécution
PowerShell ni appel COM ne se produit dans le traitement Raw Input. Le timer UI lit les
instantanés; il ne crée pas une ligne ni un callback PowerShell par paquet.
Une inscription Raw Input préexistante dans le processus est refusée plutôt que remplacée.

Les prochaines étapes restent **non implémentées** :

3. Vérifier les méthodes et signatures dans l'aide officielle correspondant à la révision
   détectée, puis proposer un test explicite de petite rotation avec un document compatible.
4. Relier la boule à la rotation via modificateur clavier, accumulation et appels COM sérialisés
   sur STA à 30 Hz initialement, puis mesurer jusqu'à 60 Hz.
5. Ajouter déplacement via modificateur configurable, zoom par molette observée ou clavier +
   boule, sensibilités et inversions. Refuser l'arrière-plan et les états non compatibles;
   gérer refus COM, dialogues, fermeture et arrêt accessible. Aucune modification de géométrie
   ni sauvegarde automatique.
6. Mesurer le curseur et les deux mains; ne pas confondre succès de capture et indépendance complète.

Le test COM utilise `Marshal.GetActiveObject("SldWorks.Application")` et
`string ISldWorks.RevisionNumber()`. Il ne crée pas d'instance. Plusieurs sessions SolidWorks
constituent une ambiguïté : conserver une seule instance pour les premiers essais. Le rapport
conserve la révision brute, sans inventer une correspondance avec une année. Les refus COM sont
rapportés avec leur HRESULT. Un serveur COM totalement bloqué peut retenir un appel synchrone :
ce diagnostic ne promet pas un délai maximal. Fermer les dialogues et attendre un SolidWorks
réactif avant de le lancer.

## Validation

Un rapport utilisateur confirme PowerShell 5.1 x64 STA, FullLanguage, Add-Type,
Windows Forms et trois collections Raw Input, mais échoue avec `0x8002802B`
(`TYPE_E_ELEMENTNOTFOUND`) au test COM. La cause n'est pas encore établie.

Le diagnostic conserve maintenant l'objet COM dans C# et lit `RevisionNumber` via
`Type.InvokeMember`, sans découverte automatique des membres COM par PowerShell.
Cette voie d'appel ne change aucune autorisation et reste à tester sur le poste.
Le rapport distingue `ObjectAcquired`, `RevisionRead`, `Stage`, `HResult` et
`CleanupError`. `Connected` signifie ici que l'objet et sa révision sont accessibles;
aucune méthode de vue n'est testée. Les politiques sont affichées par nom dans le JSON.
Relancer le diagnostic dans un nouveau processus après mise à jour.

Tests automatiques proposés sur Windows :

```powershell
powershell.exe -NoProfile -STA -File .\tests\Test-Portable.ps1
```

Ils compilent les sources via Add-Type et vérifient le layout x64, l'accumulation, les boutons,
les molettes signées, les coordonnées absolues, l'indépendance de deux accumulateurs et le JSON.
Ils ne prouvent pas l'identité des périphériques physiques ni la connexion COM réelle.

**Sur l'environnement Linux de création :** fichiers et références contrôlés statiquement;
PowerShell 5.1, Win32 et SolidWorks indisponibles. Aucun résultat Windows n'est annoncé comme
acquis. Une CI Windows séparée est fournie dans le dépôt, sans résultat d'exécution local.

À rapporter après les essais : version/architecture/langage, erreurs exactes, nombre de collections,
chemin de chaque souris, dx/dy indépendants, flags de boutons, anneau exposé ou non, comportement
du rebranchement, révision COM et mouvement du curseur. Si seul COM échoue, la capture peut
être testée séparément sans ouvrir SolidWorks.

## Références officielles

- [Modes de langage PowerShell et restrictions Add-Type](https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.core/about/about_language_modes?view=powershell-5.1)
- [Politiques d'exécution](https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.core/about/about_execution_policies?view=powershell-5.1)
- [RegisterRawInputDevices et inscription par processus](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerrawinputdevices)
- [RAWMOUSE : X/Y, boutons, molette](https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-rawmouse)
- [WM_INPUT et DefWindowProc](https://learn.microsoft.com/en-us/windows/win32/inputdev/wm-input)
- [RevisionNumber, ISldWorks](https://help.solidworks.com/2022/english/api/sldworksapi/SolidWorks.Interop.sldworks~SolidWorks.Interop.sldworks.ISldWorks~RevisionNumber.html)
- [Type.InvokeMember et objets COM](https://learn.microsoft.com/en-us/dotnet/api/system.type.invokemember?view=netframework-4.8)

Licence MIT. SolidWorks et ses composants ne sont pas distribués dans ce dossier.
