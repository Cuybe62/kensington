# Add-in SolidWorks

`SolidWorksAddin` est une classe COM publique à GUID stable, `ClassInterface.None`, qui implémente
`ISwAddin`. `ConnectToSW` reçoit la session réelle : aucune recherche dans la Running Object Table,
aucune ambiguïté multi-instance. Chaque instance expose un canal dont le nom contient son PID.
Aucun menu de rappel n'est enregistré; l'interface de configuration réside dans l'exécutable séparé.

Le timer WinForms est créé dans `ConnectToSW`, avec vérification du STA. Le thread IPC ne conserve
aucun objet SolidWorks. `SolidWorksAdapter` vérifie son thread propriétaire à chaque accès.
`IActiveDoc2` et `IActiveView` sont relus sur le STA; aucun document → aucune rotation.
Dessins et view-only sont exclus du MVP. Au changement de document/vue, la trame en attente est jetée
et le geste doit se terminer avant réactivation. Un garde empêche la réentrance du timer.

`RotateAboutCenter(verticalRadians, horizontalRadians)` tourne autour des axes écran X/Y.
L'ordre diffère des axes d'entrée : un déplacement horizontal tourne autour de Y.
`StartDynamics`/`StopDynamics` encadrent chaque rotation, avec un finally; conserver une session
dynamique plus longue est une optimisation à évaluer après validation. Le rendu réel et son coût
restent à mesurer; le compteur mesure les appels réussis, pas les images GPU.

Les erreurs COM annulent les données en attente, rendent l'adaptateur indisponible et sont tracées.
Aucun retry des mouvements échoués. Au déchargement : timer arrêté, canal fermé, worker stoppé et
références abandonnées. Pas de `FinalReleaseComObject` sur les objets partagés fournis par l'hôte.

La validation doit inclure boîtes modales, esquisses, fermeture/changement de document et sortie de
SolidWorks. Vérifier que le seul test du processus au premier plan est assez restrictif pour les
commandes utilisées; le MVP ne connaît pas tous les états internes de l'application.

## Compilation et enregistrement

Voir README pour `SolidWorksInteropPath`, MSBuild et `Register-Addin.ps1`. Les références SDK ont
`EmbedInteropTypes=false`, sont copiées avec l'add-in et ne sont pas distribuées dans le dépôt.
La CI sans SolidWorks ne valide pas leur compatibilité. Tester la version installée avant toute
annonce de compatibilité multi-version. RegAsm x64 et HKLM demandent une console administrateur.
Le script ne modifie ni pilotes ni associations de périphériques.

Sources officielles utilisées :
- [ISwAddin et enregistrement](https://help.solidworks.com/2021/english/api/sldworksapiprogguide/Overview/Using_SwAddin_to_Create_a_SolidWorks_Addin.htm)
- [IActiveDoc2](https://help.solidworks.com/2021/english/api/sldworksapi/SolidWorks.Interop.sldworks~SolidWorks.Interop.sldworks.ISldWorks~IActiveDoc2.html)
- [IActiveView](https://help.solidworks.com/2023/English/api/sldworksapi/SolidWorks.Interop.sldworks~SolidWorks.Interop.sldworks.IModelDoc2~IActiveView.html)
- [RotateAboutCenter](https://help.solidworks.com/2022/english/api/sldworksapi/SolidWorks.Interop.sldworks~SolidWorks.Interop.sldworks.IModelView~RotateAboutCenter.html)
- [StartDynamics](https://help.solidworks.com/2023/English/api/sldworksapi/SolidWorks.Interop.sldworks~SolidWorks.Interop.sldworks.IModelView~StartDynamics.html)
