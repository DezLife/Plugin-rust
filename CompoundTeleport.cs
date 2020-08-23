using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("CompoundTeleport", "DezLife", "1.0.0")]
    [Description("Teleport to npc city")]
    class CompoundTeleport : RustPlugin
    {

		#region Variables
		MonumentInfo monument;
		private Dictionary<BasePlayer, SleepingBag> bags = new Dictionary<BasePlayer, SleepingBag>();
		private Queue<SleepingBag> bagsPool = new Queue<SleepingBag>();
		List<Vector3> Positions = new List<Vector3>()
		{
			new Vector3(-6.4f, 0, 3.5f),
			new Vector3(-12.4f, 0, 17.5f),
			new Vector3(27.4f, 3, -17.5f),
			new Vector3(24.4f, 0, 10.5f),
			new Vector3(23.4f, 0, 15.5f),
			new Vector3(12.4f, 0, 17.5f),
			new Vector3(-15.4f, 0, 17.5f),
			new Vector3(-26.4f, 2.55f, 28.5f)
		};
		#endregion

		#region Config

		private static Configuration config = new Configuration();
		private class Configuration
		{
			[JsonProperty("Sleeping bag Cooldown")]
			public int cooldown;
			[JsonProperty("Sleeping bag name")]
			public string bagName;
			public static Configuration GetNewConfiguration()
			{
				return new Configuration
				{
					cooldown = 150,
					bagName = "OUTPOST"
				};
			}
		}

		protected override void LoadConfig()
		{
			base.LoadConfig();
			try
			{
				config = Config.ReadObject<Configuration>();
				if (config == null) LoadDefaultConfig();
			}
			catch
			{
				PrintWarning("Error reading configuration 'oxide/config/{Name}', creating a new configuration !!");
				LoadDefaultConfig();
			}
			NextTick(SaveConfig);
		}

		protected override void LoadDefaultConfig() => config = Configuration.GetNewConfiguration();
		protected override void SaveConfig() => Config.WriteObject(config);


		#endregion

		#region OxideHooks
		private void OnServerInitialized()
		{
			monument = UnityEngine.Object.FindObjectsOfType<MonumentInfo>().FirstOrDefault(p => p.name.Contains("compound"));

			foreach (var player in BasePlayer.activePlayerList)
				OnPlayerConnected(player);
		}
		object OnPlayerRespawn(BasePlayer p, SleepingBag bag)
		{
			if (bag.net.ID == bags[p]?.net.ID)
			{
				bag.transform.position = monument.transform.position + monument.transform.rotation * Positions.GetRandom();
			}
			return false;
		}
		object OnServerCommand(ConsoleSystem.Arg arg)
		{
			uint netId = arg.GetUInt(0, 0);
			if (arg.cmd.Name.ToLower() == "respawn_sleepingbag_remove" && netId != 0)
			{
				BasePlayer basePlayer = arg.Player();
				if (!basePlayer)
					return false;

				if (bags[basePlayer]?.net?.ID == netId)
					return false;
			}
			return null;
		}
		void Unload()
		{
			foreach (var player in BasePlayer.activePlayerList)
				OnPlayerDisconnected(player);
		}

		private void OnPlayerConnected(BasePlayer d)
		{
			SleepingBag bag = FromPool(d);

			if (!bags.ContainsKey(d))
				bags.Add(d, bag);

			SleepingBag.sleepingBags.Add(bag);
		}

		private void OnPlayerDisconnected(BasePlayer d)
		{
			SleepingBag bag;

			if (!bags.TryGetValue(d, out bag))
				return;

			SleepingBag.sleepingBags.Remove(bag);
			ResetToPool(bag);
		}
		#endregion

		#region Helpers
		private SleepingBag FromPool(BasePlayer d)
		{
			SleepingBag bag;

			if (bagsPool.Count > 0)
			{
				bag = bagsPool.Dequeue();
				bag.deployerUserID = d.userID;

				return bag;
			}

			GameObject go = new GameObject();
			bag = go.AddComponent<SleepingBag>();

			bag.deployerUserID = d.userID;
			bag.net = Network.Net.sv.CreateNetworkable();

			bag.niceName = config.bagName;
			bag.transform.position = monument.transform.position;
			bag.RespawnType = ProtoBuf.RespawnInformation.SpawnOptions.RespawnType.Bed;
			bag.unlockTime = 0;
			bag.secondsBetweenReuses = config.cooldown;

			return bag;
		}

		private void ResetToPool(SleepingBag bag)
		{
			bagsPool.Enqueue(bag);
		}
		#endregion

	}
}
