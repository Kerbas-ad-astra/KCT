﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using KSP;

namespace KerbalConstructionTime
{
    [KSPAddon(KSPAddon.Startup.TrackingStation, false)]
    public class KCT_Tracking_Station : KerbalConstructionTime
    {

    }

    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class KCT_Flight : KerbalConstructionTime
    {

    }

    [KSPAddon(KSPAddon.Startup.SpaceCentre, false)]
    public class KCT_SpaceCenter : KerbalConstructionTime
    {
        
    }

    [KSPAddon(KSPAddon.Startup.EditorAny, false)]
    public class KCT_Editor : KerbalConstructionTime
    {

    }

   /* [KSPAddon(KSPAddon.Startup.EditorSPH, false)]
    public class KCT_SPHEditor : KerbalConstructionTime
    {

    }*/

    public class KerbalConstructionTimeData : ScenarioModule
    {
        public override void OnSave(ConfigNode node)
        {
           // Boolean error = false;
            KCTDebug.Log("Writing to persistence.");
            base.OnSave(node);
            KCT_DataStorage kctVS = new KCT_DataStorage();
            node.AddNode(kctVS.AsConfigNode());
            foreach (KCT_KSC KSC in KCT_GameStates.KSCs)
            {
                if (KSC != null && KSC.KSCName != null && KSC.KSCName.Length > 0)
                    node.AddNode(KSC.AsConfigNode());
            }
            ConfigNode tech = new ConfigNode("TechList");
            foreach (KCT_TechItem techItem in KCT_GameStates.TechList)
            {
                KCT_TechStorageItem techNode = new KCT_TechStorageItem();
                techNode.FromTechItem(techItem);
                ConfigNode cnTemp = new ConfigNode("Tech");
                cnTemp = ConfigNode.CreateConfigFromObject(techNode, cnTemp);
                ConfigNode protoNode = new ConfigNode("ProtoNode");
                techItem.protoNode.Save(protoNode);
                cnTemp.AddNode(protoNode);
                tech.AddNode(cnTemp);
            }
            node.AddNode(tech);
        }
        public override void OnLoad(ConfigNode node)
        {
            KCTDebug.Log("Reading from persistence.");
            base.OnLoad(node);
            KCT_GameStates.KSCs.Clear();
            KCT_GameStates.ActiveKSC = null;
            //KCT_Utilities.SetActiveKSC("Stock");
            KCT_GameStates.TechList.Clear();
            KCT_GameStates.TechUpgradesTotal = 0;

            KCT_DataStorage kctVS = new KCT_DataStorage();
            ConfigNode CN = node.GetNode(kctVS.GetType().Name);
            if (CN != null)
                ConfigNode.LoadObjectFromConfig(kctVS, CN);

            foreach (ConfigNode ksc in node.GetNodes("KSC"))
            {
                string name = ksc.GetValue("KSCName");
                KCT_KSC loaded_KSC = new KCT_KSC(name);
                loaded_KSC.FromConfigNode(ksc);
                if (loaded_KSC != null && loaded_KSC.KSCName != null && loaded_KSC.KSCName.Length > 0)
                {
                    loaded_KSC.RDUpgrades[1] = KCT_GameStates.TechUpgradesTotal;
                    if (KCT_GameStates.KSCs.Find(k => k.KSCName == loaded_KSC.KSCName) == null)
                        KCT_GameStates.KSCs.Add(loaded_KSC);
                }
            }
            //KCT_Utilities.SetActiveKSCToRSS();
            KCT_Utilities.SetActiveKSC(KCT_GameStates.activeKSCName);

            
            ConfigNode tmp = node.GetNode("TechList");
            if (tmp != null)
            {
                foreach (ConfigNode techNode in tmp.GetNodes("Tech"))
                {
                    KCT_TechStorageItem techStorageItem = new KCT_TechStorageItem();
                    ConfigNode.LoadObjectFromConfig(techStorageItem, techNode);
                    KCT_TechItem techItem = techStorageItem.ToTechItem();
                    techItem.protoNode = new ProtoTechNode(techNode.GetNode("ProtoNode"));
                    KCT_GameStates.TechList.Add(techItem);
                }
            }
            //KCT_GameStates.ActiveKSC.AsConfigNode().Save(KSPUtil.ApplicationRootPath + "/KSC.node");

            KerbalConstructionTime.DelayedStart();
            KCT_GUI.CheckToolbar();
            KCT_GameStates.erroredDuringOnLoad.OnLoadFinish();
        }
    }

    public class KerbalConstructionTime : MonoBehaviour
    {
        public bool editorRecalcuationRequired;
        public int updateRateThrottle;

        public static MonoBehaviour instance;
        internal KerbalConstructionTime()
        {
            instance = this;

            KCT_GameStates.settings.Load(); //Load the settings file, if it exists

            string SavedFile = KSPUtil.ApplicationRootPath + "/saves/" + HighLogic.SaveFolder + "/KCT_Settings.cfg";
            if (!System.IO.File.Exists(SavedFile))
            {
                KCT_GameStates.firstStart = true;
            }

            if (KCT_PresetManager.Instance == null)
            {
                KCT_PresetManager.Instance = new KCT_PresetManager();
            }
            KCT_PresetManager.Instance.SetActiveFromSaveData();

            if (ToolbarManager.ToolbarAvailable && ToolbarManager.Instance != null && KCT_GameStates.settings.PreferBlizzyToolbar)
            {
                KCTDebug.Log("Adding Toolbar Button");
                KCT_GameStates.kctToolbarButton = ToolbarManager.Instance.add("Kerbal_Construction_Time", "MainButton");
                if (KCT_GameStates.kctToolbarButton != null)
                {
                    if (KCT_PresetManager.PresetLoaded() && !KCT_PresetManager.Instance.ActivePreset.generalSettings.Enabled) KCT_GameStates.kctToolbarButton.Visibility = new GameScenesVisibility(GameScenes.SPACECENTER);
                    else KCT_GameStates.kctToolbarButton.Visibility = new GameScenesVisibility(new GameScenes[] { GameScenes.SPACECENTER, GameScenes.FLIGHT, GameScenes.TRACKSTATION, GameScenes.EDITOR });
                    KCT_GameStates.kctToolbarButton.TexturePath = KCT_Utilities.GetButtonTexture();
                    KCT_GameStates.kctToolbarButton.ToolTip = "Kerbal Construction Time";
                    KCT_GameStates.kctToolbarButton.OnClick += ((e) =>
                    {
                        KCT_GUI.ClickToggle();
                    });
                }
            }
        }

        

        public void OnDestroy()//more toolbar stuff
        {
            if (KCT_GameStates.kctToolbarButton != null)
            {
                KCT_GameStates.kctToolbarButton.Destroy();
            }
            if (KCT_Events.instance.KCTButtonStock != null)
            {
                ApplicationLauncher.Instance.RemoveModApplication(KCT_Events.instance.KCTButtonStock);
            }

            KCT_GUI.guiDataSaver.Save();
        }

        public void Awake()
        {
            RenderingManager.AddToPostDrawQueue(0, OnDraw);
        }

        private void OnDraw()
        {
            KCT_GUI.SetGUIPositions(OnWindow);
        }

        private void OnWindow(int windowID)
        {
            KCT_GUI.DrawGUIs(windowID);
        }

        public void Start()
        {
            KCT_GameStates.settings.Save(); //Save the settings file, with defaults if it doesn't exist
            KCT_PresetManager.Instance.SaveActiveToSaveData();

           /* KCT_GameStates.timeSettings.Load(); //Load the time settings
            KCT_GameStates.timeSettings.Save(); //Save the time settings
            UpdateOldFormulaCFG(); //Update the formula cfg file to the new format so things aren't broken
            KCT_GameStates.formulaSettings.Load();
            KCT_GameStates.formulaSettings.Save();*/

            // Ghetto event queue
            if (HighLogic.LoadedScene == GameScenes.EDITOR)
            {
                InvokeRepeating("EditorRecalculation", 1, 1);
            }
            
            //Code for saving to the persistence.sfs
            ProtoScenarioModule scenario = HighLogic.CurrentGame.scenarios.Find(s => s.moduleName == typeof(KerbalConstructionTimeData).Name);
            if (scenario == null)
            {
                try
                {
                    Debug.Log("[KCT] Adding InternalModule scenario to game '" + HighLogic.CurrentGame.Title + "'");
                    HighLogic.CurrentGame.AddProtoScenarioModule(typeof(KerbalConstructionTimeData), new GameScenes[] {GameScenes.FLIGHT, GameScenes.SPACECENTER, GameScenes.EDITOR, GameScenes.TRACKSTATION});
                    // the game will add this scenario to the appropriate persistent file on save from now on
                }
                catch (ArgumentException ae)
                {
                    Debug.LogException(ae);
                }
                catch
                {
                    Debug.Log("[KCT] Unknown failure while adding scenario.");
                }
            }
            else
            {
                if (!scenario.targetScenes.Contains(GameScenes.SPACECENTER))
                    scenario.targetScenes.Add(GameScenes.SPACECENTER);
                if (!scenario.targetScenes.Contains(GameScenes.FLIGHT))
                    scenario.targetScenes.Add(GameScenes.FLIGHT);
                if (!scenario.targetScenes.Contains(GameScenes.EDITOR))
                    scenario.targetScenes.Add(GameScenes.EDITOR);
                if (!scenario.targetScenes.Contains(GameScenes.TRACKSTATION))
                    scenario.targetScenes.Add(GameScenes.TRACKSTATION);

            }
            //End code for persistence.sfs

            if (KCT_GUI.PrimarilyDisabled)
            {
                if (InputLockManager.GetControlLock("KCTLaunchLock") == ControlTypes.EDITOR_LAUNCH)
                    InputLockManager.RemoveControlLock("KCTLaunchLock");
            }

            KACWrapper.InitKACWrapper();

            if (!KCT_Events.instance.eventAdded)
            {
                KCT_Events.instance.addEvents();
            }

            if (!KCT_PresetManager.Instance.ActivePreset.generalSettings.Enabled)
            {
                if (InputLockManager.GetControlLock("KCTKSCLock") == ControlTypes.KSC_FACILITIES)
                    InputLockManager.RemoveControlLock("KCTKSCLock");
                return;
            }

            //Begin primary mod functions

            KCT_GameStates.UT = Planetarium.GetUniversalTime();

           /* List<GameScenes> validScenes = new List<GameScenes> { GameScenes.SPACECENTER, GameScenes.TRACKSTATION, GameScenes.EDITOR };
            if (validScenes.Contains(HighLogic.LoadedScene) && System.IO.File.Exists(KSPUtil.ApplicationRootPath + "saves/" + HighLogic.SaveFolder + "/KCT_simulation_backup.sfs"))
            {
                KCT_Utilities.LoadSimulationSave();
            }*/

            KCT_GUI.guiDataSaver.Load();
            if (!HighLogic.LoadedSceneIsFlight && KCT_GameStates.flightSimulated)
                KCT_GameStates.reset();

            if (HighLogic.LoadedSceneIsEditor)
            {
                if (KCT_GUI.showSimulationCompleteEditor)
                {
                    KCT_GUI.hideAll();
                    KCT_GUI.showSimulationCompleteEditor = true;
                }
                else
                {
                    KCT_GUI.hideAll();
                }
                if (!KCT_GUI.PrimarilyDisabled)
                {
                    KCT_GUI.showEditorGUI = KCT_GameStates.showWindows[1];
                }
                if (KCT_GameStates.EditorShipEditingMode && KCT_GameStates.delayStart)
                {
                    KCT_GameStates.delayStart = false;
                    EditorLogic.fetch.shipNameField.Text = KCT_GameStates.editedVessel.shipName;
                }
            }
            else if (HighLogic.LoadedScene == GameScenes.SPACECENTER)
            {
               /* if (System.IO.File.Exists(KSPUtil.ApplicationRootPath + "saves/" + HighLogic.SaveFolder + "/KCT_simulation_backup.sfs"))
                {
                    KCT_Utilities.LoadSimulationSave();
                }*/
                KCT_GUI.hideAll();
            }

            if (HighLogic.LoadedSceneIsFlight && !KCT_GameStates.flightSimulated && FlightGlobals.ActiveVessel.situation == Vessel.Situations.PRELAUNCH)
            {
                if (FlightGlobals.ActiveVessel.GetCrewCount() == 0 && KCT_GameStates.launchedCrew.Count > 0)
                {
                    KerbalRoster roster = HighLogic.CurrentGame.CrewRoster;

                    /*  foreach (CrewedPart c in KCT_GameStates.launchedCrew)
                      {
                          KCTDebug.Log(c.partID);
                      }*/

                    for (int i = 0; i < FlightGlobals.ActiveVessel.parts.Count; i++)
                    {
                        Part p = FlightGlobals.ActiveVessel.parts[i];
                        //KCTDebug.Log("craft: " + p.craftID);
                        {
                            CrewedPart cP = KCT_GameStates.launchedCrew.Find(part => part.partID == p.craftID);
                            if (cP == null) continue;
                            List<ProtoCrewMember> crewList = cP.crewList;
                            foreach (ProtoCrewMember crewMember in crewList)
                            {
                                if (crewMember != null)
                                {
                                    ProtoCrewMember finalCrewMember = crewMember;
                                    if (crewMember.type == ProtoCrewMember.KerbalType.Crew)
                                    {
                                        finalCrewMember = roster.Crew.FirstOrDefault(c => c.name == crewMember.name);
                                    }
                                    else if (crewMember.type == ProtoCrewMember.KerbalType.Tourist)
                                    {
                                        finalCrewMember = roster.Tourist.FirstOrDefault(c => c.name == crewMember.name);
                                    }
                                    if (finalCrewMember == null)
                                    {
                                        Debug.LogError("Error when assigning " + crewMember.name + " to " + p.partInfo.name +". Cannot find Kerbal in list.");
                                        continue;
                                    }
                                    try
                                    {
                                        KCTDebug.Log("Assigning " + finalCrewMember.name + " to " + p.partInfo.name);
                                        if (p.AddCrewmember(finalCrewMember))//p.AddCrewmemberAt(finalCrewMember, crewList.IndexOf(crewMember)))
                                        {
                                            finalCrewMember.rosterStatus = ProtoCrewMember.RosterStatus.Assigned;
                                            if (finalCrewMember.seat != null)
                                                finalCrewMember.seat.SpawnCrew();
                                        }
                                        else
                                        {
                                            Debug.LogError("Error when assigning " + crewMember.name + " to " + p.partInfo.name);
                                            finalCrewMember.rosterStatus = ProtoCrewMember.RosterStatus.Available;
                                            continue;
                                        }
                                    }
                                    catch
                                    {
                                        Debug.LogError("Error when assigning " + crewMember.name + " to " + p.partInfo.name);
                                        finalCrewMember.rosterStatus = ProtoCrewMember.RosterStatus.Available;
                                        continue;
                                    }
                                }
                            }
                        }
                    }
                    KCT_GameStates.launchedCrew.Clear();
                }
            }
            KCT_GameStates.erroredDuringOnLoad.OnLoadStart();
        }

        private void EditorRecalculation()
        {
            if (editorRecalcuationRequired && !KCT_GUI.PrimarilyDisabled)
            {
                KCT_Utilities.RecalculateEditorBuildTime(EditorLogic.fetch.ship);
                editorRecalcuationRequired = false;
            }
        }

        public static bool moved = false;
        private static bool updateChecked = false;
        public void FixedUpdate()
        {
            #if DEBUG
            if (!updateChecked && KCT_GameStates.settings.CheckForDebugUpdates && !KCT_GameStates.firstStart)
            {
                KCT_UpdateChecker.CheckForUpdate(false, false);
                updateChecked = true;
            }
            #endif

            if (!KCT_PresetManager.Instance.ActivePreset.generalSettings.Enabled)
                return;

            if (!KCT_GameStates.erroredDuringOnLoad.AlertFired && KCT_GameStates.erroredDuringOnLoad.HasErrored())
            {
                KCT_GameStates.erroredDuringOnLoad.FireAlert();
            }
            if (KCT_GameStates.LoadingSimulationSave)
            {
                KCT_Utilities.LoadSimulationSave(true);
            }
            
            KCT_GameStates.UT = Planetarium.GetUniversalTime();
            try
            {
                if (!KCT_GUI.PrimarilyDisabled && (HighLogic.LoadedScene == GameScenes.FLIGHT || HighLogic.LoadedScene == GameScenes.SPACECENTER || HighLogic.LoadedScene == GameScenes.TRACKSTATION && !KCT_GameStates.flightSimulated))
                {
                    IKCTBuildItem ikctItem = KCT_Utilities.NextThingToFinish();
                    if (KCT_GameStates.targetedItem == null && ikctItem != null) KCT_GameStates.targetedItem = ikctItem;
                    if (KCT_GameStates.canWarp && ikctItem != null && !ikctItem.IsComplete())
                    {
                        int warpRate = TimeWarp.CurrentRateIndex;
                        if (SOIAlert())
                        {
                            TimeWarp.SetRate(0, true);
                            KCT_GameStates.canWarp = false;
                            KCT_GameStates.warpInitiated = false;

                        }
                        else if (warpRate < KCT_GameStates.lastWarpRate) //if something else changes the warp rate then release control to them, such as Kerbal Alarm Clock
                        {
                            KCT_GameStates.canWarp = false;
                            KCT_GameStates.lastWarpRate = 0;
                        }
                        else
                        {
                            if (ikctItem == KCT_GameStates.targetedItem && (10 * TimeWarp.deltaTime) > Math.Max((ikctItem.GetTimeLeft()), 0) && TimeWarp.CurrentRate > 1.0f)
                            {
                                TimeWarp.SetRate(--warpRate, true);
                            }
                            else if (warpRate == 0 && KCT_GameStates.warpInitiated)
                            {
                                KCT_GameStates.canWarp = false;
                                KCT_GameStates.warpInitiated = false;

                            }
                            KCT_GameStates.lastWarpRate = warpRate;
                        }

                    }
                    else if (ikctItem != null && ikctItem == KCT_GameStates.targetedItem && (KCT_GameStates.warpInitiated || KCT_GameStates.settings.ForceStopWarp) && TimeWarp.CurrentRate != 0 && (ikctItem.GetTimeLeft()) < (TimeWarp.deltaTime * 2) && (!ikctItem.IsComplete())) //Still warp down even if we don't control the clock
                    {
                        TimeWarp.SetRate(0, false);
                        KCT_GameStates.warpInitiated = false;
                    }
                    else if (ikctItem != null && (KCT_GameStates.settings.ForceStopWarp) && TimeWarp.CurrentRate != 0 &&  (!ikctItem.IsComplete()))
                    {
                        if ((10 * TimeWarp.deltaTime) > Math.Max((ikctItem.GetTimeLeft()), 0) && TimeWarp.CurrentRate > 1.0f)
                        {
                            TimeWarp.SetRate(TimeWarp.CurrentRateIndex-1, true);
                        }
                    }
                }

                if (HighLogic.LoadedScene == GameScenes.FLIGHT && KCT_GameStates.flightSimulated) //Simulated flights
                {
                    if (FlightGlobals.ActiveVessel.loaded && !FlightGlobals.ActiveVessel.packed && !moved)
                    {
                        //moved = true;
                        int secondsForMove = 3;
                        if (KCT_GameStates.simulateInOrbit && loadDeferTime == DateTime.MaxValue)
                        {
                            loadDeferTime = DateTime.Now;
                        }
                        else if (KCT_GameStates.simulateInOrbit && (!KCT_GameStates.delayMove || DateTime.Now.CompareTo(loadDeferTime.AddSeconds(secondsForMove)) > 0))
                        {
                            KCTDebug.Log("Moving vessel to orbit. " + KCT_GameStates.simulationBody.bodyName + ":" + KCT_GameStates.simOrbitAltitude + ":" + KCT_GameStates.simInclination);
                            KCT_OrbitAdjuster.PutInOrbitAround(KCT_GameStates.simulationBody, KCT_GameStates.simOrbitAltitude, KCT_GameStates.simInclination);
                            moved = true;
                            loadDeferTime = DateTime.MaxValue;
                        }
                        else if (!KCT_GameStates.simulateInOrbit)
                            moved = true;

                        if (KCT_GameStates.simulateInOrbit && loadDeferTime != DateTime.MaxValue && lastSeconds != (loadDeferTime.AddSeconds(secondsForMove) - DateTime.Now).Seconds)
                        {
                            double remaining = (loadDeferTime.AddSeconds(secondsForMove) - DateTime.Now).TotalSeconds;
                            ScreenMessages.PostScreenMessage("[KCT] Moving vessel in " + Math.Round(remaining) + " seconds", (float)(remaining - Math.Floor(remaining)), ScreenMessageStyle.UPPER_CENTER);
                            lastSeconds = (int)remaining;
                        }
                    }
                    if (KCT_GameStates.simulationEndTime > 0 && KCT_GameStates.UT >= KCT_GameStates.simulationEndTime)
                    {
                        FlightDriver.SetPause(true);
                        KCT_GUI.showSimulationCompleteFlight = true;
                    }
                    if (FlightGlobals.ActiveVessel.situation != Vessel.Situations.PRELAUNCH && KCT_GameStates.simulationEndTime == 0 && KCT_GameStates.simulationTimeLimit > 0)
                    {
                        KCT_GameStates.simulationEndTime = Planetarium.GetUniversalTime() + KCT_GameStates.simulationTimeLimit; //Just in case the event doesn't fire
                    }
                }

                if (HighLogic.LoadedScene == GameScenes.TRACKSTATION)
                {
                    KCT_Utilities.SetActiveKSCToRSS();
                }

                if (!KCT_GUI.PrimarilyDisabled && HighLogic.LoadedScene == GameScenes.SPACECENTER)
                {
                    if (VesselSpawnDialog.Instance.Visible)
                    {
                        POINTER_INFO ptr = new POINTER_INFO();
                        ptr.evt = POINTER_INFO.INPUT_EVENT.TAP;
                        VesselSpawnDialog.Instance.ButtonClose(ref ptr);
                        KCTDebug.Log("Attempting to close spawn dialog!");
                    }
                }

               /* if (!HighLogic.LoadedSceneIsFlight && KCT_GameStates.recoveredVessel != null)
                {
                    InputLockManager.SetControlLock(ControlTypes.All, "KCTPopupLock");
                    DialogOption[] options = new DialogOption[3];
                    options[0] = new DialogOption("VAB Storage", RecoverToVAB);
                    options[1] = new DialogOption("SPH Storage", RecoverToSPH);
                    options[2] = new DialogOption("The Scrapyard", RecoverToScrapyard);
                    MultiOptionDialog diag = new MultiOptionDialog("Send recovered vessel to", windowTitle: "Vessel Recovery", options: options);
                    PopupDialog.SpawnPopupDialog(diag, false, HighLogic.Skin);
                }*/

                if (!KCT_GUI.PrimarilyDisabled)
                    KCT_Utilities.ProgressBuildTime();
            }
            catch (IndexOutOfRangeException e)
            {
                print(e.Message);
                print(e.StackTrace);
            }

        }
        
       /* private void RecoverToVAB()
        {
            KCT_Utilities.SpendFunds(KCT_GameStates.recoveredVessel.cost, TransactionReasons.VesselRollout);
            KCT_GameStates.recoveredVessel.UpdateShipType(KCT_BuildListVessel.ListType.VAB);
            KCT_GameStates.ActiveKSC.VABWarehouse.Add(KCT_GameStates.recoveredVessel);
            KCT_GameStates.ActiveKSC.Recon_Rollout.Add(new KCT_Recon_Rollout(KCT_GameStates.recoveredVessel, KCT_Recon_Rollout.RolloutReconType.Recovery, KCT_GameStates.recoveredVessel.id.ToString()));
            KCT_GameStates.recoveredVessel = null;
            InputLockManager.RemoveControlLock("KCTPopupLock");
            
        }
        private void RecoverToSPH()
        {
            KCT_Utilities.SpendFunds(KCT_GameStates.recoveredVessel.cost, TransactionReasons.VesselRollout);
            KCT_GameStates.recoveredVessel.UpdateShipType(KCT_BuildListVessel.ListType.SPH);
            KCT_GameStates.ActiveKSC.SPHWarehouse.Add(KCT_GameStates.recoveredVessel);
            KCT_GameStates.ActiveKSC.Recon_Rollout.Add(new KCT_Recon_Rollout(KCT_GameStates.recoveredVessel, KCT_Recon_Rollout.RolloutReconType.Recovery, KCT_GameStates.recoveredVessel.id.ToString()));
            KCT_GameStates.recoveredVessel = null;
            InputLockManager.RemoveControlLock("KCTPopupLock");
        }
        private void RecoverToScrapyard()
        {
            KCTDebug.Log("Adding recovered parts to Part Inventory");
            foreach (ConfigNode p in KCT_GameStates.recoveredVessel.ExtractedPartNodes)
            {
               // string name = p.partInfo.name + KCT_Utilities.GetTweakScaleSize(p);
                KCT_Utilities.AddPartToInventory(p);
            }
            KCT_GameStates.recoveredVessel = null;
            InputLockManager.RemoveControlLock("KCTPopupLock");

        }*/

        private static DateTime loadDeferTime = DateTime.MaxValue;
        private static int lastSeconds = 0;

        public static void DelayedStart()
        {

        //    KCTDebug.Log(ScenarioUpgradeableFacilities.protoUpgradeables.Keys);
          //  KCTDebug.Log(ScenarioUpgradeableFacilities.protoUpgradeables.Values.ElementAt(0).facilityRefs[0].name);


           /* if (!updateChecked)
            {
                if (KCT_GameStates.settings.CheckForUpdates && !KCT_GameStates.firstStart) //Check for updates
                    KCT_UpdateChecker.CheckForUpdate(false, KCT_GameStates.settings.VersionSpecific);
                updateChecked = true;
            }*/

            if (!KCT_PresetManager.Instance.ActivePreset.generalSettings.Enabled)
                return;

            List<GameScenes> validScenes = new List<GameScenes> { GameScenes.SPACECENTER };
            if (validScenes.Contains(HighLogic.LoadedScene))
            {
                //Check for simulation save and load it.
                string backupFile = KSPUtil.ApplicationRootPath + "saves/" + HighLogic.SaveFolder + "/KCT_simulation_backup.sfs";
                if (System.IO.File.Exists(backupFile))
                {
                    KCT_GameStates.LoadingSimulationSave = true;
                  /*  if (!KCT_GameStates.LoadingSimulationSave)
                        KCT_Utilities.LoadSimulationSave();
                    else
                        System.IO.File.Delete(backupFile);*/
                }
            }

            if (HighLogic.LoadedSceneIsFlight && KCT_GameStates.flightSimulated)
            {
                KCTDebug.Log("Simulation started");
                KCT_GUI.hideAll();
                KCT_GUI.showSimulationWindow = !KCT_GameStates.settings.NoSimGUI;
                KCT_GUI.showTimeRemaining = true;
                if (!KCT_GameStates.simulationInitialized)
                {
                    Planetarium.SetUniversalTime(KCT_GameStates.simulationUT);
                    KCT_GameStates.simulationInitialized = true;
                }
            }

            if (!HighLogic.LoadedSceneIsFlight && KCT_GameStates.FundsToChargeAtSimEnd != 0)
            {
                KCT_Utilities.SpendFunds(KCT_GameStates.FundsToChargeAtSimEnd, TransactionReasons.None);
                KCT_GameStates.FundsToChargeAtSimEnd = 0;
            }
            if (!HighLogic.LoadedSceneIsFlight && KCT_GameStates.FundsGivenForVessel != 0)
            {
                KCT_Utilities.SpendFunds(KCT_GameStates.FundsGivenForVessel, TransactionReasons.VesselRollout);
                KCT_GameStates.FundsGivenForVessel = 0;
            }
            if (HighLogic.LoadedSceneIsFlight && !KCT_GameStates.flightSimulated)
            {
                List<VesselType> invalidTypes = new List<VesselType> { VesselType.Debris, VesselType.SpaceObject, VesselType.Unknown };
                if (!invalidTypes.Contains(FlightGlobals.ActiveVessel.vesselType) && !KCT_GameStates.BodiesVisited.Contains(FlightGlobals.ActiveVessel.mainBody.bodyName))
                {
                    KCT_GameStates.BodiesVisited.Add(FlightGlobals.ActiveVessel.mainBody.bodyName);
                    var message = new ScreenMessage("[KCT] New simulation body unlocked: " + FlightGlobals.ActiveVessel.mainBody.bodyName, 4.0f, ScreenMessageStyle.UPPER_LEFT);
                    ScreenMessages.PostScreenMessage(message, true);
                    KCTDebug.Log("Unlocked sim body: " + FlightGlobals.ActiveVessel.mainBody.bodyName);
                }
            }

            if (KCT_GUI.PrimarilyDisabled) return;
            
            //The following should only be executed when fully enabled for the save

            foreach (KCT_KSC KSC in KCT_GameStates.KSCs)
            {
                KSC.RecalculateBuildRates();
                KSC.RecalculateUpgradedBuildRates();
            }

            if (!HighLogic.LoadedSceneIsFlight && KCT_GameStates.buildSimulatedVessel)
            {
                KCT_GameStates.buildSimulatedVessel = false;
                KCT_BuildListVessel toBuild = KCT_GameStates.launchedVessel.NewCopy(false);
                toBuild.buildPoints = KCT_Utilities.GetBuildTime(toBuild.ExtractedPartNodes, true, KCT_GUI.useInventory);
                KCT_Utilities.AddVesselToBuildList(toBuild, KCT_GUI.useInventory);
            }

            if (HighLogic.LoadedSceneIsFlight && !KCT_GameStates.flightSimulated)
            {
                KCT_GUI.hideAll();
                if (FlightGlobals.ActiveVessel.situation == Vessel.Situations.PRELAUNCH && KCT_GameStates.launchedVessel != null)
                {
                    bool removed = KCT_GameStates.launchedVessel.RemoveFromBuildList();
                    if (removed) //Only do these when the vessel is first removed from the list
                    {
                        //Add the cost of the ship to the funds so it can be removed again by KSP
                        //KCT_Utilities.AddFunds(KCT_Utilities.GetTotalVesselCost(FlightGlobals.ActiveVessel.protoVessel), TransactionReasons.VesselRollout);
                        KCT_Utilities.AddFunds(KCT_GameStates.launchedVessel.cost, TransactionReasons.VesselRollout);
                        FlightGlobals.ActiveVessel.vesselName = KCT_GameStates.launchedVessel.shipName;
                    }

                    KCT_Recon_Rollout rollout = KCT_GameStates.ActiveKSC.Recon_Rollout.FirstOrDefault(r => r.associatedID == KCT_GameStates.launchedVessel.id.ToString());
                    if (rollout != null)
                        KCT_GameStates.ActiveKSC.Recon_Rollout.Remove(rollout);
                }
            }
           
            if (HighLogic.LoadedSceneIsEditor)
            {
                if (KCT_GameStates.EditorShipEditingMode)
                {
                    KCTDebug.Log("Editing " + KCT_GameStates.editedVessel.shipName);
                    EditorLogic.fetch.shipNameField.Text = KCT_GameStates.editedVessel.shipName;
                }
                if (!KCT_GUI.PrimarilyDisabled)
                {
                    if (KCT_GameStates.settings.OverrideLaunchButton)
                    {
                        KCTDebug.Log("Taking control of launch button");
                        EditorLogic.fetch.launchBtn.methodToInvoke = "ShowLaunchAlert";
                        EditorLogic.fetch.launchBtn.scriptWithMethodToInvoke = KerbalConstructionTime.instance;
                    }
                    else
                        InputLockManager.SetControlLock(ControlTypes.EDITOR_LAUNCH, "KCTLaunchLock");
                    KCT_Utilities.RecalculateEditorBuildTime(EditorLogic.fetch.ship);
                }
            }
            if (HighLogic.LoadedScene == GameScenes.SPACECENTER)
            {
                if (!KCT_GUI.PrimarilyDisabled)
                {
                    if (ToolbarManager.ToolbarAvailable && KCT_GameStates.settings.PreferBlizzyToolbar)
                        if (KCT_GameStates.showWindows[0])
                            KCT_GUI.ClickOn();
                    else
                    {
                        if (KCT_Events.instance != null && KCT_Events.instance.KCTButtonStock != null)
                        {
                            //KCT_Events.instance.KCTButtonStock.SetTrue(true);
                            //KCT_GUI.clicked = true;
                            if (KCT_GameStates.showWindows[0])
                                KCT_GUI.ClickOn();
                        }
                      /*  else
                        {
                            KCT_GUI.showEditorGUI = KCT_GameStates.showWindows[0];
                        }*/
                    }
                    KCT_GUI.ResetBLWindow();
                }
                else
                {
                    KCT_GUI.showBuildList = false;
                    KCT_GameStates.showWindows[0] = false;
                }
                if (KCT_GameStates.firstStart)
                {
                    KCTDebug.Log("Showing first start.");
                    KCT_GUI.showFirstRun = true;
                }
                KCT_GameStates.firstStart = false;
                if (KCT_GameStates.LaunchFromTS)
                {
                    KCT_GameStates.launchedVessel.Launch();
                }
            }
        }

        public bool SOIAlert()
        {
            foreach (Vessel v in FlightGlobals.Vessels)
            {
                if (KCT_GameStates.VesselTypesForSOI.Contains(v.vesselType))
                {
                    if (v != FlightGlobals.ActiveVessel)
                    {
                        if (!KCT_GameStates.vesselDict.ContainsKey(v.id.ToString()))
                        {
                            KCT_GameStates.vesselDict.Add(v.id.ToString(), v.mainBody.bodyName);
                            print("Vessel " + v.id.ToString() + " added to lstVessels.");

                        }
                        else if (v.mainBody.bodyName != KCT_GameStates.vesselDict[v.id.ToString()])
                        {
                            KCT_GameStates.lastSOIVessel = v.name;
                            print("Vessel " + v.id.ToString() + " SOI change.");
                            KCT_GameStates.vesselDict[v.id.ToString()] = v.mainBody.bodyName;
                            KCT_GUI.showSOIAlert = true;
                            return true;

                        }
                    }
                }
            }
            return false;
        }

        public void ShowLaunchAlert()
        {
            if (KCT_GUI.PrimarilyDisabled)
                EditorLogic.fetch.launchVessel();
            else
            {
                KCT_GUI.showLaunchAlert = true;
                EditorLogic.fetch.Lock(true, true, true, "KCTGUILock");

                //Pop up the window at the mouse cursor if we're in mode 1
                if (KCT_GameStates.settings.WindowMode == 1)
                {
                    KCT_GUI.centralWindowPosition.width = 50;
                    KCT_GUI.centralWindowPosition.y = Mouse.screenPos.y;
                    KCT_GUI.centralWindowPosition.x = Mouse.screenPos.x - (KCT_GUI.centralWindowPosition.width/2);
                }

                // This is how you hide tooltips.
                EditorTooltip.Instance.HideToolTip();
                GameEvents.onTooltipDestroyRequested.Fire();
            }
        }

        public void UpdateOldFormulaCFG()
        {
            String filePath = KSPUtil.ApplicationRootPath + "GameData/KerbalConstructionTime/KCT_Formulas.cfg";
            if (System.IO.File.Exists(filePath))
            {
                bool didUpdate = false;
                ConfigNode current = ConfigNode.Load(filePath).GetNode("KCT_FormulaSettings");
                if (current.HasValue("NodeMax") && current.HasValue("NodeFormula"))
                {
                    string max = current.GetValue("NodeMax");
                    if (max != "" && double.Parse(max) > 0)
                    {
                        didUpdate = true;
                        KCTDebug.Log("Updating node formula.");
                        string concat = "min(" + max + ", " + current.GetValue("NodeFormula") + ")";
                        current.SetValue("NodeFormula", concat);
                    }
                }
                if (current.HasValue("UpgradeFundsMax") && current.HasValue("UpgradeFundsFormula"))
                {
                    string max = current.GetValue("UpgradeFundsMax");
                    if (max != "" && double.Parse(max) > 0)
                    {
                        didUpdate = true;
                        KCTDebug.Log("Updating funds upgrades formula.");
                        string concat = "min(" + max + ", " + current.GetValue("UpgradeFundsFormula") + ")";
                        current.SetValue("UpgradeFundsFormula", concat);
                    }
                }
                if (current.HasValue("UpgradeScienceMax") && current.HasValue("UpgradeScienceFormula"))
                {
                    string max = current.GetValue("UpgradeScienceMax");
                    if (max != "" && double.Parse(max) > 0)
                    {
                        didUpdate = true;
                        KCTDebug.Log("Updating science upgrades formula.");
                        string concat = "min(" + max + ", " + current.GetValue("UpgradeScienceFormula") + ")";
                        current.SetValue("UpgradeScienceFormula", concat);
                    }
                }
                if (didUpdate)
                {
                    ConfigNode save = new ConfigNode("KCT_FormulaSettings");
                    save.AddNode(current);
                    save.Save(filePath);
                }
            }
        }
    }
}
/*
Copyright (C) 2014  Michael Marvin, Zachary Eck

    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.
*/