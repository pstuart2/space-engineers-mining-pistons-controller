using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using VRage;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ObjectBuilders.Definitions;
using VRageMath;

namespace IngameScript
{
	partial class Program : MyGridProgram
	{
		#region mdk preserve
		static string SEARCH_TAG = "DeepDriller";
		#endregion

		static MyIni CB_IniConfig;

		static float CFG_DrillPushSpeed = 0.025f;
		static float CFG_RetractSpeed = 0.75F;
		static float CFG_UpperPistonLimit = 10.0f;
		static float CFG_LowerPistonLimit = 0.0f;
		static float CFG_CargoFillPause = 0.95f;
		static float CFG_CargoFillRestart = 0.20f;
		static float CFG_MaxDepth = 0.0f;

		static bool CFG_AllDrills = false;
		static bool CFG_TrackBatteries = false;

		static float CFG_TurnOnEnginesAtBatteryThreshold = 0.4f;
		static float CFG_TurnOffEngineAtBatteryThreshold = 0.98f;

		static DateTime lastRunTime = new DateTime();
		static DateTime stateStartTime = new DateTime();

		static SortedDictionary<int, PistonGroup> pistonGroups = new SortedDictionary<int, PistonGroup>();
		static List<IMyTextPanel> displayPanels = new List<IMyTextPanel>();
		static List<IMyTerminalBlock> storage = new List<IMyTerminalBlock>();
		static List<IMyBatteryBlock> batteries = new List<IMyBatteryBlock>();
		static List<IMyPowerProducer> engines = new List<IMyPowerProducer>();

		static int SECONDS_BETWEEN_LOOP_COUNTER_RESET = 20;

		DrillController drillController;

		MyFixedPoint totalVolume = MyFixedPoint.Zero;
		MyFixedPoint totalUsedVolume = MyFixedPoint.Zero;

		float percentFull = 0.0f;
		float totalDepth = 0.0f;
		float batteryPercent = 0.0f;

		int loopCounter = 0;
		int refreshLoopCount = 6 * SECONDS_BETWEEN_LOOP_COUNTER_RESET;

		string[] statusString = { "/", "-", "\\", "|" };

		enum State { Stopped, Paused, Starting, Drilling, Finishing, Retracting };
		State CurrentState = State.Stopped;

		bool IsCommandRunning = false;
		bool enginesOn = false;

		public Program()
		{
			Load();

			drillController = new DrillController(Me);

			LoadConfig();
			ScanForBlocks();

			Runtime.UpdateFrequency = UpdateFrequency.Update10;
		}

		public void Save()
		{
			Storage = CurrentState.ToString();
		}

		public void Load()
		{
			if(string.IsNullOrWhiteSpace(Storage))
			{
				return;
			}

			CurrentState = (State)Enum.Parse(typeof(State), Storage, true);

			
		}

		public void Main(string argument, UpdateType updateSource)
		{
			if(RunCommands(argument, updateSource) || IsCommandRunning)
			{
				return;
			}

			Update();
			UpdateDisplays();
			WriteControlText();

			FinishLoop();
		}

		private void FinishLoop()
		{
			lastRunTime += Runtime.TimeSinceLastRun;
			if (loopCounter >= refreshLoopCount) { loopCounter = 0; }
			else { loopCounter++; }
		}

		private void LoadConfig()
		{
			CB_IniConfig = Helpers.LoadIni(Me.CustomData);

			CFG_DrillPushSpeed = CB_IniConfig.Get(SEARCH_TAG, "DrillPushSpeed").ToSingle(CFG_DrillPushSpeed);
			CFG_RetractSpeed = CB_IniConfig.Get(SEARCH_TAG, "RetractSpeed").ToSingle(CFG_RetractSpeed);
			CFG_LowerPistonLimit = CB_IniConfig.Get(SEARCH_TAG, "LowerPistonLimit").ToSingle(CFG_LowerPistonLimit);
			CFG_UpperPistonLimit = CB_IniConfig.Get(SEARCH_TAG, "UpperPistonLimit").ToSingle(CFG_UpperPistonLimit);
			CFG_CargoFillPause = CB_IniConfig.Get(SEARCH_TAG, "CargoFillPause").ToSingle(CFG_CargoFillPause);
			CFG_CargoFillRestart = CB_IniConfig.Get(SEARCH_TAG, "CargoFillRestart").ToSingle(CFG_CargoFillRestart);
			CFG_MaxDepth = CB_IniConfig.Get(SEARCH_TAG, "MaxDepth").ToSingle(CFG_MaxDepth);

			CFG_AllDrills = CB_IniConfig.Get(SEARCH_TAG, "AllDrills").ToBoolean();
			CFG_TrackBatteries = CB_IniConfig.Get(SEARCH_TAG, "TrackBatteries").ToBoolean();

			CFG_TurnOnEnginesAtBatteryThreshold = CB_IniConfig.Get(SEARCH_TAG, "TurnOnEnginesAtBatteryThreshold").ToSingle(CFG_TurnOnEnginesAtBatteryThreshold);
			CFG_TurnOffEngineAtBatteryThreshold = CB_IniConfig.Get(SEARCH_TAG, "TurnOffEngineAtBatteryThreshold").ToSingle(CFG_TurnOffEngineAtBatteryThreshold);
		}

		private void ScanForBlocks()
		{
			GetPistonGroups();
			foreach (var pg in pistonGroups.Values)
			{
				pg.Refresh(GridTerminalSystem);
			}
			
			drillController.Refresh(GridTerminalSystem);

			displayPanels.Clear();
			GridTerminalSystem.GetBlocksOfType(displayPanels, ShouldTrackDispalyPanels);

			storage.Clear();
			GridTerminalSystem.GetBlocksOfType(storage, ShouldTrackStorage);

			GridTerminalSystem.GetBlocksOfType(batteries, ShouldTrackBatteries);
			GridTerminalSystem.GetBlocksOfType(engines, ShouldTrackEngine);

			enginesOn = engines.Any((e) => e.Enabled);
		}

		private void GetPistonGroups()
		{
			pistonGroups.Clear();

			List<IMyExtendedPistonBase> pistons = new List<IMyExtendedPistonBase>();
			GridTerminalSystem.GetBlocksOfType(pistons, ShouldTrackPiston);
			
			foreach(var piston in pistons)
			{
				var index = PistonGroup.ParseIndex(piston.CustomName);
				if (!pistonGroups.ContainsKey(index))
				{
					pistonGroups.Add(index, new PistonGroup(index));
				}
			}
		}

		private bool ShouldTrackPiston(IMyExtendedPistonBase piston)
		{
			return piston.IsSameConstructAs(Me)
				&& piston.IsFunctional
				&& piston.CustomName.Contains($"[{SEARCH_TAG}.");
		}

		private bool ShouldTrackStorage(IMyTerminalBlock entity)
		{
			return entity.IsSameConstructAs(Me)
				&& entity.IsFunctional
				&& entity.HasInventory
				&& (entity.CustomName.Contains($"[{SEARCH_TAG}.INV]") || MyIni.HasSection(entity.CustomData, $"[{SEARCH_TAG}.INV]"));
		}

		private bool ShouldTrackDispalyPanels(IMyTextPanel block)
		{
			var shouldTrack = block.IsSameConstructAs(Me)
				&& block.IsFunctional
				&& (block.CustomName.Contains($"[{SEARCH_TAG}]") || MyIni.HasSection(block.CustomData, SEARCH_TAG));

			if(shouldTrack)
			{
				block.ContentType = ContentType.TEXT_AND_IMAGE;
			}

			return shouldTrack;
		}

		bool ShouldTrackEngine(IMyPowerProducer block)
		{
			return block.IsSameConstructAs(Me)
				&& block.IsFunctional
				&& (block.CustomName.Contains($"[{SEARCH_TAG}.Engine]") || MyIni.HasSection(block.CustomData, $"[{SEARCH_TAG}.Engine]"));
		}

		bool ShouldTrackBatteries(IMyBatteryBlock block)
		{
			return CFG_TrackBatteries
				&& block.IsSameConstructAs(Me)
				&& block.IsFunctional;
		}

		private bool RunCommands(string argument, UpdateType updateSource)
		{
			if (updateSource == UpdateType.Update10)
			{
				return false;
			}

			IsCommandRunning = true;
			switch (argument.ToLower())
			{
				case "rescan":
					{
						//Stop();
						LoadConfig();
						ScanForBlocks();
						break;
					}
				case "start":
					{
						Start();
						break;
					}

				case "stop":
					{
						Stop();
						break;
					}

				case "retract":
					{
						Retract();
						break;
					}
			}

			IsCommandRunning = false;
			return true;
		}

		void ChangeState(State state)
		{
			CurrentState = state;
			stateStartTime = lastRunTime;
		}

		void Start()
		{
			if (CurrentState == State.Stopped || CurrentState == State.Paused)
			{
				ChangeState(State.Starting);
				drillController.TurnOn();
			}
		}

		void Drill()
		{
			ChangeState(State.Drilling);
		}

		void Finish()
		{
			ChangeState(State.Finishing);
			StopPistons();
		}

		void Stop()
		{
			ChangeState(State.Stopped);
			StopPistons();
			drillController.TurnOff();
		}

		void Pause()
		{
			ChangeState(State.Paused);
			StopPistons();
			drillController.TurnOff();
		}

		void Retract()
		{
			ChangeState(State.Retracting);
			foreach(var pg in pistonGroups.Values)
			{
				pg.Retract();
			}

			drillController.TurnOff();
		}

		void StopPistons()
		{
			foreach (var pg in pistonGroups.Values)
			{
				pg.Stop();
			}
		}

		bool IsSecondsElapsed(int seconds)
		{
			return lastRunTime - stateStartTime >= TimeSpan.FromSeconds(seconds);
		}

		bool IsMaxDepthReached()
		{
			return CFG_MaxDepth > 0 && totalDepth >= CFG_MaxDepth;
		}

		bool IsMaxInventoryReached()
		{
			return percentFull >= CFG_CargoFillPause || percentFull >= 1.0f;
		}

		bool IsMinInventoryReached()
		{
			return percentFull <= CFG_CargoFillRestart;
		}

		void Update()
		{
			totalDepth = pistonGroups.Values.Sum((pg) => pg.GetDepth());
			UpdateInventory();
			UpdateBatteryState();

			switch (CurrentState)
			{
				case State.Starting:
					{
						if(IsSecondsElapsed(2))
						{
							Drill();
						}
						break;
					}

				case State.Drilling:
					{
						if (!OnlyExecuteEveryXLoops(5))
						{
							return;
						}

						foreach (var pg in pistonGroups.Values)
						{
							if (!pg.IsDrillingComplete())
							{
								pg.Extend();
								break;
							}
						}

						if (IsMaxDepthReached() || pistonGroups.Values.All((pg) => pg.IsDrillingComplete()))
						{
							Finish();
						} 
						else
						{
							if(IsMaxInventoryReached())
							{
								Pause();
							}
						}

						break;
					}

				case State.Paused:
					{
						if(IsMinInventoryReached())
						{
							Start();
						}

						break;
					}

				case State.Finishing:
					{
						if(IsSecondsElapsed(10))
						{
							Retract();
						}

						break;
					}

				case State.Retracting:
					{
						if (pistonGroups.Values.All((pg) => pg.IsRetracted()))
						{
							Stop();
						}

						break;
					}
			}
		}

		void UpdateBatteryState()
		{
			if (!CFG_TrackBatteries || !OnlyExecuteEveryXLoops(12))
			{
				return;
			}

			float maxStoredPower = 0.0f;
			float currentStorePower = 0.0f;

			foreach (var battery in batteries)
			{
				maxStoredPower += battery.MaxStoredPower;
				currentStorePower += battery.CurrentStoredPower;
			}

			batteryPercent = 1 - ((maxStoredPower - currentStorePower) / maxStoredPower);

			if (batteryPercent <= CFG_TurnOnEnginesAtBatteryThreshold)
			{
				TurnOnEngines();
			}
			else if (batteryPercent >= CFG_TurnOffEngineAtBatteryThreshold)
			{
				TurnOffEngines();
			}
		}

		void TurnOnEngines()
		{
			if (enginesOn) return;
			Echo("Turn on...");
			enginesOn = true;
			engines.ForEach((d) => d.ApplyAction("OnOff_On"));
		}

		void TurnOffEngines()
		{
			if (!enginesOn) return;
			Echo("Turn off...");
			enginesOn = false;
			engines.ForEach((d) => d.ApplyAction("OnOff_Off"));
		}

		bool OnlyExecuteEveryXLoops(int x)
		{
			return loopCounter % x == 0;
		}

		void UpdateInventory()
		{
			if (!OnlyExecuteEveryXLoops(12))
			{
				return;
			}

			totalVolume = MyFixedPoint.Zero;
			totalUsedVolume = MyFixedPoint.Zero;

			storage.ForEach((s) =>
			{
				var inventory = s.GetInventory();

				totalVolume += inventory.MaxVolume;
				totalUsedVolume += inventory.CurrentVolume;
			});

			percentFull = 1 - ((((float)totalVolume) - ((float)totalUsedVolume)) / ((float)totalVolume));
		}

		void UpdateDisplays()
		{
			StringBuilder sb = new StringBuilder(string.Format("Status: {0,12}     Drills: {1,10}",
				CurrentState, drillController.GetStateText()));

			if(storage.Count > 0)
			{
				sb.AppendFormat("     Storage Used: {0,10:P}", percentFull);
			}

			sb.AppendFormat("     Drill Depth: {0:F1}\n", totalDepth);

			foreach (var pg in pistonGroups.Values)
			{
				sb.Append("\n");
				sb.Append(pg.GetStatus());
			}

			foreach (var display in displayPanels)
			{
				display.WriteText(sb);
			}
		}

		void WriteControlText()
		{
			StringBuilder sb = new StringBuilder($"{CurrentState}\n\n");

			sb.Append("Paul's Drilling Script " + statusString[loopCounter % 4] + "\n======================");
			sb.Append($"\n\nPistonGroups: {pistonGroups.Count}");
			sb.Append($"\nDrills: {drillController.Count}");
			sb.Append($"\nDisplays: {displayPanels.Count}");
			sb.Append($"\nStorage: {storage.Count}");
			sb.Append($"\nCapacity: {totalVolume}");
			sb.Append($"\nUsed: {totalUsedVolume}");

			if (CFG_TrackBatteries)
			{
				sb.AppendFormat("\nBattery Percent: {0:P}", batteryPercent);
				sb.AppendFormat("\nEngines: {0} ({1})", engines.Count, enginesOn ? "On" : "Off");
			}
			

			Echo(sb.ToString());
		}
	}
}
