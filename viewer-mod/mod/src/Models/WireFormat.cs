using System.Collections.Generic;
using Newtonsoft.Json;

namespace ViewerMod.Models
{
    /// <summary>
    /// Wire format for a root object entry.
    /// </summary>
    public class RootEntry
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("handleId")]
        public string HandleId { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("assemblyName")]
        public string AssemblyName { get; set; }
    }

    /// <summary>
    /// Wire format for the inspect response.
    /// </summary>
    public class InspectResponse
    {
        [JsonProperty("handleId")]
        public string HandleId { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("assemblyName")]
        public string AssemblyName { get; set; }

        [JsonProperty("value")]
        public object Value { get; set; }

        [JsonProperty("members")]
        public List<MemberData> Members { get; set; }

        [JsonProperty("collectionInfo")]
        public CollectionInfo CollectionInfo { get; set; }
    }

    /// <summary>
    /// Wire format for a member in the inspect response.
    /// </summary>
    public class MemberData
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("assemblyName")]
        public string AssemblyName { get; set; }

        [JsonProperty("isPrimitive")]
        public bool IsPrimitive { get; set; }

        [JsonProperty("handleId")]
        public string HandleId { get; set; }

        [JsonProperty("value")]
        public object Value { get; set; }
    }

    /// <summary>
    /// Wire format for collection information.
    /// </summary>
    public class CollectionInfo
    {
        [JsonProperty("isCollection")]
        public bool IsCollection { get; set; }

        [JsonProperty("count")]
        public int Count { get; set; }

        [JsonProperty("elementType")]
        public string ElementType { get; set; }

        [JsonProperty("elements")]
        public List<CollectionElement> Elements { get; set; }
    }

    /// <summary>
    /// Wire format for a collection element.
    /// </summary>
    public class CollectionElement
    {
        [JsonProperty("index")]
        public int Index { get; set; }

        [JsonProperty("handleId")]
        public string HandleId { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("value")]
        public object Value { get; set; }
    }
}
