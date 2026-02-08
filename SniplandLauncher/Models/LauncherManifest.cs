using System.Collections.Generic;
using Newtonsoft.Json;

namespace SniplandLauncher.Models
{
    public class LauncherManifest
    {
        [JsonProperty("maintenance_message")]
        public string? MaintenanceMessage { get; set; }

        [JsonProperty("modes")]
        public List<GameMode>? Modes { get; set; }
    }

    public class GameMode
    {
        [JsonProperty("id")]
        public string? Id { get; set; }

        [JsonProperty("name")]
        public string? Name { get; set; }

        [JsonProperty("version")]
        public string? Version { get; set; }

        [JsonProperty("minecraft_version")]
        public string? MinecraftVersion { get; set; }

        [JsonProperty("loader")]
        public GameLoader? Loader { get; set; }

        [JsonProperty("description")]
        public string? Description { get; set; }

        [JsonProperty("icon_url")]
        public string? IconUrl { get; set; }

        [JsonProperty("banner_url")]
        public string? BannerUrl { get; set; }

        [JsonProperty("remote_manifest_url")]
        public string? RemoteManifestUrl { get; set; }
    }

    public class GameLoader
    {
        [JsonProperty("type")]
        public string? Type { get; set; }

        [JsonProperty("version")]
        public string? Version { get; set; }
    }
}
