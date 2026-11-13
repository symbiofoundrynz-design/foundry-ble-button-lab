using System.Windows; using System.Windows.Controls;
namespace FoundryBLEButtonLab;
public partial class MainWindow:Window { readonly BleController ble=new(); public MainWindow(){
    InitializeComponent();
    DataContext=ble;
    ble.Log+=x=>Dispatcher.Invoke(()=>{
        LogBox.AppendText(x+Environment.NewLine);
        LogBox.ScrollToEnd();
    });
    ble.DiscoveryLog+=x=>Dispatcher.Invoke(()=>{
        DiscoveryLogBox.AppendText(x+Environment.NewLine);
        DiscoveryLogBox.ScrollToEnd();
    });
    ble.DiscoveryStatus+=x=>Dispatcher.Invoke(()=>{
        DiscoveryStatusText.Text=x;
    });
}
 DeviceRow? Sel()=>DevicesGrid.SelectedItem as DeviceRow; void Scan_Click(object s,RoutedEventArgs e)=>ble.Start();void Stop_Click(object s,RoutedEventArgs e)=>ble.Stop();async void Discovery_Click(object s,RoutedEventArgs e)=>await ble.ArmDiscoveryAsync();async void Pair_Click(object s,RoutedEventArgs e){if(Sel() is{}d)await ble.PairAsync(d);}async void Connect_Click(object s,RoutedEventArgs e){if(Sel() is{}d)await ble.ConnectAsync(d);}async void Subscribe_Click(object s,RoutedEventArgs e){if(Sel() is{}d)await ble.SubscribeAllAsync(d);}void Assign_Click(object s,RoutedEventArgs e){if(Sel() is{}d&&AliasBox.SelectedItem is ComboBoxItem i)ble.Assign(d,i.Content?.ToString()??"BUTTON");}void Ignore_Click(object s,RoutedEventArgs e){if(Sel() is{}d)ble.Ignore(d);}}

