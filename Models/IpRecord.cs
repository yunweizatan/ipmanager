using System.Text.Json.Serialization;

namespace IPManage.Models;

public sealed class IpRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string IpAddress { get; set; } = string.Empty;

    public string DeviceName { get; set; } = string.Empty;

    public string AssetNumber { get; set; } = string.Empty;

    public string Owner { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string Vlan { get; set; } = string.Empty;

    public string Subnet { get; set; } = string.Empty;

    public string DeviceType { get; set; } = string.Empty;

    public RecordStatus Status { get; set; } = RecordStatus.InUse;

    public DateTime ApplyTime { get; set; } = DateTime.Now;

    public string Remark { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    [JsonIgnore]
    public string ApplyTimeDisplay => ApplyTime.ToString("yyyy-MM-dd HH:mm:ss");

    [JsonIgnore]
    public string StatusDisplay => Status.ToString();

    [JsonIgnore]
    public string StatusText => Status.ToChineseText();

    [JsonIgnore]
    public string MaskedPassword => string.IsNullOrEmpty(Password) ? string.Empty : new string('●', Math.Min(Password.Length, 8));

    [JsonIgnore]
    public string Summary => string.IsNullOrWhiteSpace(DeviceName) ? IpAddress : $"{IpAddress} / {DeviceName}";

    public IpRecord Clone()
    {
        return new IpRecord
        {
            Id = Id,
            IpAddress = IpAddress,
            DeviceName = DeviceName,
            AssetNumber = AssetNumber,
            Owner = Owner,
            Password = Password,
            Vlan = Vlan,
            Subnet = Subnet,
            DeviceType = DeviceType,
            Status = Status,
            ApplyTime = ApplyTime,
            Remark = Remark,
            CreatedAt = CreatedAt,
            UpdatedAt = UpdatedAt
        };
    }
}
