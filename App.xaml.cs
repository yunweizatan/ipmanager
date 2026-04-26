using System.Text;
using System.Windows;

namespace IPManage;

public partial class App : Application
{
    static App()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }
}
