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
	partial class Program
	{
		public class DrillController
		{
			private IMyProgrammableBlock MyPb;

			List<IMyShipDrill> drills = new List<IMyShipDrill>();

			public int Count { get { return drills.Count; } }

			public DrillController(IMyProgrammableBlock me)
			{
				this.MyPb = me;
			}

			public void Refresh(IMyGridTerminalSystem grid)
			{
				drills.Clear();
				grid.GetBlocksOfType(drills, ShouldTrackDrill);
			}

			public void TurnOn()
			{
				drills.ForEach((d) => d.ApplyAction("OnOff_On"));
			}

			public void TurnOff()
			{
				drills.ForEach((d) => d.ApplyAction("OnOff_Off"));
			}

			public string GetStateText()
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

			private bool ShouldTrackDrill(IMyShipDrill drill)
			{
				return CFG_AllDrills || drill.IsSameConstructAs(MyPb)
					&& drill.IsFunctional
					&& drill.CustomName.Contains($"[{SEARCH_TAG}]");
			}
		}


	}
}
