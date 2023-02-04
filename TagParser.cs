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
		public static class TagParser
		{
			// [TAG:P:Set Name:SortOrder:Inverted?]
			// [MPC:P:Set 1:0:Inverted]
			public static string SEARCH_TAG = "";

			public static bool ContainsPistonTag(string value)
			{
				return value.Contains($"[{SEARCH_TAG}:P:");
			}

			public static PistonTag ParsePistonTag(string value)
			{
				var pattern = $"\\[{SEARCH_TAG}:P:(?<name>[a-zA-Z--9\\ ]+):(?<sort>\\d+):?(?<inverted>(i|I|1|t|T|true|True|TRUE)?)]";
				var match = System.Text.RegularExpressions.Regex.Match(value, pattern);

				return new PistonTag(
					name: match.Groups["name"].Value, 
					sortIndex: int.Parse(match.Groups["sort"].Value), 
					isInverted: !string.IsNullOrEmpty(match.Groups["inverted"].Value)
					);

			}
		}
	}
}
