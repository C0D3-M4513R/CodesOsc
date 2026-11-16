using System.Windows;

namespace CodeOSC.Modules.OpenShock.UI;

public partial class ShockerCacheView
{
    private OpenShockModule module;

    public ShockerCacheView(OpenShockModule module, ShockerCacheSetting moduleSetting)
    {
        this.module = module;
        InitializeComponent();

        DataContext = moduleSetting;
    }

    public async void FetchShockers_OnClick(object sender, RoutedEventArgs e)
    {
        await module.FetchShockers();
    }
}

