namespace IPManage.Models;

public sealed class CsvImportResult
{
    public string SourceEncoding { get; set; } = "未知";

    public string FailureReportPath { get; set; } = string.Empty;

    public int AddedCount { get; set; }

    public int UpdatedCount { get; set; }

    public int SkippedCount { get; set; }

    public List<string> Errors { get; set; } = [];

    public string Summary => $"编码 {SourceEncoding}，新增 {AddedCount}，更新 {UpdatedCount}，跳过 {SkippedCount}";
}
