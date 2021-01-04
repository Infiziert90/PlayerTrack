﻿// ReSharper disable InvertIf
// ReSharper disable ConvertIfStatementToSwitchStatement

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace PlayerTrack
{
	public class RosterService : IRosterService
	{
		private readonly Queue<string> _deleteRequests = new Queue<string>();
		private readonly JsonSerializerSettings _jsonSerializerSettings;
		private readonly IPlayerTrackPlugin _playerTrackPlugin;

		public RosterService(IPlayerTrackPlugin playerTrackPlugin)
		{
			_playerTrackPlugin = playerTrackPlugin;
			Current = new TrackRoster(new Dictionary<string, TrackPlayer>(), _playerTrackPlugin);
			_jsonSerializerSettings = SerializerUtil.CamelCaseJsonSerializer();
			InitRoster();
			LoadRoster();
		}

		public TrackRoster Current { get; set; }
		public TrackRoster All { get; set; }
		public TrackPlayer SelectedPlayer { get; set; }


		public void ClearPlayers()
		{
			Current.Roster = new Dictionary<string, TrackPlayer>();
		}

		public void ProcessPlayers(List<TrackPlayer> incomingPlayers)
		{
			ProcessDeleteRequests();
			ProcessVerificationRequests();
			ProcessUpdateRequests();
			var currentPlayers = new TrackRoster(new Dictionary<string, TrackPlayer>(), _playerTrackPlugin);
			foreach (var player in incomingPlayers)
			{
				var encounter = player.Encounters.Last();
				if (All.IsNewPlayer(player.Key))
				{
					All.AddPlayer(player);
				}
				else
				{
					if (All.IsNewEncounter(player.Key, encounter))
						All.AddEncounter(player.Key, encounter);
					else
						All.UpdateEncounter(player.Key, encounter);
					All.UpdatePlayer(player);
				}

				var currentPlayer = All.GetPlayer(player.Key);
				SubmitVerificationRequest(currentPlayer);
				currentPlayer.ClearBackingFields();
				try
				{
					currentPlayers.AddPlayer(currentPlayer);
				}
				catch (Exception ex)
				{
					_playerTrackPlugin.LogError(ex, "Failed to add to currentPlayer.. " + currentPlayer.Key);
				}
			}

			currentPlayers.SortByName();
			Current = currentPlayers;
		}

		public void DeletePlayer(string key)
		{
			_deleteRequests.Enqueue(key);
		}

		public void ChangeSelectedPlayer(string key)
		{
			SelectedPlayer = All.GetPlayer(key);
		}

		public void BackupRoster(bool forceBackup = false)
		{
			if (forceBackup || (DateTime.UtcNow - _playerTrackPlugin.Configuration.LastBackup.ToDateTime())
				.TotalMilliseconds >
				_playerTrackPlugin.Configuration.BackupFrequency)
			{
				_playerTrackPlugin.GetDataManager().CreateBackup();
				_playerTrackPlugin.GetDataManager().DeleteBackups(_playerTrackPlugin.Configuration.BackupRetention);
				_playerTrackPlugin.Configuration.LastBackup = DateUtil.CurrentTime();
				_playerTrackPlugin.SaveConfig();
			}
		}

		public void SaveData()
		{
			try
			{
				var data = JsonConvert.SerializeObject(All.Roster, _jsonSerializerSettings);
				var metaData = JsonConvert.SerializeObject(new TrackMetaData
				{
					SchemaVersion = _playerTrackPlugin.Configuration.SchemaVersion,
					Compressed = _playerTrackPlugin.Configuration.Compressed
				}, _jsonSerializerSettings);
				if (_playerTrackPlugin.Configuration.Compressed) data = data.Compress();
				_playerTrackPlugin.GetDataManager().SaveData("players.dat", data);
				_playerTrackPlugin.GetDataManager().SaveData("data.meta", metaData);
				BackupRoster();
			}
			catch (Exception ex)
			{
				_playerTrackPlugin.LogError(ex, "Failed to save player data - will try again soon.");
			}
		}

		private void SubmitUpdateRequest(TrackPlayer player)
		{
			if (!_playerTrackPlugin.Configuration.SyncToLodestone) return;
			if (player.Lodestone.Status == TrackLodestoneStatus.Verified && DateUtil.CurrentTime() >
				player.Lodestone?.LastUpdated +
				_playerTrackPlugin.Configuration.LodestoneUpdateFrequency)
				_playerTrackPlugin.GetLodestoneService().AddUpdateRequest(new TrackLodestoneRequest
				{
					PlayerKey = player.Key,
					LodestoneId = player.Lodestone.Id
				});
		}

		private void ProcessUpdateRequests()
		{
			var responses = _playerTrackPlugin.GetLodestoneService().GetUpdateResponses();
			foreach (var response in responses)
			{
				var player = All.GetPlayer(response.PlayerKey);

				if (player.IsNewName(response.PlayerName) ||
				    player.IsNewHomeWorld(response.HomeWorld))
				{
					All.DeletePlayer(player.Key);
					player.UpdateName(response.PlayerName);
					player.UpdateHomeWorld(response.HomeWorld);
					player.ClearBackingFields();
					if (All.IsNewPlayer(player.Key))
					{
						All.AddPlayer(player);
					}
					else
					{
						All.MergePlayer(player);
						player = All.GetPlayer(player.Key);
					}
				}

				player.Lodestone.Status = response.Status;
				player.Lodestone.LastUpdated = DateUtil.CurrentTime();
				HandleFailure(player);
			}
		}

		private static void HandleFailure(TrackPlayer player)
		{
			if (player.Lodestone.Status == TrackLodestoneStatus.Failed)
			{
				player.Lodestone.LastFailed = player.Lodestone.LastUpdated;
				player.Lodestone.FailureCount++;
			}
			else
			{
				player.Lodestone.LastFailed = 0;
				player.Lodestone.FailureCount = 0;
				player.Lodestone.LastFailed = 0;
			}
		}

		private void SubmitVerificationRequest(TrackPlayer player)
		{
			if (!_playerTrackPlugin.Configuration.SyncToLodestone) return;
			if (player.Lodestone.Status == TrackLodestoneStatus.Unverified)
			{
				_playerTrackPlugin.GetLodestoneService().AddIdRequest(new TrackLodestoneRequest
				{
					PlayerKey = player.Key,
					PlayerName = player.Name,
					WorldName = player.HomeWorld
				});
				player.Lodestone.Status = TrackLodestoneStatus.Verifying;
			}
		}

		private void ProcessVerificationRequests()
		{
			var responses = _playerTrackPlugin.GetLodestoneService().GetVerificationResponses();
			foreach (var response in responses)
			{
				var player = All.GetPlayer(response.PlayerKey);
				player.Lodestone.Id = response.LodestoneId;
				player.Lodestone.Status = response.Status;
				player.Lodestone.LastUpdated = DateUtil.CurrentTime();
				HandleFailure(player);
			}
		}

		private void ProcessDeleteRequests()
		{
			while (_deleteRequests.Count > 0)
			{
				var playerKey = _deleteRequests.Dequeue();
				All.DeletePlayer(playerKey);
			}
		}

		private void InitRoster()
		{
			try
			{
				_playerTrackPlugin.GetDataManager().InitDataFiles(new[] {"players.dat", "data.meta"});
			}
			catch
			{
				_playerTrackPlugin.LogInfo("Failed to properly initialize but probably will be fine.");
			}
		}

		private void LoadRoster()
		{
			TrackRoster loadedRoster;
			var currentTime = DateUtil.CurrentTime();
			try
			{
				var data = _playerTrackPlugin.GetDataManager().ReadData("players.dat");
				var meta = _playerTrackPlugin.GetDataManager().ReadData("data.meta");
				var metaData = JsonConvert.DeserializeObject<TrackMetaData>(meta, _jsonSerializerSettings);
				if (metaData.Compressed) data = data.Decompress();
				loadedRoster = new TrackRoster(
					JsonConvert.DeserializeObject<Dictionary<string, TrackPlayer>>(data, _jsonSerializerSettings),
					_playerTrackPlugin);

				foreach (var player in loadedRoster.Roster)
				{
					foreach (var world in player.Value.HomeWorlds)
						world.Name = _playerTrackPlugin.GetWorldName(world.Id);
					foreach (var encounter in player.Value.Encounters)
					{
						encounter.Location.PlaceName =
							_playerTrackPlugin.GetPlaceName(encounter.Location.TerritoryType);
						encounter.Location.ContentName =
							_playerTrackPlugin.GetContentName(
								_playerTrackPlugin.GetContentId(encounter.Location.TerritoryType));
						encounter.Job.Code = _playerTrackPlugin.GetJobCode(encounter.Job.Id);
					}

					var lode = player.Value.Lodestone;
					if (lode?.Status != null)
					{
						if (lode.Status == TrackLodestoneStatus.Verifying)
							lode.Status = TrackLodestoneStatus.Unverified;
						else if (lode.Status == TrackLodestoneStatus.Updating)
							lode.Status = TrackLodestoneStatus.Verified;

						if (lode.Status == TrackLodestoneStatus.Unverified)
						{
							SubmitVerificationRequest(player.Value);
						}
						else if (lode.Status == TrackLodestoneStatus.Verified)
						{
							SubmitUpdateRequest(player.Value);
						}
						else if (lode.Status == TrackLodestoneStatus.Failed &&
						         lode.FailureCount < _playerTrackPlugin.Configuration.LodestoneMaxFailure &&
						         currentTime < lode.LastFailed + _playerTrackPlugin.Configuration.LodestoneFailureDelay)
						{
							player.Value.Lodestone.Status = TrackLodestoneStatus.Unverified;
							SubmitVerificationRequest(player.Value);
						}
					}
				}
			}
			catch
			{
				_playerTrackPlugin.LogInfo("Can't load data so starting fresh.");
				loadedRoster = new TrackRoster(new Dictionary<string, TrackPlayer>(), _playerTrackPlugin);
			}

			All = loadedRoster;
		}
	}
}