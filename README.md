# TrackballCAD — prototype Windows x64

> **Poste d'école sans Visual Studio : utiliser [TrackballCAD Portable](portable/README.md).**
> Cette variante autonome se trouve dans `portable/` : Windows PowerShell 5.1 x64 STA,
> compilation à la demande par Add-Type, aucun add-in ni enregistrement administrateur.
> Commencer par `portable/Check-Environment.ps1`, puis `portable/Start-TrackballCAD.ps1`.
> Elle propose le diagnostic, la capture et un prototype de rotation continue; les instructions ci-dessous
> concernent l'ancienne variante Visual Studio et ne s'appliquent pas au poste d'école.

TrackballCAD transforme une deuxième souris ou trackball en contrôleur de navigation CAD dédié.

**État : MVP expérimental implémenté, non compilé et non validé sur Windows/SolidWorks.**
Le dépôt initial a été créé sur Linux sans SDK .NET ni SolidWorks. Le critère des deux mains
réellement indépendantes n'est **pas encore satisfait** : Raw Input distingue les souris,
mais n'empêche pas leur contribution au curseur Windows. Le confinement expérimental
contraint les deux souris pendant le geste. Ne pas considérer ce prototype comme un
remplacement fonctionnel de SpaceMouse.

```text
                   ┌───────────────────┐
Normal Mouse ─────►│ Windows / CAD UI  │
                   └───────────────────┘

                   ┌───────────────────┐
Kensington ───────►│ Windows Raw Input │  processus TrackballCAD.UI
                   └─────────┬─────────┘
                             ▼
                   ┌───────────────────┐
                   │ Navigation Engine │  accumulation / activation
                   └─────────┬─────────┘
                             │ canal nommé local, trames échantillonnées
                             ▼
                   ┌───────────────────┐
                   │ SolidWorks API    │  add-in, thread STA SolidWorks
                   └───────────────────┘
```

La Kensington continue aussi d'alimenter le curseur Windows (branche non représentée).
Aucune simulation clavier/souris, aucun driver, aucun `RIDEV_NOLEGACY` global.

## Solution et dépendances

| Projet | Type / dépendances | Rôle |
|---|---|---|
| `TrackballCAD.Input` | bibliothèque net48, WinForms, user32 | fenêtre Raw Input, liste, identité, événements |
| `TrackballCAD.Navigation` | bibliothèque net48, BCL uniquement | activation, accumulation, protocole, `ICadAdapter` limité à la rotation |
| `TrackballCAD.Settings` | bibliothèque net48 | paramètres de session du MVP |
| `TrackballCAD.UI` | exécutable WinForms, Input + Navigation + Settings | sélection, diagnostic, émetteur IPC, confinement optionnel |
| `TrackballCAD.SolidWorks` | bibliothèque COM net48, Navigation, WinForms, interops SDK | `ISwAddin`, récepteur IPC, rotation sur le STA |
| `TrackballCAD.Tests` | console x64, Input + Navigation + Settings | tests sans framework tiers, sans matériel |

Tout cible **.NET Framework 4.8 / x64 / C# 7.3**. Aucun paquet NuGet applicatif.
Les interops SolidWorks propriétaires restent sur la machine du développeur; ne pas les
committer. Pan, zoom, roll, profils, accélération et persistance sont volontairement absents.

## Compiler sous Windows

**Premier lancement : ne pas utiliser `Add-AppxPackage` ni `Add-AppPackage`.** Ce projet
ne produit pas de paquet `.appx` ou `.msix`. Le diagnostic est un `.exe` classique et
le complément SolidWorks est une DLL COM.

Après installation des prérequis ci-dessous, ouvrir PowerShell dans le dossier contenant
`TrackballCAD.sln`, puis lancer :

```powershell
.\scripts\Start-Diagnostics.ps1
```

Ce script trouve MSBuild (dans le PATH ou via Visual Studio Installer), compile le diagnostic
et les tests, exécute les tests puis ouvre la fenêtre. Il ne demande aucun chemin de paquet
et ne nécessite ni droits administrateur ni SDK SolidWorks. Si la compilation échoue,
il s'arrête en conservant les messages MSBuild. Les chemins sont calculés depuis le script,
pas depuis le répertoire courant. Utiliser `-Configuration Release` pour une compilation Release.

Installer Visual Studio avec « Développement Desktop .NET », le SDK .NET utilisé par
les projets au format SDK et le targeting/developer pack .NET Framework 4.8.
Pour l'add-in : installation SolidWorks x64 et API SDK correspondant à la version à tester.

Depuis un **Developer PowerShell for Visual Studio**, à la racine du dépôt :

```powershell
# Étape 1 : diagnostic autonome, sans SolidWorks ni ses interops
msbuild src/TrackballCAD.UI/TrackballCAD.UI.csproj /restore /p:Configuration=Debug /p:Platform=x64
.\src\TrackballCAD.UI\bin\Debug\net48\TrackballCAD.UI.exe

# Tests du noyau, sans SolidWorks
msbuild tests/TrackballCAD.Tests/TrackballCAD.Tests.csproj /restore /p:Configuration=Debug /p:Platform=x64
.\tests\TrackballCAD.Tests\bin\Debug\net48\TrackballCAD.Tests.exe

# Étape 2 : solution complète; adapter le chemin à l'installation réelle
msbuild TrackballCAD.sln /restore /p:Configuration=Debug /p:Platform=x64 /p:SolidWorksInteropPath="C:\Program Files\SOLIDWORKS Corp\SOLIDWORKS\api\redist"
```

Autre option : créer `Directory.Build.local.props` (ignoré par Git) :

```xml
<Project>
  <PropertyGroup>
    <SolidWorksInteropPath>C:\Program Files\SOLIDWORKS Corp\SOLIDWORKS\api\redist</SolidWorksInteropPath>
  </PropertyGroup>
</Project>
```

Puis ouvrir `TrackballCAD.sln` dans Visual Studio, configuration `Debug | x64`.
Le dossier doit contenir `SolidWorks.Interop.sldworks.dll` et
`SolidWorks.Interop.swpublished.dll`. Un contrôle MSBuild explique leur absence.

## Installer et essayer l'add-in

1. Fermer SolidWorks. Dans un PowerShell Windows **administrateur**, exécuter
   `.\scripts\Register-Addin.ps1`. Le script appelle **RegAsm Framework64** avec `/codebase`
   et inscrit le complément dans HKLM. Garder toutes les DLL du répertoire de sortie ensemble.
   L'assembly de développement n'est pas signé; l'avertissement `/codebase` est attendu.
   Le script vérifie la DLL, ses dépendances, la fermeture de SolidWorks et les droits
   administrateur avant l'enregistrement. Pour une compilation Release, ajouter
   `-Configuration Release`; aucun paramètre `Path` n'est nécessaire.
2. Ouvrir SolidWorks; activer **TrackballCAD** dans Outils → Compléments. Le chargement
   automatique n'est pas activé par le script. Ouvrir une pièce ou un assemblage éditable.
3. Lancer `TrackballCAD.UI.exe` avec le même compte Windows. La fenêtre propose le PID
   s'il n'y a qu'une instance `SLDWORKS`; sinon relever le PID voulu dans le Gestionnaire des tâches.
4. Bouger chaque souris séparément et comparer les lignes du diagnostic. Choisir le chemin
   correspondant à la trackball; les noms HID sont génériques, pas des noms commerciaux garantis.
5. Cliquer **Connect**, vérifier l'état connecté, cocher **Enable held-button rotation**.
   Le bouton d'activation par défaut est **4 (X1)**; le remplacer si le matériel n'en dispose pas.
6. Revenir dans SolidWorks. Maintenir le bouton choisi et tourner la boule : X produit la rotation
   horizontale, Y la verticale. Relâcher pour terminer. Le diagnostic affiche les deltas,
   la molette signée, les transitions de boutons et le nombre de rotations appliquées par seconde.
7. Comparer sans et avec **Experimental cursor lock**. Le verrouillage bloque aussi la souris
   principale; un événement d'une autre souris annule le geste. Échap, perte de focus,
   déconnexion, fermeture et limite de cinq secondes annulent aussi le confinement.
8. Après annulation, relâcher puis presser de nouveau le bouton. Après rebranchement,
   sélectionner de nouveau le périphérique. La reconnexion IPC est automatique.

Les boutons et la molette restent visibles par Windows/SolidWorks : ils peuvent déclencher
une action native. Le MVP ne supprime aucun clic. Choisir un bouton sans commande conflictuelle
et relever ce comportement dans le compte rendu de test.

Désinstallation, SolidWorks fermé, PowerShell administrateur :

```powershell
.\scripts\Register-Addin.ps1 -Unregister
```

Fermer SolidWorks avant recompilation de l'add-in. Pour déboguer, attacher Visual Studio
au processus `SLDWORKS.exe` avec le débogueur .NET Framework. Les erreurs COM sont émises
via `Trace`; les erreurs Raw Input s'affichent dans le diagnostic.

## Validation et limites

Suivre [le protocole de validation](docs/validation.md) dans l'ordre : identification indépendante,
rotation réelle, puis essai des deux mains. Consigner la version SolidWorks, le matériel,
les actions parasites des boutons, la fréquence mesurée et les effets sur le curseur.
**N'ajouter aucune navigation avancée tant que ces essais ne sont pas validés.**

Les timers WinForms sont opportunistes : intervalle demandé 16 ms, plafond défensif 120 Hz,
pas de garantie temps réel ni de 60 Hz sur un gros assemblage. Le diagnostic mesure les
rotations API effectivement appliquées, pas les images rendues par le GPU. Le processus
séparé ajoute une cadence d'échantillonnage et de la latence à mesurer.

Le pipeline ignore les périphériques absolus, les dessins et les documents en lecture seule
« view-only ». Les boîtes modales, changements de documents et gestionnaires de commandes
SolidWorks doivent être testés; le contrôle de premier plan actuel vérifie le processus,
pas tous les états d'interaction internes de SolidWorks.

## Documentation

- [Architecture, risques et étapes](docs/architecture.md)
- [Raw Input et curseur partagé](docs/raw-input.md)
- [Intégration SolidWorks](docs/solidworks-integration.md)
- [Identification des périphériques](docs/device-detection.md)
- [Moteur de navigation](docs/navigation-engine.md)
- [Protocole de validation Windows](docs/validation.md)

La CI Windows compile le diagnostic et exécute les tests du noyau. Elle ne compile pas
l'add-in sans SDK propriétaire et ne valide ni le matériel ni SolidWorks.

## Licence

MIT, voir [LICENSE](LICENSE). Les bibliothèques SolidWorks ne sont pas incluses et restent
soumises aux conditions de leur éditeur.
