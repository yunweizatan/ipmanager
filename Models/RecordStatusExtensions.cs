namespace IPManage.Models;

public static class RecordStatusExtensions
{
    public static string ToChineseText(this RecordStatus status)
    {
        return status switch
        {
            RecordStatus.InUse => "使用中",
            RecordStatus.Idle => "空闲",
            RecordStatus.Reserved => "预留",
            RecordStatus.Disabled => "停用",
            _ => "未知"
        };
    }

    public static RecordStatus FromChineseText(string text)
    {
        return text switch
        {
            "使用中" => RecordStatus.InUse,
            "空闲" => RecordStatus.Idle,
            "预留" => RecordStatus.Reserved,
            "停用" => RecordStatus.Disabled,
            _ => RecordStatus.InUse
        };
    }
}
