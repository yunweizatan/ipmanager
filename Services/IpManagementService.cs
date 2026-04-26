using System.IO;
using System.Text;
using IPManage.Models;
using IPManage.Repositories;
using IPManage.Validators;

namespace IPManage.Services;

public sealed class IpManagementService
{
    private static readonly Encoding Utf8WithoutBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
    private static readonly Encoding Utf8WithBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true, throwOnInvalidBytes: true);
    private static readonly Encoding Utf8WithBomForExport = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);

    private readonly IIpRepository _repository;
    private readonly IpRecordValidator _validator;

    public IpManagementService()
        : this(new JsonIpRepository(), new IpRecordValidator())
    {
    }

    public IpManagementService(IIpRepository repository, IpRecordValidator validator)
    {
        _repository = repository;
        _validator = validator;
    }

    public string DataRoot => _repository.DataRoot;

    public IReadOnlyList<IpRecord> LoadRecords() => _repository.LoadAll();

    public IReadOnlyList<string> Validate(IpRecord record, IEnumerable<IpRecord> allRecords) =>
        _validator.Validate(record, allRecords).ToList();

    public void SaveAll(IReadOnlyCollection<IpRecord> records) => _repository.SaveAll(records);

    public string CreateBackup() => _repository.CreateBackup();

    public CsvPreviewResult PreviewCsv(string csvPath, int previewCount = 5)
    {
        var warnings = new List<string>();
        var document = LoadCsvDocument(csvPath, warnings);
        var preview = new CsvPreviewResult
        {
            SourceEncoding = document.SourceEncoding,
            TotalRowCount = Math.Max(document.Rows.Count - document.FirstDataRowIndex, 0),
            PreviewRowCount = Math.Min(previewCount, Math.Max(document.Rows.Count - document.FirstDataRowIndex, 0)),
            Warnings = warnings
        };

        for (var i = document.FirstDataRowIndex; i < document.Rows.Count && preview.PreviewLines.Count < previewCount; i++)
        {
            var row = document.Rows[i];
            if (row.Columns.All(string.IsNullOrWhiteSpace))
            {
                continue;
            }

            var ip = GetColumn(row.Columns, document.HeaderMap, "IP地址", 0);
            var deviceName = GetColumn(row.Columns, document.HeaderMap, "设备名称", 1);
            var owner = GetColumn(row.Columns, document.HeaderMap, "使用人", 3);
            var vlan = GetColumn(row.Columns, document.HeaderMap, "VLAN", 4);
            var password = GetColumn(row.Columns, document.HeaderMap, "密码");
            var passwordSummary = string.IsNullOrWhiteSpace(password) ? "未填写" : "已填写";
            preview.PreviewLines.Add($"第 {row.LineNumber} 行: IP={ip}，设备={SafeDisplay(deviceName)}，使用人={SafeDisplay(owner)}，VLAN={SafeDisplay(vlan)}，密码={passwordSummary}");
        }

        return preview;
    }

    public CsvImportResult ImportCsv(string csvPath, IList<IpRecord> existingRecords)
    {
        var result = new CsvImportResult();
        var document = LoadCsvDocument(csvPath, result.Errors);
        result.SourceEncoding = document.SourceEncoding;

        if (document.Rows.Count <= document.FirstDataRowIndex)
        {
            result.Errors.Add("CSV 文件没有可导入的数据。");
            return result;
        }

        for (var i = document.FirstDataRowIndex; i < document.Rows.Count; i++)
        {
            var row = document.Rows[i];
            var columns = row.Columns;
            if (columns.All(string.IsNullOrWhiteSpace))
            {
                continue;
            }

            if (columns.Count < 6)
            {
                result.SkippedCount++;
                result.Errors.Add($"第 {row.LineNumber} 行字段不足，至少需要 6 列。");
                continue;
            }

            var candidate = new IpRecord
            {
                IpAddress = GetColumn(columns, document.HeaderMap, "IP地址", 0),
                DeviceName = GetColumn(columns, document.HeaderMap, "设备名称", 1),
                AssetNumber = GetColumn(columns, document.HeaderMap, "资产编号", 2),
                Owner = GetColumn(columns, document.HeaderMap, "使用人", 3),
                Password = GetColumn(columns, document.HeaderMap, "密码"),
                Vlan = GetColumn(columns, document.HeaderMap, "VLAN", 4),
                ApplyTime = ParseDateTime(GetColumn(columns, document.HeaderMap, "申请时间", 5)),
                DeviceType = GetColumn(columns, document.HeaderMap, "设备类型", 6),
                Status = RecordStatusExtensions.FromChineseText(GetColumn(columns, document.HeaderMap, "状态", 7)),
                Remark = GetColumn(columns, document.HeaderMap, "备注", 8),
                Subnet = GetColumn(columns, document.HeaderMap, "网段", 9)
            };

            if (string.IsNullOrWhiteSpace(candidate.DeviceType))
            {
                candidate.DeviceType = "PC";
            }

            if (candidate.ApplyTime == default)
            {
                candidate.ApplyTime = DateTime.Now;
            }

            var existing = existingRecords.FirstOrDefault(record =>
                string.Equals(record.IpAddress, candidate.IpAddress, StringComparison.OrdinalIgnoreCase));

            if (existing is not null)
            {
                candidate.Id = existing.Id;
                candidate.CreatedAt = existing.CreatedAt;
            }

            var validationSource = existingRecords.Where(record =>
                existing is null || !string.Equals(record.Id, existing.Id, StringComparison.OrdinalIgnoreCase));

            var errors = Validate(candidate, validationSource);
            if (errors.Count > 0)
            {
                result.SkippedCount++;
                result.Errors.Add($"第 {row.LineNumber} 行: {errors[0]}");
                continue;
            }

            if (existing is null)
            {
                existingRecords.Add(candidate);
                result.AddedCount++;
            }
            else
            {
                existing.IpAddress = candidate.IpAddress;
                existing.DeviceName = candidate.DeviceName;
                existing.AssetNumber = candidate.AssetNumber;
                existing.Owner = candidate.Owner;
                existing.Password = candidate.Password;
                existing.Vlan = candidate.Vlan;
                existing.Subnet = candidate.Subnet;
                existing.DeviceType = candidate.DeviceType;
                existing.Status = candidate.Status;
                existing.ApplyTime = candidate.ApplyTime;
                existing.Remark = candidate.Remark;
                existing.UpdatedAt = DateTime.Now;
                result.UpdatedCount++;
            }
        }

        if (result.Errors.Count > 0)
        {
            result.FailureReportPath = WriteImportFailureReport(csvPath, result);
        }

        return result;
    }

    public string ExportCsv(IEnumerable<IpRecord> records)
    {
        var repository = (JsonIpRepository)_repository;
        var exportDirectory = repository.GetExportsPath();
        var exportPath = Path.Combine(exportDirectory, $"ip_records_{DateTime.Now:yyyyMMdd_HHmmss}.csv");

        var sb = new StringBuilder();
        sb.AppendLine("IP地址,设备名称,资产编号,使用人,密码,VLAN,申请时间,设备类型,状态,备注,网段");

        foreach (var record in records.OrderBy(item => item.IpAddress, StringComparer.OrdinalIgnoreCase))
        {
            sb.AppendLine(string.Join(',',
                Escape(record.IpAddress),
                Escape(record.DeviceName),
                Escape(record.AssetNumber),
                Escape(record.Owner),
                Escape(record.Password),
                Escape(record.Vlan),
                Escape(record.ApplyTimeDisplay),
                Escape(record.DeviceType),
                Escape(record.StatusText),
                Escape(record.Remark),
                Escape(record.Subnet)));
        }

        File.WriteAllText(exportPath, sb.ToString(), Utf8WithBomForExport);
        return exportPath;
    }

    private static string ReadCsvContent(string csvPath, out string detectedEncoding)
    {
        var bytes = File.ReadAllBytes(csvPath);
        if (bytes.Length == 0)
        {
            detectedEncoding = "未知";
            return string.Empty;
        }

        if (TryDetectEncodingFromBom(bytes, out var bomEncoding, out detectedEncoding))
        {
            return NormalizeCsvText(bomEncoding.GetString(bytes));
        }

        if (TryDecode(bytes, Utf8WithoutBom, out var utf8Text))
        {
            detectedEncoding = "UTF-8";
            return NormalizeCsvText(utf8Text);
        }

        var gb18030 = Encoding.GetEncoding("GB18030");
        detectedEncoding = "GB18030/GBK";
        return NormalizeCsvText(gb18030.GetString(bytes));
    }

    private CsvDocument LoadCsvDocument(string csvPath, ICollection<string> warnings)
    {
        var csvContent = ReadCsvContent(csvPath, out var detectedEncoding);
        var rows = ParseCsvRows(csvContent, warnings);
        var headerMap = rows.Count > 0 ? BuildHeaderMap(rows[0].Columns) : [];
        var hasHeader = headerMap.ContainsKey(NormalizeHeader("IP地址")) ||
                        headerMap.ContainsKey(NormalizeHeader("设备名称")) ||
                        headerMap.ContainsKey(NormalizeHeader("VLAN"));
        return new CsvDocument(
            detectedEncoding,
            rows,
            headerMap,
            hasHeader ? 1 : 0);
    }

    private static bool TryDetectEncodingFromBom(byte[] bytes, out Encoding encoding, out string displayName)
    {
        if (bytes.Length >= 3 &&
            bytes[0] == 0xEF &&
            bytes[1] == 0xBB &&
            bytes[2] == 0xBF)
        {
            encoding = Utf8WithBom;
            displayName = "UTF-8 BOM";
            return true;
        }

        if (bytes.Length >= 4 &&
            bytes[0] == 0xFF &&
            bytes[1] == 0xFE &&
            bytes[2] == 0x00 &&
            bytes[3] == 0x00)
        {
            encoding = Encoding.UTF32;
            displayName = "UTF-32 LE";
            return true;
        }

        if (bytes.Length >= 4 &&
            bytes[0] == 0x00 &&
            bytes[1] == 0x00 &&
            bytes[2] == 0xFE &&
            bytes[3] == 0xFF)
        {
            encoding = new UTF32Encoding(bigEndian: true, byteOrderMark: true, throwOnInvalidCharacters: true);
            displayName = "UTF-32 BE";
            return true;
        }

        if (bytes.Length >= 2 &&
            bytes[0] == 0xFF &&
            bytes[1] == 0xFE)
        {
            encoding = Encoding.Unicode;
            displayName = "UTF-16 LE";
            return true;
        }

        if (bytes.Length >= 2 &&
            bytes[0] == 0xFE &&
            bytes[1] == 0xFF)
        {
            encoding = Encoding.BigEndianUnicode;
            displayName = "UTF-16 BE";
            return true;
        }

        encoding = Utf8WithoutBom;
        displayName = "UTF-8";
        return false;
    }

    private static bool TryDecode(byte[] bytes, Encoding encoding, out string text)
    {
        try
        {
            text = encoding.GetString(bytes);
            return true;
        }
        catch (DecoderFallbackException)
        {
            text = string.Empty;
            return false;
        }
    }

    private static string NormalizeCsvText(string text)
    {
        return text.TrimStart('\uFEFF')
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
    }

    private static List<CsvRow> ParseCsvRows(string content, ICollection<string> errors)
    {
        var rows = new List<CsvRow>();
        if (string.IsNullOrEmpty(content))
        {
            return rows;
        }

        var columns = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;
        var rowStartLine = 1;
        var currentLine = 1;

        for (var i = 0; i < content.Length; i++)
        {
            var ch = content[i];

            if (ch == '"')
            {
                if (inQuotes)
                {
                    if (i + 1 < content.Length && content[i + 1] == '"')
                    {
                        field.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else if (field.Length == 0)
                {
                    inQuotes = true;
                }
                else
                {
                    field.Append(ch);
                }

                continue;
            }

            if (ch == ',' && !inQuotes)
            {
                columns.Add(field.ToString());
                field.Clear();
                continue;
            }

            if (ch == '\n' && !inQuotes)
            {
                columns.Add(field.ToString());
                field.Clear();
                rows.Add(new CsvRow(rowStartLine, [.. columns]));
                columns.Clear();
                currentLine++;
                rowStartLine = currentLine;
                continue;
            }

            field.Append(ch);

            if (ch == '\n')
            {
                currentLine++;
            }
        }

        if (inQuotes)
        {
            errors.Add($"第 {rowStartLine} 行存在未闭合的引号。");
        }

        if (field.Length > 0 || columns.Count > 0)
        {
            columns.Add(field.ToString());
            rows.Add(new CsvRow(rowStartLine, [.. columns]));
        }

        return rows;
    }

    private static string Escape(string value)
    {
        if (value.Contains('"'))
        {
            value = value.Replace("\"", "\"\"");
        }

        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
        {
            value = $"\"{value}\"";
        }

        return value;
    }

    private static string GetColumn(IReadOnlyList<string> columns, IReadOnlyDictionary<string, int> headerMap, string headerName, int? fallbackIndex = null)
    {
        var normalizedHeader = NormalizeHeader(headerName);
        if (headerMap.TryGetValue(normalizedHeader, out var index))
        {
            return index < columns.Count ? columns[index].Trim() : string.Empty;
        }

        if (fallbackIndex.HasValue && fallbackIndex.Value < columns.Count)
        {
            return columns[fallbackIndex.Value].Trim();
        }

        return string.Empty;
    }

    private static DateTime ParseDateTime(string value)
    {
        return DateTime.TryParse(value, out var result) ? result : default;
    }

    private sealed record CsvRow(int LineNumber, List<string> Columns);
    private sealed record CsvDocument(string SourceEncoding, List<CsvRow> Rows, Dictionary<string, int> HeaderMap, int FirstDataRowIndex);

    private static Dictionary<string, int> BuildHeaderMap(IReadOnlyList<string> headers)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < headers.Count; i++)
        {
            var normalized = NormalizeHeader(headers[i]);
            if (!string.IsNullOrWhiteSpace(normalized) && !map.ContainsKey(normalized))
            {
                map[normalized] = i;
            }
        }

        return map;
    }

    private static string NormalizeHeader(string value)
    {
        return new string(value.Where(ch => !char.IsWhiteSpace(ch)).ToArray()).Trim().ToUpperInvariant();
    }

    private string WriteImportFailureReport(string csvPath, CsvImportResult result)
    {
        var repository = (JsonIpRepository)_repository;
        var exportDirectory = repository.GetExportsPath();
        var reportPath = Path.Combine(exportDirectory, $"import_failures_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
        var builder = new StringBuilder();
        builder.AppendLine($"源文件: {csvPath}");
        builder.AppendLine($"编码: {result.SourceEncoding}");
        builder.AppendLine($"结果: {result.Summary}");
        builder.AppendLine();
        builder.AppendLine("失败明细:");

        foreach (var error in result.Errors)
        {
            builder.AppendLine(error);
        }

        File.WriteAllText(reportPath, builder.ToString(), Utf8WithBomForExport);
        return reportPath;
    }

    private static string SafeDisplay(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "-" : value.Trim();
    }
}
