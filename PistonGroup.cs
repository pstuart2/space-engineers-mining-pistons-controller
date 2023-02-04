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
			public string Name { get; }
			
			public string [] PistonNames { get; }
			public bool IsInverted { get; }

			List<IMyExtendedPistonBase> pistons = new List<IMyExtendedPistonBase>();

			public PistonGroup(string name, string pistonNames, bool isInverted)
			{
				Name = name;
				PistonNames = pistonNames.Split(';');
				IsInverted = isInverted;
			}

			public void Refresh(IMyGridTerminalSystem grid)
			{
				pistons.Clear();
				grid.GetBlocksOfType(pistons, ShouldTrackPiston);
			}

			private bool ShouldTrackPiston(IMyExtendedPistonBase piston)
			{
				return PistonNames.Contains(piston.CustomName);
			}

			public void Retract(float velocity)
			{
				pistons.ForEach((p) =>
				{
					p.Velocity = velocity;
					if (IsInverted)
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
					if (IsInverted)
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
				return IsInverted ? pistons[0].Status == PistonStatus.Retracting : pistons[0].Status == PistonStatus.Extending;
			}

			public bool IsDrillingComplete()
			{
				var first = pistons[0];
				return IsInverted ? first.CurrentPosition == 0.0f : first.CurrentPosition == first.MaxLimit;
			}

			public bool IsRetracted()
			{
				var first = pistons[0];
				return IsInverted ? first.CurrentPosition == first.MaxLimit : first.CurrentPosition == 0.0f;
			}

			public float GetPrecentageComplete()
			{
				var first = pistons[0];
				if (IsInverted)
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
				return $"{Name}({pistons.Count}) {first.CurrentPosition:F1}m ({GetPrecentageCompleteFormatted()}) - {PistonDrillStatus()}...";
			}
		}
	}
}
