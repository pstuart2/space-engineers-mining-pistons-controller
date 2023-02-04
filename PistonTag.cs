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
		public class PistonTag
		{
			public string Name { get; }

			public bool IsInverted { get; }

			public int SortIndex { get; }

			public PistonTag(string name, int sortIndex, bool isInverted)
			{
				Name = name;
				SortIndex = sortIndex;
				IsInverted = isInverted;
			}

			public override bool Equals(object obj)
			{
				return (obj as PistonTag).Name == Name;
			}

			public override int GetHashCode()
			{
				return Name.GetHashCode();
			}

			public override string ToString()
			{
				return $"{Name}:{SortIndex}:{IsInverted}";
			}
		}
	}
}
