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
		public static string SEARCH_TAG = "DeepDriller";
		#endregion

		MyIni MyIni;

		float CFG_DrillPushSpeed = 0.025f;
		float CFG_RetractSpeed = 0.75F;
		float CFG_UpperPistonLimit = 10.0f;
		float CFG_LowerPistonLimit = 0.0f;

		bool CFG_AllDrills = false;

		enum State { Stopped, Starting, Drilling, Finishing, Retracting };
		State CurrentState = State.Stopped;

		static DateTime lastRunTime = new DateTime();
		static DateTime stateStartTime = new DateTime();

		static SortedDictionary<int, PistonGroup> pistonGroups = new SortedDictionary<int, PistonGroup>();
		static List<IMyTextPanel> displayPanels = new List<IMyTextPanel>();
		static List<IMyShipDrill> drills = new List<IMyShipDrill>();

		static int SECONDS_BETWEEN_LOOP_COUNTER_RESET = 20;
		int loopCounter = 0;
		int refreshLoopCount = 6 * SECONDS_BETWEEN_LOOP_COUNTER_RESET;

		string[] statusString = { "/", "-", "\\", "|" };

		public Program()
		{
			PistonGroup.SEARCH_TAG = SEARCH_TAG;

			LoadConfig();
			ScanForBlocks();

			Runtime.UpdateFrequency = UpdateFrequency.Update10;
		}

		public void Save()
		{
		}

		public void Main(string argument, UpdateType updateSource)
		{
			RunCommands(argument, updateSource);
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
			MyIni = Helpers.LoadIni(Me.CustomData);

			CFG_DrillPushSpeed = MyIni.Get(SEARCH_TAG, "DrillPushSpeed").ToSingle(0.025f);
			CFG_RetractSpeed = MyIni.Get(SEARCH_TAG, "RetractSpeed").ToSingle(0.025f);
			CFG_LowerPistonLimit = MyIni.Get(SEARCH_TAG, "LowerPistonLimit").ToSingle(0.0f);
			CFG_UpperPistonLimit = MyIni.Get(SEARCH_TAG, "UpperPistonLimit").ToSingle(10.0f);

			CFG_AllDrills = MyIni.Get(SEARCH_TAG, "AllDrills").ToBoolean();
		}

		private void ScanForBlocks()
		{
			GetPistonGroups();
			foreach (var pg in pistonGroups.Values)
			{
				pg.Refresh(GridTerminalSystem, MyIni);
			}
			
			GridTerminalSystem.GetBlocksOfType(displayPanels, ShouldTrack);
			GridTerminalSystem.GetBlocksOfType(drills, ShouldTrackDrill);
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
					pistonGroups.Add(index, new PistonGroup(index)
					{
						UpperPistonLimit = CFG_UpperPistonLimit,
						LowerPistonLimit = CFG_LowerPistonLimit,
					});
				}
			}
		}

		private bool ShouldTrackPiston(IMyExtendedPistonBase piston)
		{
			return piston.CustomName.Contains($"[{SEARCH_TAG}.");
		}

		private bool ShouldTrackDrill(IMyShipDrill drill)
		{
			return CFG_AllDrills || ShouldTrack(drill);
		}

		private bool ShouldTrack(IMyFunctionalBlock block)
		{
			return block.IsSameConstructAs(Me)
				&& block.IsFunctional
				&& block.CustomName.Contains($"[{SEARCH_TAG}]");
		}

		private void RunCommands(string argument, UpdateType updateSource)
		{
			if (updateSource == UpdateType.Update10)
			{
				return;
			}

			switch(argument.ToLower())
			{
				case "rescan":
					{
						Stop();
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
		}

		void ChangeState(State state)
		{
			CurrentState = state;
			stateStartTime = lastRunTime;
		}

		void Start()
		{
			if (CurrentState == State.Stopped)
			{
				ChangeState(State.Starting);
				DrillsOn();
			}
		}

		void Drill()
		{
			ChangeState(State.Drilling);
		}

		void Finish()
		{
			ChangeState(State.Finishing);
		}

		void Stop()
		{
			ChangeState(State.Stopped);
			foreach (var pg in pistonGroups.Values)
			{
				pg.Stop();
			}

			DrillsOff();
		}

		void Retract()
		{
			ChangeState(State.Retracting);
			foreach(var pg in pistonGroups.Values)
			{
				pg.Retract(CFG_RetractSpeed);
			}

			DrillsOff();
		}

		void DrillsOn()
		{
			drills.ForEach((d) => d.ApplyAction("OnOff_On"));
		}

		void DrillsOff()
		{
			drills.ForEach((d) => d.ApplyAction("OnOff_Off"));
		}

		bool IsSecondsElapsed(int seconds)
		{
			return lastRunTime - stateStartTime >= TimeSpan.FromSeconds(seconds);
		}

		void Update()
		{
			switch(CurrentState)
			{
				case State.Starting:
					{
						if(drills.All((d) => d.IsWorking) && IsSecondsElapsed(2))
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
								pg.Extend(CFG_DrillPushSpeed);
								break;
							}
						}

						if (pistonGroups.Values.All((pg) => pg.IsDrillingComplete()))
						{
							Finish();
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

		bool OnlyExecuteEveryXLoops(int x)
		{
			return loopCounter % x == 0;
		}

		string GetDrillStateText()
		{
			if (drills.All((d) => !d.Enabled))
			{
				return "All Off";
			}

			if (drills.All((d) => d.Enabled))
			{
				return "All On";
			}

			return "Mixed";
		}

		void UpdateDisplays()
		{
			StringBuilder sb = new StringBuilder($"Status: {CurrentState}");

			sb.Append("\n\nDrills: ");
			sb.Append(GetDrillStateText());

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
			sb.Append($"\nDrills: {drills.Count}");
			sb.Append($"\nDisplays: {displayPanels.Count}");

			Echo(sb.ToString());
		}
	}
}
