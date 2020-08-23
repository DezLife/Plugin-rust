﻿using System.Collections.Generic;
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
		private Dictionary<string, MonumentInfo> positions = new Dictionary<string, MonumentInfo>();
		private Dictionary<BasePlayer, SleepingBag[]> bags = new Dictionary<BasePlayer, SleepingBag[]>();
		private Queue<SleepingBag> bagsPool = new Queue<SleepingBag>();
		List<Vector3> PositionsOutPost = new List<Vector3>()
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
		List<Vector3> PositionsBandit = new List<Vector3>()
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
			[JsonProperty("The opportunity respawn in a outpost")]
			public bool outPostRespawn;
			[JsonProperty("Name bag outpost")]
			public string bagNameOutPost;
			[JsonProperty("The opportunity respawn in a Bandit Town")]
			public bool banditRespawn;
			[JsonProperty("Name bag Bandit Town")]
			public string bagNameBandit;
			public static Configuration GetNewConfiguration()
			{
				return new Configuration
				{
					cooldown = 150,
					outPostRespawn = true,
					bagNameOutPost = "OUTPOST",
					banditRespawn = true,
					bagNameBandit = "BANDIT TOWN"
					
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
        void Unload()
        {
            foreach (var player in BasePlayer.activePlayerList)
                OnPlayerDisconnected(player);
        }

        object OnPlayerRespawn(BasePlayer p, SleepingBag bag)
        {
			if(bags[p].Where(x => x.net.ID == bag.net.ID).Count() > 0)
            {
				var pos = bag.niceName == config.bagNameBandit ? PositionsBandit : PositionsOutPost;
				bag.transform.position = positions[bag.niceName].transform.position + positions[bag.niceName].transform.rotation * pos.GetRandom();
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

                if (bags[basePlayer].Where(bag => bag.net.ID == netId).Count() != 0)
                    return false;
            }
            return null;
        }
        private void OnServerInitialized()
		{
			foreach (MonumentInfo monument in UnityEngine.Object.FindObjectsOfType<MonumentInfo>())
			{
				if (monument.name.ToLower().Contains("compound") && config.outPostRespawn)
                {
					positions.Add(config.bagNameOutPost, monument);
				}
				else if (monument.name.Contains("bandit") && config.banditRespawn)
                {
					positions.Add(config.bagNameBandit, monument);
				}
			}
			foreach (var player in BasePlayer.activePlayerList)
				OnPlayerConnected(player);
		}
		private void OnPlayerConnected(BasePlayer d)
		{
			int count = positions.Count, idx = -1;

			if (!bags.ContainsKey(d))
				bags.Add(d, new SleepingBag[count]);

			foreach (var positionKvp in positions)
			{
				SleepingBag bag = FromPool(d);
				bag.niceName = positionKvp.Key;
				bag.transform.position = positionKvp.Value.transform.position;

				bags[d][++idx] = bag;

				SleepingBag.sleepingBags.Add(bag);
			}
		}

		private void OnPlayerDisconnected(BasePlayer d)
		{
			if (!bags.ContainsKey(d))
				return;

			foreach (SleepingBag bag in bags[d])
			{
				SleepingBag.sleepingBags.Remove(bag);
				ResetToPool(bag);
			}
		}
        #endregion

        #region Helpers
        private SleepingBag FromPool(BasePlayer d)
		{
			SleepingBag bag;

			if (bagsPool.Count > 0)
			{
				bag = bagsPool.Dequeue();
				bag.OwnerID = d.userID;

				return bag;
			}

			GameObject go = new GameObject();
			bag = go.AddComponent<SleepingBag>();

			bag.deployerUserID = d.userID;
			bag.net = Network.Net.sv.CreateNetworkable();

			bag.secondsBetweenReuses = config.cooldown;
			bag.transform.position = Vector3.one;
			bag.RespawnType = ProtoBuf.RespawnInformation.SpawnOptions.RespawnType.Bed;
			bag.unlockTime = 0;

			return bag;
		}

		private void ResetToPool(SleepingBag bag)
		{
			bagsPool.Enqueue(bag);
		}
        #endregion
    }
}
