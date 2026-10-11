# TrackballCAD Portable — prototype de rotation

Prototype pour Windows PowerShell **5.1 Desktop, 64 bits, STA**, sans Visual Studio,
SDK à installer, NuGet, pilote ou droits administrateur. Le dossier `portable` est
autonome : copier son contenu dans un dossier autorisé sur le poste de l'école.

**Le diagnostic COM et le test de rotation de 5° ont été validés par l'utilisateur sur
la révision 33.5.0, avec un assemblage.** Cette livraison ajoute la liaison continue
trackball → rotation, confirmée par retour utilisateur en mode maintien de Maj. Le déplacement et le zoom restent reportés.
`Check-Environment.ps1` et `Start-TrackballCAD.ps1` restent des outils sans commande de vue;
seuls le test explicite et le nouveau `Start-Navigation.ps1` peuvent orienter la vue.

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
  "selectedDevice": null,
  "rotation": {
    "updateHz": 30,
    "activationKey": "LeftShift",
    "radiansPerCount": 0.002,
    "invertX": false,
    "invertY": false
  }
}
```

Intervalle autorisé : 50 à 1000 ms. Il concerne uniquement le diagnostic, pas la
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

## Étape 3 — petite rotation explicite (révision 33.x)

Le rapport utilisateur confirme désormais `ObjectAcquired=true`, `RevisionRead=true`,
`Stage=Complete` et la révision **33.5.0**. C'est une validation de l'accès COM et de
la lecture de version sur le poste. L'utilisateur a ensuite confirmé une rotation visible
et `RotationApplied=true`, `RedrawCompleted=true`, `Stage=Complete` sur un assemblage.

```powershell
powershell.exe -NoProfile -STA -File .\Test-SolidWorksView.ps1
```

1. Garder une seule instance SolidWorks ouverte avec une pièce ou un assemblage de test
   entièrement chargé. Terminer les commandes et fermer les dialogues. Utiliser la fenêtre
   principale, pas un document détaché.
2. Cliquer **Arm one 5 degree rotation (5 s)**, puis revenir dans SolidWorks avant cinq secondes.
3. Le test demande une seule rotation horizontale de 5° autour du centre de la vue, suivie
   d'un rafraîchissement graphique. Aucune géométrie n'est modifiée et rien n'est enregistré.
4. Échap annule le compte à rebours, même lorsque SolidWorks est au premier plan.
5. Revenir dans la fenêtre pour lire le résultat, puis la fermer : le même résultat JSON
   apparaît dans la console. Rapporter `Stage`, `RotationRequested`, `RotationApplied`,
   `RedrawCompleted`, `HResult` et si le modèle a effectivement tourné.

Le test utilise le même appel tardif C# `InvokeMember` que le diagnostic maintenant validé.
Il vérifie le PID de l'objet COM, la fenêtre principale au premier plan et activée, le document
de type pièce/assemblage, l'absence de mode view-only et la présence de `ActiveView`. Un changement
de document pendant la préparation annule l'action. La révision est limitée à 33.x pour ce test.
La préparation trop longue et un compte à rebours retardé sont abandonnés, sans rattrapage.

Un refus COM termine l'essai sans nouvelle tentative automatique. `RotationRequested=true`
avec une erreur signifie qu'une action a pu être envoyée : observer la vue avant de relancer.
Si seul le rafraîchissement échoue, `RotationApplied` reste vrai. Un appel COM synchrone bloqué
ne peut pas être interrompu instantanément par Échap; les tests de fenêtre ne détectent pas tous
les états internes de SolidWorks. Aucun test ne force le premier plan ni ne ferme un dialogue.
La rotation n'est pas automatiquement inversée : l'utilisateur garde le contrôle de la vue.

Signatures consultées dans l'aide officielle : `object ActiveDoc { get; }`,
`object ActiveView { get; }`, `int GetType()`, `bool IsOpenedViewOnly()`,
`void RotateAboutCenter(double XAngle, double YAngle)` et `void GraphicsRedraw2()`.
Le test appelle `(0, Math.PI / 36)` pour une rotation autour de l'axe écran Y.
`GraphicsRedraw2` reste documenté en 2025 mais est marqué obsolète par l'éditeur; utilisé
ici pour ce premier test de compatibilité. Aucune session StartDynamics n'est nécessaire
pour cette unique rotation.

Les contenus 2025 de `ActiveDoc`, `IsOpenedViewOnly` et `GraphicsRedraw2` ont été consultés.
Les pages 2025 de rotation/vue ont renvoyé un cadre de navigation sans contenu exploitable;
leurs signatures ont été vérifiées dans les éditions officielles antérieures ci-dessous.
Le test réel de rotation et de rafraîchissement a depuis réussi sur le poste en 33.5.0.
Cela ne valide pas encore leur performance lors d'une navigation continue.

## Étape 4 — rotation continue avec la Kensington

Extraire la nouvelle archive dans un dossier distinct. On peut y copier l'ancien `config.json` :
les anciennes configurations de diagnostic restent compatibles et utilisent les réglages par défaut.

```powershell
powershell.exe -NoProfile -STA -File .\Start-Navigation.ps1
```

1. Choisir la Kensington dans la liste (celle identifiée dans le diagnostic; `047D:80D6` sur
   le poste rapporté). Si le chemin exact était sauvegardé, il est présélectionné, mais la
   navigation reste désactivée. VID/PID seuls ne provoquent aucune sélection automatique.
2. Cliquer **Connect SolidWorks**, puis **Enable rotation**. Ouvrir une pièce ou un assemblage
   complètement chargé et revenir dans la fenêtre principale SolidWorks **sans maintenir Maj**.
3. Maintenir **Maj gauche / LeftShift**, puis faire rouler la boule. Relâcher pour arrêter le geste.
   X tourne horizontalement autour de Y écran; Y tourne verticalement autour de X écran.
4. **Échap désactive la navigation**, même dans SolidWorks, tant que la boucle UI répond.
   Cliquer de nouveau Enable rotation pour reprendre. Le bouton STOP fait la même chose.
5. Ajuster `rad/count` pour changer la sensibilité et Invert X/Y pour les sens. Toute modification
   arrête la navigation : cliquer de nouveau Enable rotation. Si c'est trop lent, essayer
   `0.004` au lieu de `0.002`. La rotation unique de l'étape précédente était volontairement
   limitée à 5°; la rotation continue n'a pas de limite angulaire totale.
6. Rester d'abord à 30 Hz. Le compteur **API updates** mesure les mises à jour ayant effectué
   rotation et rafraîchissement avec succès; il ne mesure pas les images GPU. `last COM` mesure
   la durée du traitement CAD du dernier mouvement. Les timers Windows ne garantissent pas
   la cadence demandée : relever les valeurs avant d'essayer 45 ou 60 Hz.
7. Pour conserver les réglages, cliquer **Save settings on close**, puis fermer la fenêtre.
   La sauvegarde concerne `config.json`, jamais le document CAD.

Le modificateur est configurable : LeftShift, RightShift, LeftControl ou RightControl.
Ces touches ne sont pas supprimées pour Windows et peuvent modifier les sélections/commandes
natives lorsque l'on utilise simultanément la souris. Les clics et la molette de la Kensington
restent natifs; ils ne déclenchent pas la navigation dans cette étape. Le curseur bouge toujours.

La capture déclenche uniquement un handler **C#** qui vérifie le modificateur/le premier plan
par Win32 et accumule les deltas du handle sélectionné. Aucun COM dans `WM_INPUT`. Le timer
WinForms du même STA consomme les deltas et appelle l'adaptateur CAD; sa réentrance est bloquée.
Le document et la vue sont conservés pendant un geste, puis relus pour vérifier leur identité
avant les rotations. Les paquets vieux de plus de 150 ms sont rejetés à partir du temps du
message Windows, pas seulement de sa date de traitement, avec gestion du bouclage du compteur.
Les paquets antérieurs à l'activation de la touche sont aussi écartés. La détection de la touche
est échantillonnée; elle n'est pas une reconstruction exacte de l'ordre matériel clavier/souris.

La perte de premier plan ou un dialogue empêchant l'accès à la fenêtre principale annule le
geste et exige un relâchement puis une nouvelle pression dans SolidWorks. Un changement de
périphériques enlève la sélection et désactive la navigation. Un changement de document/vue,
ou une erreur COM désactive la navigation sans retry automatique. Une mise à jour dépassant
150 ms jette les mouvements accumulés et ceux encore en file à son retour, sans désactiver
la navigation : seul un nouveau mouvement peut produire une rotation après ce délai.
Revenir à un état prêt puis réactiver, ou reconnecter si SolidWorks a été fermé. Les états internes
non modaux de SolidWorks ne sont pas tous détectables par le test de fenêtre : terminer les
commandes avant l'essai. Échap ne peut pas interrompre immédiatement un appel COM bloqué.

Les rotations par mise à jour sont plafonnées à ±0,15 rad (~8,6°) par axe pour éviter un saut
important. À grande vitesse, cette protection peut tronquer une partie du mouvement. Il n'y a
ni accélération ni lissage pour l'instant. Aucun mouvement ancien n'est rejoué pour rattraper
un retard. Les objets COM sont libérés au relâchement/changement de geste et à la fermeture.

À tester avant le déplacement ou le zoom :

- trackball seule avec/sans modificateur; souris principale seule avec le même modificateur;
- arrêt au relâchement, Échap, Alt-Tab et absence de reprise avec la touche encore maintenue;
- débranchement/rebranchement, fermeture/changement du document, dessin, dialogue modal;
- sens X/Y, sensibilité, fréquence réelle sur petit et gros assemblages, dérive du curseur;
- sauvegarde/relecture JSON, fermeture de SolidWorks et reconnexion manuelle.

## Limite du curseur — critère principal non satisfait

Raw Input distingue les sources; **la trackball continue à déplacer le curseur Windows**.
Le prototype laisse les deux périphériques fonctionner comme avant. Il ne satisfait donc
pas encore l'indépendance complète de la navigation et du curseur.

`RIDEV_NOLEGACY` n'est pas un mécanisme de suppression globale des mouvements d'une Kensington
précise dans un autre processus. Il n'est pas utilisé. Aucun hook de suppression, `ClipCursor`,
émulation de clics ou pilote n'est inclus. Un appel ponctuel à `SetCursorPos` peut désormais
ramener le curseur à la fin du geste, si l'option **Return cursor on stop** est cochée.

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
Test-SolidWorksView.ps1        test de rotation uniquement après clic sur Armer
Start-Navigation.ps1           rotation continue, désactivée au démarrage
Start-TrackballCAD.ps1         configuration, chargement, boucle UI, sauvegarde
src/RawInputBridge.cs          P/Invoke, fenêtre message-only, capture et accumulateurs
src/DiagnosticsWindow.cs       petite fenêtre diagnostic, handlers entièrement C#
src/SolidWorksProbe.cs         acquisition COM et lecture de révision, rapport par étape
src/SolidWorksViewTest.cs       test COM de vue et petite fenêtre d'armement
src/RotationEngine.cs          sélection, modificateur, deltas, sensibilité, expiration
src/SolidWorksRotationAdapter.cs  appels COM de rotation sur le STA
src/NavigationWindow.cs        activation, réglages et mesure du débit
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

État des prochaines étapes :

3. Test explicite de petite rotation **validé par retour utilisateur** sur 33.5.0.
4. Liaison boule → rotation **confirmée par retour utilisateur**, cible 30 Hz réglable
   jusqu'à 60 Hz; sensibilité/inversions et sauvegarde locale disponibles.
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
Ils couvrent aussi le filtre par périphérique du moteur de rotation, l'activation par touche,
la perte de focus, l'expiration des deltas, les inversions, le plafonnement et le JSON de rotation.
Ils ne prouvent pas l'identité des périphériques physiques ni la connexion COM réelle.

**Sur l'environnement Linux de création :** fichiers et références contrôlés statiquement;
PowerShell 5.1, Win32 et SolidWorks indisponibles. Les validations COM et rotation unique
mentionnées ici proviennent des rapports utilisateur. La nouvelle rotation continue et les
nouveaux tests n'ont pas été exécutés ici. Une CI Windows séparée est fournie dans le dépôt.

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
- [ActiveDoc, aide 2025](https://help.solidworks.com/2025/english/api/sldworksapi/SOLIDWORKS.Interop.sldworks~SOLIDWORKS.Interop.sldworks.ISldWorks~ActiveDoc.html)
- [IsOpenedViewOnly, aide 2025](https://help.solidworks.com/2025/english/api/sldworksapi/SolidWorks.Interop.sldworks~SolidWorks.Interop.sldworks.IModelDoc2~IsOpenedViewOnly.html)
- [GraphicsRedraw2, aide 2025](https://help.solidworks.com/2025/english/api/sldworksapi/SolidWorks.Interop.sldworks~SolidWorks.Interop.sldworks.IModelDoc2~GraphicsRedraw2.html)
- [ActiveView, aide 2023](https://help.solidworks.com/2023/English/api/sldworksapi/SolidWorks.Interop.sldworks~SolidWorks.Interop.sldworks.IModelDoc2~ActiveView.html)
- [RotateAboutCenter, aide 2021](https://help.solidworks.com/2021/english/api/sldworksapi/SolidWorks.Interop.sldworks~SolidWorks.Interop.sldworks.IModelView~RotateAboutCenter.html?format=P&value=)

Licence MIT. SolidWorks et ses composants ne sont pas distribués dans ce dossier.

Référence pour l’horodatage des paquets : [GetMessageTime](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getmessagetime).


## Activation sans maintenir Maj : mode Toggle

La rotation avec maintien de Maj a été confirmée sur le poste. Le relâchement arrêtait
volontairement la navigation. Cette version ajoute un choix **Hold / Toggle** dans
Start-Navigation.ps1. La configuration livrée utilise Toggle ; les anciennes configurations
sans `rotation.activationMode` conservent Hold.

1. Fermer l'ancienne fenêtre et lancer un nouveau processus :
   `powershell.exe -NoProfile -STA -File .\Start-Navigation.ps1`.
2. Sélectionner la Kensington, cliquer **Connect SolidWorks** et choisir **Toggle**.
3. Revenir dans la fenêtre principale SolidWorks, document pièce/assemblage ouvert.
4. Presser **Ctrl + Maj + F9**, relâcher les touches et déplacer la boule : rotation active.
5. Presser à nouveau **Ctrl + Maj + F9**, ou **Échap**, pour arrêter.
6. Après perte de focus, dialogue modal détecté ou erreur, réactiver explicitement avec
   le raccourci. Aucun redémarrage automatique de la rotation.
7. **Save settings on close** conserve le mode choisi dans le JSON à la fermeture.

Le raccourci est enregistré globalement seulement en mode Toggle, sans répétition automatique.
S'il est déjà réservé, un message signale l'échec : utiliser Hold ou libérer le raccourci.
Le raccourci ne démarre la rotation que dans la fenêtre principale SolidWorks. Échap et
les raccourcis dépendent de la disponibilité de la boucle de messages ; ils ne peuvent
pas interrompre un appel COM déjà bloqué. Le mode Hold reste utilisable comme avant.

**Les clics Kensington restent des clics Windows et le curseur reste partagé.**
Ne pas leur attribuer des commandes CAD supplémentaires dans ce prototype : elles
s'ajouteraient aux clics natifs. Les raccourcis clavier évitent de devoir cliquer avec
la Kensington pour activer la navigation ; ils ne suppriment pas ses clics. Aucun blocage
global des souris, repositionnement permanent du curseur ou pilote n'est ajouté.
La version cursor-return-1 ajoute uniquement un retour ponctuel en fin de geste. L'indépendance
complète demandée n'est donc toujours pas satisfaite.

Validation de cette évolution : contrôles statiques locaux uniquement. La compilation
Add-Type, l'enregistrement du raccourci, l'arrêt sur perte de focus et la bascule pendant
la rotation doivent être testés sur Windows. Les tests PowerShell couvrent aussi la
persistance du mode, les anciens fichiers et le rejet d'un mode inconnu.

Référence : [RegisterHotKey et MOD_NOREPEAT](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerhotkey).


## Correctif toggle-fix-2 : petite rotation puis arrêt

Le maintien de Maj fonctionne d'après le retour utilisateur, mais la première version
Toggle ne produisait qu'un petit déplacement sur son poste. L'utilisateur a confirmé que
l'arrêt venait du seuil de 150 ms. Le code désactivait entièrement la navigation dès qu'une
mise à jour COM dépassait ce seuil, y compris la préparation initiale du document et de la vue.

Cette version conserve l'activation après un appel lent terminé sans exception. Elle
supprime tous les deltas accumulés pendant cet appel et rejette les paquets encore en file
dont l'horodatage précède ou égale son retour. Les mouvements suivants sont traités normalement,
sans rejouer l'ancienne commande ni réinitialiser le contexte CAD. Une erreur COM continue
d'arrêter la navigation ; ni le contrôle de premier plan ni les arrêts explicites ne sont retirés.

Pour tester :

1. Fermer l'ancienne fenêtre et ouvrir un **nouveau processus PowerShell** avec
   `powershell.exe -NoProfile -STA -File .\Start-Navigation.ps1` depuis le dossier extrait.
2. Vérifier le titre de version (**toggle-fix-2**, ou **cursor-return-1** qui inclut ce correctif), sélectionner la Kensington,
   connecter SolidWorks et choisir Toggle.
3. Dans SolidWorks, presser Ctrl+Maj+F9 une fois, relâcher toutes les touches,
   puis faire rouler la boule pendant quelques secondes.
4. Vérifier que la rotation continue après un premier appel lent, sans toucher le clavier.
   Presser de nouveau le raccourci pour arrêter ; vérifier aussi Échap et le changement de fenêtre.
5. Si le problème persiste, revenir dans TrackballCAD et cliquer **Copy diagnostics**.
   Le journal conserve l'arrêt précédent même si le retour à la fenêtre cause ensuite une
   perte de focus. Il indique la réception du raccourci, les arrêts et les appels lents.
   Le même rapport est écrit dans la console à la fermeture, sans fichier automatique.

Les tests `tests/Test-Portable.ps1` ajoutent les cas de maintien de l'activation après appel
lent, de rejet de la file ancienne, de mouvement suivant accepté, de perte de focus et
arrêt explicite non annulés par le vidage, ainsi que le bouclage du compteur de temps.
Ces tests nécessitent Windows PowerShell 5.1 ; ils n'ont pas été exécutés dans l'environnement
Linux de création. Contrôles locaux : diff, intégrité JSON, références des sources et archive.


## Retour ponctuel du curseur — cursor-return-1

À la demande de l'utilisateur, **Return cursor on stop** est activé par défaut. L'objectif
est d'alterner trackball et souris : la position est lue à l'activation réelle du geste,
le curseur reste libre pendant la rotation, puis revient une seule fois au point mémorisé
lors de l'arrêt au clavier. Ce n'est ni un blocage ni une séparation des curseurs.

1. Fermer l'ancienne version et lancer un nouveau processus depuis cette archive :
   `powershell.exe -NoProfile -STA -File .\Start-Navigation.ps1`.
2. Vérifier le titre **cursor-return-1**, sélectionner la trackball et connecter SolidWorks.
   Choisir **Toggle** et laisser **Return cursor on stop** coché.
3. Dans SolidWorks, placer le curseur à un endroit reconnaissable, puis presser Ctrl+Maj+F9.
4. Relâcher les touches, orienter la vue avec la boule et arrêter de bouger la boule.
5. Presser de nouveau Ctrl+Maj+F9, ou Échap : le curseur revient à sa position de départ.
   Reprendre ensuite la souris normale. Si la boule bouge encore après ce retour, ses
   mouvements natifs déplaceront à nouveau le curseur.

En mode Hold, le relâchement du modificateur demande aussi ce retour. L'option est enregistrée
avec **Save settings on close** dans `rotation.returnCursorOnStop` (booléen JSON). Les anciens
fichiers sans cette propriété activent ce nouveau comportement ; décocher pour le désactiver.

La perte de focus, un dialogue modal détecté, une erreur, un changement de périphérique,
une modification des réglages ou la fermeture annulent le retour. Cliquer STOP dans la fenêtre
TrackballCAD change le focus et ne ramène donc pas le curseur. Utiliser le raccourci ou Échap
**dans SolidWorks** pour demander le retour. Une demande retardée de plus d'une seconde par
un blocage COM est abandonnée, sans nouvelle tentative, pour éviter un saut tardif.

Le module `src/CursorReturn.cs` contient l'état de ce retour et les déclarations Win32.
La capture lit la position à la transition vers Rotate ; elle ne la modifie jamais.
Le timer STA consomme la demande une seule fois après l'arrêt et vérifie encore le premier
plan avant d'appeler SetCursorPos. Le journal indique la capture, le retour ou son échec.
Aucun appel de repositionnement n'est exécuté continuellement pendant la navigation.

**Limites :** les clics restent actifs et le curseur peut sortir de la zone du modèle pendant
la rotation. Ne pas utiliser la souris normale simultanément si son déplacement doit être
conservé : le retour remet le point initial des deux périphériques. Les restrictions de bureau
ou un confinement du curseur imposé par une autre application peuvent empêcher le retour exact ;
aucun changement de droits, de bureau ou de confinement n'est tenté.

Validation locale : contrôles statiques et archive. Les tests PowerShell ajoutés vérifient
le retour unique, les coordonnées négatives, l'annulation, l'expiration et la configuration,
sans déplacer le vrai curseur. Ils restent à exécuter sur Windows avec
`powershell.exe -NoProfile -STA -File .\tests\Test-Portable.ps1`.
Valider manuellement le retour avec Ctrl+Maj+F9 et Échap, son absence après Alt+Tab, l'option
décochée et, si applicable, plusieurs écrans avec des facteurs d'échelle différents.

Références Microsoft : [GetCursorPos](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getcursorpos),
[SetCursorPos](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setcursorpos).
