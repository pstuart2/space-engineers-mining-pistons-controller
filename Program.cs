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
		public static string SEARCH_TAG = "MPC";

		public static int SECONDS_BETWEEN_CONTROL_SEARCHES = 20;
		public static float DRILL_PUSH_SPEED = 0.025f;
		public static float RETRACT_SPEED = 0.75F;

		#endregion

		enum State { Stopped, Starting, Drilling, Finishing, Retracting };
		State CurrentState = State.Stopped;

		static DateTime lastRunTime = new DateTime();
		static DateTime stateStartTime = new DateTime();

		static SortedDictionary<int, PistonGroup> pistonGroups = new SortedDictionary<int, PistonGroup>();
		static List<IMyTextPanel> displayPanels = new List<IMyTextPanel>();
		static List<IMyShipDrill> drills = new List<IMyShipDrill>();

		int loopCounter = 0;
		int refreshLoopCount = 6 * SECONDS_BETWEEN_CONTROL_SEARCHES;

		string[] statusString = { "/", "-", "\\", "|" };

		public Program()
		{
			TagParser.SEARCH_TAG = SEARCH_TAG;

			Runtime.UpdateFrequency = UpdateFrequency.Update10;
			
			ScanForBlocks();
		}
		
		public void Save()
		{
			// Called when the program needs to save its state. Use
			// this method to save your state to the Storage field
			// or some other means. 
			// 
			// This method is optional and can be removed if not
			// needed.

			// TODO: Save isDrilling
			
		}

		public void Main(string argument, UpdateType updateSource)
		{
			//if (loopCounter == 0)
			//{
			//	Refresh();
			//}

			RunCommands(argument, updateSource);

			Update();
			UpdateDisplays();

			WriteControlText();

			lastRunTime += Runtime.TimeSinceLastRun;
			if (loopCounter >= refreshLoopCount) { loopCounter = 0; }
			else { loopCounter++; }
		}

		private void ScanForBlocks()
		{
			GetPistonGroups();
			foreach (var pg in pistonGroups.Values)
			{
				pg.Refresh(GridTerminalSystem);
			}
			
			GridTerminalSystem.GetBlocksOfType(displayPanels, ShouldTrack);
			GridTerminalSystem.GetBlocksOfType(drills, ShouldTrack);
		}

		private void GetPistonGroups()
		{
			pistonGroups.Clear();

			List<IMyExtendedPistonBase> pistons = new List<IMyExtendedPistonBase>();
			GridTerminalSystem.GetBlocksOfType(pistons, ShouldTrackPiston);
			foreach(var piston in pistons)
			{
				var tag = TagParser.ParsePistonTag(piston.CustomName);
				if(!pistonGroups.ContainsKey(tag.SortIndex))
				{
					pistonGroups.Add(tag.SortIndex, new PistonGroup(tag, SEARCH_TAG));
				}
			}
		}

		private bool ShouldTrackPiston(IMyExtendedPistonBase piston)
		{
			return TagParser.ContainsPistonTag(piston.CustomName);
		}

		private bool ShouldTrack(IMyFunctionalBlock block)
		{
			return block.IsSameConstructAs(Me) && block.CustomName.Contains($"[{SEARCH_TAG}")
				&& block.IsFunctional;
		}

		void RunCommands(string argument, UpdateType updateSource)
		{
			if (updateSource == UpdateType.Update10)
			{
				return;
			}

			switch(argument.ToLower())
			{
				case "rescan":
					{
						GetPistonGroups();
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

		void Finishing()
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
				pg.Retract(RETRACT_SPEED);
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
								pg.Extend(DRILL_PUSH_SPEED);
								break;
							}
						}

						if (pistonGroups.Values.All((pg) => pg.IsDrillingComplete()))
						{
							Finishing();
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

			sb.Append("\n\nDrill State: ");
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
