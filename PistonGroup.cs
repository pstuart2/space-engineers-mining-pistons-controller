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
			public int Index { get; }

			public string Name { get; private set; }

			public string PistonTag { get; private set; }

			public bool IsInverted { get; private set; }

			public float UpperPistonLimit { get; private set; } = CFG_UpperPistonLimit;
			public float LowerPistonLimit { get; private set; } = CFG_LowerPistonLimit;

			List<IMyExtendedPistonBase> pistons = new List<IMyExtendedPistonBase>();

			public static int ParseIndex(string name)
			{
				var tagString = $"[{SEARCH_TAG}.";
				var startIndex = name.IndexOf(tagString) + tagString.Length;
				var endIndex = name.IndexOf(']', startIndex);

				var indexString = name.Substring(startIndex, endIndex - startIndex);

				return int.Parse(indexString);
			}

			public PistonGroup(int index)
			{
				Index = index;
				PistonTag = $"{SEARCH_TAG}.{Index}";
				SetDefaultName();
			}

			public void Refresh(IMyGridTerminalSystem grid)
			{
				SetDefaultName();
				pistons.Clear();
				
				grid.GetBlocksOfType(pistons, ShouldTrackPiston);

				ParseConfig();

				UpdatePistonSettings();
			}

			private void SetDefaultName()
			{
				Name = $"Set.{Index}" + (IsInverted ? "i" : "");
			}

			private bool ShouldTrackPiston(IMyExtendedPistonBase piston)
			{
				return piston.CustomName.Contains($"[{PistonTag}]");
			}

			private void ParseConfig()
			{
				bool outBool;
				if (CB_IniConfig.Get(PistonTag, "Inverted").TryGetBoolean(out outBool))
				{
					IsInverted = outBool;
				}

				LowerPistonLimit = CB_IniConfig.Get(SEARCH_TAG, "LowerPistonLimit").ToSingle(CFG_LowerPistonLimit);
				UpperPistonLimit = CB_IniConfig.Get(SEARCH_TAG, "UpperPistonLimit").ToSingle(CFG_UpperPistonLimit);

				string outString;
				if (CB_IniConfig.Get(PistonTag, "Name").TryGetString(out outString))
				{
					Name = outString;
				} else
				{
					SetDefaultName();
				}
			}

			private void UpdatePistonSettings()
			{
				pistons.ForEach((p) =>
				{
					p.SetValueFloat("LowerLimit", LowerPistonLimit);
					p.SetValueFloat("UpperLimit", UpperPistonLimit);
				});
			}

			public void Retract()
			{
				pistons.ForEach((p) =>
				{
					p.Velocity = CFG_RetractSpeed;
					if (IsInverted)
					{
						p.Extend();
					} else
					{
						p.Retract();
					}
				});
			}

			public void Extend()
			{
				pistons.ForEach((p) =>
				{
					p.Velocity = CFG_DrillPushSpeed;
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
				var first = pistons.First();
				return IsInverted ? first.CurrentPosition == 0.0f : first.CurrentPosition == first.MaxLimit;
			}

			public bool IsRetracted()
			{
				var first = pistons.First();
				return IsInverted ? first.CurrentPosition == first.MaxLimit : first.CurrentPosition == 0.0f;
			}

			public float GetPrecentageComplete()
			{
				var first = pistons.First();
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
				var first = pistons.First();
				return string.Format("{0} ({1})\n{2,5:F1}m {3,5:P0} - {4}", Name, pistons.Count, first.CurrentPosition, GetPrecentageComplete(), PistonDrillStatus());
			}
		}
	}
}
