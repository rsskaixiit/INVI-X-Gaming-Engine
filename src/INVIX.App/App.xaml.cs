using System.Windows;
#if INVIX_HARDENED
using Invix.Services;
#endif

namespace Invix;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
#if INVIX_HARDENED
        if (IntegrityGuard.IsCompromised() || !IntegrityGuard.VerifySelf())
        {
            Environment.Exit(1);
            return;
        }
#endif

        base.OnStartup(e);
    }
}
