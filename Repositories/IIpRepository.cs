using IPManage.Models;

namespace IPManage.Repositories;

public interface IIpRepository
{
    string DataRoot { get; }

    IReadOnlyList<IpRecord> LoadAll();

    void SaveAll(IReadOnlyCollection<IpRecord> records);

    string CreateBackup();
}
