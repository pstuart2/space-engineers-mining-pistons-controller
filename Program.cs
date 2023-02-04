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
		public static string TAG = "DEEPDRILL";

		public static int SECONDS_BETWEEN_CONTROL_SEARCHES = 20;
		public static float DRILL_PUSH_SPEED = 0.025f;
		public static float RETRACT_SPEED = 0.75F;

		// TODO: Better way to get this
		public static List<PistonGroup> pistons = new List<PistonGroup>()
		{
			new PistonGroup("Set v1", "VPLT;VPRT", true),
			new PistonGroup("Set v2", "VPLB;VPRB", true),
			new PistonGroup("Set 1", "DP01L;DP01R", false),
			new PistonGroup("Set 2", "DP02L;DP02R", false),
			new PistonGroup("Set 3", "DP03L;DP03R", false),
			new PistonGroup("Set 4", "DP04L;DP04R", false),
			new PistonGroup("Set 5", "DP05L;DP05R", false),
			new PistonGroup("Set 6", "DP06L;DP06R", false),
		};
		#endregion

		enum State { Stopped, Starting, Drilling, Finishing, Retracting };
		State CurrentState = State.Stopped;

		static DateTime lastRunTime = new DateTime();
		static DateTime stateStartTime = new DateTime();

		static List<IMyTextPanel> displayPanels = new List<IMyTextPanel>();
		static List<IMyShipDrill> drills = new List<IMyShipDrill>();

		int loopCounter = 0;
		int refreshLoopCount = 6 * SECONDS_BETWEEN_CONTROL_SEARCHES;

		string[] statusString = { "/", "-", "\\", "|" };

		public Program()
		{
			Runtime.UpdateFrequency = UpdateFrequency.Update10;
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
			if (loopCounter == 0)
			{
				Refresh();
			}

			RunCommands(argument, updateSource);

			Update();
			UpdateDisplays();

			WriteControlText();

			lastRunTime += Runtime.TimeSinceLastRun;
			if (loopCounter >= refreshLoopCount) { loopCounter = 0; }
			else { loopCounter++; }
		}

		private void Refresh()
		{
			// TODO: Add functional piston check
			// TODO: Add IsSameConstructAs(Me) check
			pistons.ForEach((p) => p.Refresh(GridTerminalSystem));
			GridTerminalSystem.GetBlocksOfType(displayPanels, ShouldTrack);
			GridTerminalSystem.GetBlocksOfType(drills, ShouldTrack);
		}

		private bool ShouldTrack(IMyFunctionalBlock block)
		{
			return block.IsSameConstructAs(Me) && block.CustomName.Contains($"[{TAG}")
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
			pistons.ForEach((pg) => pg.Stop());
			DrillsOff();
		}

		void Retract()
		{
			ChangeState(State.Retracting);
			pistons.ForEach((pg) => pg.Retract(RETRACT_SPEED));
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

		void Update()
		{
			switch(CurrentState)
			{
				case State.Starting:
					{
						if(drills.All((d) => d.IsWorking) && lastRunTime - stateStartTime >= TimeSpan.FromSeconds(2))
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

						foreach (var pg in pistons)
						{
							if (!pg.IsDrillingComplete())
							{
								pg.Extend(DRILL_PUSH_SPEED);
								break;
							}
						}

						if (pistons.All((pg) => pg.IsDrillingComplete()))
						{
							Finishing();
						}

						break;
					}

				case State.Finishing:
					{
						if(lastRunTime - stateStartTime >= TimeSpan.FromSeconds(10))
						{
							Retract();
						}

						break;
					}

				case State.Retracting:
					{
						if (pistons.All((pg) => pg.IsRetracted()))
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

			foreach (var pg in pistons)
			{
				sb.Append("\n\n");
				sb.Append(pg.GetStatus());
			}

			foreach (var display in displayPanels)
			{
				display.WriteText(sb);
			}
		}

		void WriteControlText()
		{
			StringBuilder sb = new StringBuilder($"Status: {CurrentState}\n\n");

			sb.Append("Paul's Drilling Script " + statusString[loopCounter % 4] + "\n======================\n\n");

			Echo(sb.ToString());
		}
	}
}
