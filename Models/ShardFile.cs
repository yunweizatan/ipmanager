namespace IPManage.Models;

public sealed class ShardFile
{
    public string Vlan { get; set; } = string.Empty;

    public string Subnet { get; set; } = string.Empty;

    public List<IpRecord> Records { get; set; } = [];
}
