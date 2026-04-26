using System.Net;
using IPManage.Models;

namespace IPManage.Validators;

public sealed class IpRecordValidator
{
    public IEnumerable<string> Validate(IpRecord record, IEnumerable<IpRecord> allRecords)
    {
        if (string.IsNullOrWhiteSpace(record.IpAddress))
        {
            yield return "IP 地址不能为空。";
        }
        else if (!IPAddress.TryParse(record.IpAddress, out var parsed) || parsed.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
        {
            yield return "IP 地址格式不正确，当前仅支持 IPv4。";
        }

        if (record.Status != RecordStatus.Idle && string.IsNullOrWhiteSpace(record.DeviceName))
        {
            yield return "设备名称不能为空，空闲状态可留空。";
        }

        if (string.IsNullOrWhiteSpace(record.Vlan))
        {
            yield return "VLAN 不能为空。";
        }

        if (record.ApplyTime == default)
        {
            yield return "申请时间不能为空。";
        }

        if (allRecords.Any(existing =>
                !string.Equals(existing.Id, record.Id, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(existing.IpAddress, record.IpAddress, StringComparison.OrdinalIgnoreCase)))
        {
            yield return "IP 地址已存在，不能重复录入。";
        }
    }
}
