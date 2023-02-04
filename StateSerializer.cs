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
		public static class StateSerializer
		{
			public static string[] separator = new string[] { "\n" };
			public static string serialized;
			public static Queue<string> deserialized;
			public static void InitPack()
			{
				serialized = "";
			}
			public static void Pack(string str)
			{
				serialized += str.Replace(separator[0], " ") + separator[0];
			}
			public static void Pack(int val)
			{
				serialized += val.ToString() + separator[0];
			}
			public static void Pack(long val)
			{
				serialized += val.ToString() + separator[0];
			}
			public static void Pack(float val)
			{
				serialized += val.ToString() + separator[0];
			}
			public static void Pack(double val)
			{
				serialized += val.ToString() + separator[0];
			}
			public static void Pack(bool val)
			{
				serialized += (val ? "1" : "0") + separator[0];
			}
			
			public static void InitUnpack(string str)
			{
				deserialized = new Queue<string>(str.Split(separator, StringSplitOptions.None));
			}
			public static string UnpackString()
			{
				return deserialized.Dequeue();
			}
			public static int UnpackInt()
			{
				return int.Parse(deserialized.Dequeue());
			}
			public static long UnpackLong()
			{
				return long.Parse(deserialized.Dequeue());
			}
			public static float UnpackFloat()
			{
				return float.Parse(deserialized.Dequeue());
			}
			public static double UnpackDouble()
			{
				return double.Parse(deserialized.Dequeue());
			}
			public static bool UnpackBool()
			{
				return deserialized.Dequeue() == "1";
			}
		}
	}
}
