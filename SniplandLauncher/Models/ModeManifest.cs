using System.Collections.Generic;
using Newtonsoft.Json;

namespace SniplandLauncher.Models
{
    public class ModeManifest
    {
        [JsonProperty("files")]
        public List<FileEntry>? Files { get; set; }

        [JsonProperty("update_cleaning")]
        public List<string>? UpdateCleaning { get; set; }
    }

    public class FileEntry
    {
        [JsonProperty("path")]
        public string? Path { get; set; }

        [JsonProperty("size")]
        public long Size { get; set; }

        [JsonProperty("hash")]
        public string? Hash { get; set; }

        [JsonProperty("url")]
        public string? Url { get; set; }
    }
}
