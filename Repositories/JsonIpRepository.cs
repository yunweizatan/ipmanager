using System.Net;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using IPManage.Models;

namespace IPManage.Repositories;

public sealed class JsonIpRepository : IIpRepository
{
    private const string EncryptedPasswordPrefix = "enc:";
    private const string IndexFileName = "index.json";
    private const string ShardsDirectoryName = "shards";
    private const string BackupDirectoryName = "backup";
    private const string ExportsDirectoryName = "exports";

    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public JsonIpRepository()
    {
        DataRoot = Path.Combine(AppContext.BaseDirectory, "data");
    }

    public string DataRoot { get; }

    public IReadOnlyList<IpRecord> LoadAll()
    {
        EnsureStorageExists();

        var indexPath = GetIndexPath();
        if (!File.Exists(indexPath))
        {
            var seedRecords = CreateSeedRecords();
            SaveAll(seedRecords);
            return seedRecords;
        }

        var index = DeserializeFile<IpIndex>(indexPath) ?? new IpIndex();
        var results = new List<IpRecord>();

        foreach (var relativeShardPath in index.IpIndexMap.Values.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var shardPath = Path.Combine(DataRoot, relativeShardPath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(shardPath))
            {
                continue;
            }

            var shard = DeserializeFile<ShardFile>(shardPath);
            if (shard?.Records is { Count: > 0 })
            {
                RestoreSensitiveFields(shard.Records);
                results.AddRange(shard.Records);
            }
        }

        if (results.Count == 0)
        {
            results = CreateSeedRecords().ToList();
            SaveAll(results);
        }

        return results
            .OrderBy(record => SortableIp(record.IpAddress))
            .ThenBy(record => record.DeviceName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public void SaveAll(IReadOnlyCollection<IpRecord> records)
    {
        EnsureStorageExists();

        var shardRoot = GetShardsPath();
        Directory.CreateDirectory(shardRoot);

        var grouped = records
            .Select(record =>
            {
                NormalizeRecord(record);
                return PrepareForStorage(record);
            })
            .GroupBy(record => BuildShardFileName(record), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => new ShardFile
                {
                    Vlan = group.First().Vlan,
                    Subnet = group.First().Subnet,
                    Records = group
                        .OrderBy(record => SortableIp(record.IpAddress))
                        .ThenBy(record => record.DeviceName, StringComparer.CurrentCultureIgnoreCase)
                        .ToList()
                },
                StringComparer.OrdinalIgnoreCase);

        foreach (var existingFile in Directory.GetFiles(shardRoot, "*.json"))
        {
            var fileName = Path.GetFileName(existingFile);
            if (!grouped.ContainsKey(fileName))
            {
                File.Delete(existingFile);
            }
        }

        foreach (var entry in grouped)
        {
            WriteJsonAtomic(Path.Combine(shardRoot, entry.Key), entry.Value);
        }

        var index = new IpIndex
        {
            Version = 1,
            RecordCount = records.Count,
            LastUpdated = DateTime.Now,
            IpIndexMap = records.ToDictionary(
                record => record.IpAddress,
                record => $"{ShardsDirectoryName}/{BuildShardFileName(record)}",
                StringComparer.OrdinalIgnoreCase)
        };

        WriteJsonAtomic(GetIndexPath(), index);
    }

    public string CreateBackup()
    {
        EnsureStorageExists();

        var backupDirectory = Path.Combine(DataRoot, BackupDirectoryName);
        Directory.CreateDirectory(backupDirectory);

        var targetRoot = Path.Combine(backupDirectory, $"backup_{DateTime.Now:yyyyMMdd_HHmmss}");
        Directory.CreateDirectory(targetRoot);

        var indexPath = GetIndexPath();
        if (File.Exists(indexPath))
        {
            File.Copy(indexPath, Path.Combine(targetRoot, IndexFileName), overwrite: true);
        }

        var sourceShards = GetShardsPath();
        var targetShards = Path.Combine(targetRoot, ShardsDirectoryName);
        Directory.CreateDirectory(targetShards);

        foreach (var file in Directory.GetFiles(sourceShards, "*.json"))
        {
            File.Copy(file, Path.Combine(targetShards, Path.GetFileName(file)), overwrite: true);
        }

        return targetRoot;
    }

    public string GetExportsPath()
    {
        var exportDirectory = Path.Combine(DataRoot, ExportsDirectoryName);
        Directory.CreateDirectory(exportDirectory);
        return exportDirectory;
    }

    private static IReadOnlyList<IpRecord> CreateSeedRecords()
    {
        return [];
    }

    private void EnsureStorageExists()
    {
        Directory.CreateDirectory(DataRoot);
        Directory.CreateDirectory(GetShardsPath());
        Directory.CreateDirectory(Path.Combine(DataRoot, BackupDirectoryName));
        Directory.CreateDirectory(Path.Combine(DataRoot, ExportsDirectoryName));
    }

    private string GetIndexPath() => Path.Combine(DataRoot, IndexFileName);

    private string GetShardsPath() => Path.Combine(DataRoot, ShardsDirectoryName);

    private string BuildShardFileName(IpRecord record)
    {
        var vlan = string.IsNullOrWhiteSpace(record.Vlan) ? "unassigned" : record.Vlan.Trim().ToLowerInvariant();
        var subnet = string.IsNullOrWhiteSpace(record.Subnet) ? InferSubnet(record.IpAddress) : record.Subnet.Trim();
        var safeSubnet = subnet.Replace("/", "_").Replace(".", "-");
        return $"{vlan}_{safeSubnet}.json";
    }

    private static string InferSubnet(string ipAddress)
    {
        if (IPAddress.TryParse(ipAddress, out var parsed) && parsed.GetAddressBytes() is [var a, var b, var c, _])
        {
            return $"{a}.{b}.{c}.0/24";
        }

        return "unknown_0_24";
    }

    private void NormalizeRecord(IpRecord record)
    {
        record.Id = string.IsNullOrWhiteSpace(record.Id) ? Guid.NewGuid().ToString("N") : record.Id;
        record.IpAddress = record.IpAddress.Trim();
        record.DeviceName = record.DeviceName.Trim();
        record.AssetNumber = record.AssetNumber.Trim();
        record.Owner = record.Owner.Trim();
        record.Password = record.Password.Trim();
        record.Vlan = record.Vlan.Trim();
        record.DeviceType = record.DeviceType.Trim();
        record.Subnet = string.IsNullOrWhiteSpace(record.Subnet) ? InferSubnet(record.IpAddress) : record.Subnet.Trim();
        record.Remark = record.Remark.Trim();
        record.CreatedAt = record.CreatedAt == default ? DateTime.Now : record.CreatedAt;
        record.UpdatedAt = DateTime.Now;
    }

    private static void RestoreSensitiveFields(IEnumerable<IpRecord> records)
    {
        foreach (var record in records)
        {
            if (string.IsNullOrWhiteSpace(record.Password))
            {
                continue;
            }

            record.Password = UnprotectPassword(record.Password);
        }
    }

    private static IpRecord PrepareForStorage(IpRecord record)
    {
        var clone = record.Clone();
        clone.Password = ProtectPassword(clone.Password);
        return clone;
    }

    private static string ProtectPassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.StartsWith(EncryptedPasswordPrefix, StringComparison.Ordinal))
        {
            return password;
        }

        try
        {
            var plainBytes = Encoding.UTF8.GetBytes(password);
            var encryptedBytes = ProtectedData.Protect(plainBytes, optionalEntropy: null, DataProtectionScope.CurrentUser);
            return EncryptedPasswordPrefix + Convert.ToBase64String(encryptedBytes);
        }
        catch (CryptographicException)
        {
            return password;
        }
    }

    private static string UnprotectPassword(string password)
    {
        if (!password.StartsWith(EncryptedPasswordPrefix, StringComparison.Ordinal))
        {
            return password;
        }

        try
        {
            var encryptedBytes = Convert.FromBase64String(password[EncryptedPasswordPrefix.Length..]);
            var plainBytes = ProtectedData.Unprotect(encryptedBytes, optionalEntropy: null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(plainBytes);
        }
        catch (Exception)
        {
            return password;
        }
    }

    private T? DeserializeFile<T>(string path)
    {
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<T>(json, _jsonOptions);
    }

    private void WriteJsonAtomic<T>(string path, T payload)
    {
        var tempPath = $"{path}.tmp";
        var json = JsonSerializer.Serialize(payload, _jsonOptions);
        File.WriteAllText(tempPath, json);
        File.Move(tempPath, path, overwrite: true);
    }

    private static string SortableIp(string ipAddress)
    {
        if (!IPAddress.TryParse(ipAddress, out var parsed))
        {
            return ipAddress;
        }

        return string.Join('.', parsed.GetAddressBytes().Select(b => b.ToString("D3")));
    }
}
