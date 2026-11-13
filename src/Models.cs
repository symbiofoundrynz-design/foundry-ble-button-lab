using System.ComponentModel;
using System.Runtime.CompilerServices;
namespace FoundryBLEButtonLab;
public sealed class DeviceRow : INotifyPropertyChanged {
 string alias="â€”", name="", classification="Unknown", state="Seen", address=""; int rssi=-127, presses, advCount; DateTime lastSeen=DateTime.Now;
 public ulong NumericAddress {get;set;} public string Alias {get=>alias;set=>Set(ref alias,value);} public string Name{get=>name;set=>Set(ref name,value);} public string Classification{get=>classification;set=>Set(ref classification,value);} public string State{get=>state;set=>Set(ref state,value);} public string Address{get=>address;set=>Set(ref address,value);} public int Rssi{get=>rssi;set=>Set(ref rssi,value);} public int Presses{get=>presses;set=>Set(ref presses,value);} public int AdvCount{get=>advCount;set=>Set(ref advCount,value);} public DateTime LastSeen{get=>lastSeen;set=>Set(ref lastSeen,value);} public string LastSeenText=>LastSeen.ToString("HH:mm:ss");
 public event PropertyChangedEventHandler? PropertyChanged; void Set<T>(ref T f,T v,[CallerMemberName]string? n=null){if(EqualityComparer<T>.Default.Equals(f,v))return;f=v;PropertyChanged?.Invoke(this,new(n)); if(n==nameof(LastSeen))PropertyChanged?.Invoke(this,new(nameof(LastSeenText)));}
}
public sealed record GattRow(string Service,string Characteristic,string Properties);

