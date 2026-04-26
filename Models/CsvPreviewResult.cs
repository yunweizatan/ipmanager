namespace IPManage.Models;

public sealed class CsvPreviewResult
{
    public string SourceEncoding { get; set; } = "未知";

    public int TotalRowCount { get; set; }

    public int PreviewRowCount { get; set; }

    public List<string> PreviewLines { get; set; } = [];

    public List<string> Warnings { get; set; } = [];
}
