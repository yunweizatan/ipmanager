namespace IPManage.Models;

public sealed class IpIndex
{
    public int Version { get; set; } = 1;

    public int RecordCount { get; set; }

    public DateTime LastUpdated { get; set; } = DateTime.Now;

    public Dictionary<string, string> IpIndexMap { get; set; } = [];
}
