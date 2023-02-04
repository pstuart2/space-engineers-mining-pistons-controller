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
		public class PistonGroup
		{
			public PistonTag Tag { get; }

			private string SearchTag;

			List<IMyExtendedPistonBase> pistons = new List<IMyExtendedPistonBase>();

			public PistonGroup(PistonTag tag, string searchTag)
			{
				Tag = tag;
				SearchTag = searchTag;
			}

			public void Refresh(IMyGridTerminalSystem grid)
			{
				pistons.Clear();
				grid.GetBlocksOfType(pistons, ShouldTrackPiston);
			}

			private bool ShouldTrackPiston(IMyExtendedPistonBase piston)
			{
				return piston.CustomName.Contains($"[{SearchTag}:P:{Tag.Name}:");
			}

			public void Retract(float velocity)
			{
				pistons.ForEach((p) =>
				{
					p.Velocity = velocity;
					if (Tag.IsInverted)
					{
						p.Extend();
					} else
					{
						p.Retract();
					}
				});
			}

			public void Extend(float velocity)
			{
				pistons.ForEach((p) =>
				{
					p.Velocity = velocity;
					if (Tag.IsInverted)
					{
						p.Retract();
					}
					else
					{
						p.Extend();
					}
				});
			}

			public void Stop()
			{
				pistons.ForEach((p) => p.Velocity = 0.0f);
			}

			public bool IsDrilling()
			{
				return Tag.IsInverted ? pistons[0].Status == PistonStatus.Retracting : pistons[0].Status == PistonStatus.Extending;
			}

			public bool IsDrillingComplete()
			{
				var first = pistons[0];
				return Tag.IsInverted ? first.CurrentPosition == 0.0f : first.CurrentPosition == first.MaxLimit;
			}

			public bool IsRetracted()
			{
				var first = pistons[0];
				return Tag.IsInverted ? first.CurrentPosition == first.MaxLimit : first.CurrentPosition == 0.0f;
			}

			public float GetPrecentageComplete()
			{
				var first = pistons[0];
				if (Tag.IsInverted)
				{
					return (first.MaxLimit - first.CurrentPosition) / first.MaxLimit;
				}

				return (first.MaxLimit - (first.MaxLimit - first.CurrentPosition)) / first.MaxLimit;
			}

			public string GetPrecentageCompleteFormatted()
			{
				return $"{GetPrecentageComplete():P0}";
			}

			public string PistonDrillStatus()
			{
				if (IsDrillingComplete())
				{
					return "complete";
				}

				if (IsDrilling())
				{
					return "drilling";
				}

				return "waiting";
			}

			public string GetStatus()
			{
				var first = pistons[0];
				return $"{Tag.Name}({pistons.Count}) {first.CurrentPosition:F1}m ({GetPrecentageCompleteFormatted()}) - {PistonDrillStatus()}...";
			}
		}
	}
}
