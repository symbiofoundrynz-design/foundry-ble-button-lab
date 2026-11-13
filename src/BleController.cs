using System.Collections.ObjectModel;
using System.Windows;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;
using Windows.Storage.Streams;

namespace FoundryBLEButtonLab;

public sealed class BleController {
    readonly BluetoothLEAdvertisementWatcher watcher=new(){ScanningMode=BluetoothLEScanningMode.Active};
    readonly Dictionary<ulong,DeviceRow> map=[];
    readonly Dictionary<ulong,BluetoothLEDevice> connections=[];
    readonly Dictionary<ulong,List<GattCharacteristic>> notifyChars=[];
    readonly HashSet<ulong> discoveryBaseline=[];
    readonly HashSet<ulong> discoveryAttempted=[];
    readonly Dictionary<ulong,int> discoveryHits=[]; readonly Dictionary<ulong,BluetoothAddressType> candidateAddressTypes=[];
    bool discoveryArmed,discoveryBusy;
    DateTime discoveryDeadline;

    public ObservableCollection<DeviceRow> Devices{get;}=[];
    public ObservableCollection<GattRow> Gatt{get;}=[];
    public event Action<string>? Log; public event Action<string>? DiscoveryLog; public event Action<string>? DiscoveryStatus;

    public BleController(){
        watcher.Received+=OnReceived;
        watcher.Stopped+=(s,e)=>Emit($"SCAN_STOP {e.Error}");
    }

    public void Start(){watcher.Start();Emit("SCAN_START");}
    public void Stop(){watcher.Stop();}

    public async Task ArmDiscoveryAsync(){
        discoveryArmed=false; discoveryBusy=false;
        discoveryBaseline.Clear(); discoveryAttempted.Clear(); discoveryHits.Clear(); candidateAddressTypes.Clear();
        if(watcher.Status!=BluetoothLEAdvertisementWatcherStatus.Started) Start();
        SetDiscoveryStatus("BASELINING - BLACK POWER OFF"); Emit("DISCOVERY_BASELINE_BEGIN 3s");
        await Task.Delay(3000);
        foreach(var a in map.Keys) discoveryBaseline.Add(a);
        discoveryDeadline=DateTime.Now.AddSeconds(30);
        discoveryArmed=true;
        SetDiscoveryStatus("READY - INSERT BATTERY / PRESS BLACK"); Emit($"DISCOVERY_READY BASELINE={discoveryBaseline.Count} WINDOW=30s PRESS_BUTTON_NOW");
    }

    void OnReceived(BluetoothLEAdvertisementWatcher s,BluetoothLEAdvertisementReceivedEventArgs e)=>
        Application.Current.Dispatcher.Invoke(()=>{candidateAddressTypes[e.BluetoothAddress]=e.BluetoothAddressType;
            if(!map.TryGetValue(e.BluetoothAddress,out var d)){
                d=new(){NumericAddress=e.BluetoothAddress,Address=Fmt(e.BluetoothAddress),Name=e.Advertisement.LocalName??"",Classification=Classify(e),State="Seen"};
                map[e.BluetoothAddress]=d; Devices.Add(d);
                Emit($"DISCOVER {d.Address} ADDRTYPE={e.BluetoothAddressType} RSSI={e.RawSignalStrengthInDBm} NAME={d.Name}");
            }
            var prior=d.Rssi;
            d.AdvCount++; d.Rssi=e.RawSignalStrengthInDBm; d.LastSeen=DateTime.Now;
            if(string.IsNullOrWhiteSpace(d.Name)&&!string.IsNullOrWhiteSpace(e.Advertisement.LocalName)) d.Name=e.Advertisement.LocalName;
            if(d.AdvCount<=3||prior==-127||Math.Abs(d.Rssi-prior)>=5||d.AdvCount%10==0)
                Emit($"ADV {d.Address} COUNT={d.AdvCount} RSSI={d.Rssi} DELTA={(prior==-127?0:d.Rssi-prior)} NAME={d.Name}");

            if(discoveryArmed){
                if(DateTime.Now>discoveryDeadline){discoveryArmed=false;SetDiscoveryStatus("TIMEOUT - NO BUTTON LOCKED");Emit("DISCOVERY_TIMEOUT");return;}
                if(discoveryBaseline.Contains(e.BluetoothAddress)||KnownNoise(d)||d.Classification!="Unknown"||e.RawSignalStrengthInDBm < -78) return;
                discoveryHits[e.BluetoothAddress]=discoveryHits.GetValueOrDefault(e.BluetoothAddress)+1;
                if(discoveryHits[e.BluetoothAddress]>=2&&!discoveryBusy&&!discoveryAttempted.Contains(e.BluetoothAddress)){
                    discoveryAttempted.Add(e.BluetoothAddress); discoveryBusy=true;
                    SetDiscoveryStatus($"PROBING {d.Address}"); Emit($"DISCOVERY_CANDIDATE {d.Address} ADDRTYPE={e.BluetoothAddressType} RSSI={d.Rssi} HITS={discoveryHits[e.BluetoothAddress]}"); Emit($"DISCOVERY_ADV_DETAIL {d.Address} {DescribeAdvertisement(e)}");
                    _=ProbeDiscoveryCandidateAsync(d);
                }
            }
        });

    static bool KnownNoise(DeviceRow d) => false; // Public snapshot: no environment-specific device exclusion list.
    static string DescribeAdvertisement(BluetoothLEAdvertisementReceivedEventArgs e){
        var services=e.Advertisement.ServiceUuids.Count==0
            ? "-"
            : string.Join(",",e.Advertisement.ServiceUuids.Select(v=>v.ToString()));

        var manufacturers=e.Advertisement.ManufacturerData.Count==0
            ? "-"
            : string.Join(";",e.Advertisement.ManufacturerData.Select(v=>$"{v.CompanyId:X4}:{AdvertisementHex(v.Data)}"));

        var sections=e.Advertisement.DataSections.Count==0
            ? "-"
            : string.Join(";",e.Advertisement.DataSections.Select(v=>$"{v.DataType:X2}:{AdvertisementHex(v.Data)}"));

        return $"ADDRTYPE={e.BluetoothAddressType} ADVTYPE={e.AdvertisementType} NAME=[{e.Advertisement.LocalName}] SERVICES=[{services}] MFG=[{manufacturers}] DATA=[{sections}]";
    }

    static string AdvertisementHex(IBuffer buffer){
        var reader=DataReader.FromBuffer(buffer);
        var bytes=new byte[reader.UnconsumedBufferLength];
        reader.ReadBytes(bytes);
        return Convert.ToHexString(bytes);
    }
    static string Classify(BluetoothLEAdvertisementReceivedEventArgs e){
        if(e.Advertisement.ServiceUuids.Any(x=>x.ToString().Contains("fe4a",StringComparison.OrdinalIgnoreCase))) return "Omron/HEM";
        if(e.Advertisement.ManufacturerData.Any(x=>x.CompanyId==0x0075)) return "Samsung";
        return "Unknown";
    }

    async Task ProbeDiscoveryCandidateAsync(DeviceRow d){
        try{
            Emit($"DISCOVERY_PROBE_BEGIN {d.Address}");
            BluetoothLEDevice? ble;
            if(candidateAddressTypes.TryGetValue(d.NumericAddress,out var candidateType)){
                Emit($"DISCOVERY_RESOLVE_TYPE {d.Address} {candidateType}");
                ble=await BluetoothLEDevice.FromBluetoothAddressAsync(d.NumericAddress,candidateType);
            }else{
                Emit($"DISCOVERY_RESOLVE_TYPE {d.Address} FALLBACK");
                ble=await BluetoothLEDevice.FromBluetoothAddressAsync(d.NumericAddress);
            }
            if(ble is null){
                Emit($"DISCOVERY_RESOLVE_NULL {d.Address}");
                SetDiscoveryStatus($"REJECTED {d.Address} - RESOLVE NULL");
                discoveryBusy=false;
                return;
            }
            d.Name=string.IsNullOrWhiteSpace(ble.Name)?d.Name:ble.Name;
            Emit($"DISCOVERY_RESOLVED {d.Address} NAME={ble.Name} STATUS={ble.ConnectionStatus}");
            var access=DeviceAccessInformation.CreateFromId(ble.DeviceId);
            Emit($"DISCOVERY_ACCESS {access.CurrentStatus}");
            var pendingGatt=ble.GetGattServicesAsync(BluetoothCacheMode.Uncached).AsTask();
            if(await Task.WhenAny(pendingGatt,Task.Delay(TimeSpan.FromSeconds(6)))!=pendingGatt){
                Emit($"DISCOVERY_GATT_TIMEOUT {d.Address} LIMIT=6s");
                SetDiscoveryStatus($"REJECTED {d.Address} - GATT TIMEOUT");
                ble.Dispose();
                discoveryBusy=false;
                return;
            }
            var sr=await pendingGatt;
            Emit($"DISCOVERY_GATT {d.Address} STATUS={sr.Status} COUNT={sr.Services.Count}");
            if(sr.Status!=GattCommunicationStatus.Success){ble.Dispose();discoveryBusy=false;return;}

            bool knownButtonProfile=false;
            var discoveredNotify=new List<GattCharacteristic>();
            foreach(var svc in sr.Services){
                var sid=svc.Uuid.ToString();
                Emit($"DISCOVERY_SERVICE {sid}");
                if(sid.Contains("ffe0",StringComparison.OrdinalIgnoreCase)||sid.StartsWith("00001802",StringComparison.OrdinalIgnoreCase))
                    knownButtonProfile=true;
                var cr=await svc.GetCharacteristicsAsync(BluetoothCacheMode.Uncached);
                foreach(var c in cr.Characteristics){
                    var cid=c.Uuid.ToString();
                    Emit($"DISCOVERY_CHAR SERVICE={sid} CHAR={cid} PROPS={c.CharacteristicProperties}");
                    if(cid.Contains("ffe1",StringComparison.OrdinalIgnoreCase)) knownButtonProfile=true;
                    if(c.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Notify)||c.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Indicate))
                        discoveredNotify.Add(c);
                }
            }

            if(knownButtonProfile){
                if(connections.Remove(d.NumericAddress,out var old)) old.Dispose();
                connections[d.NumericAddress]=ble;
                notifyChars[d.NumericAddress]=discoveredNotify;
                d.Classification="Candidate Button"; d.State="Button Ready";
                Emit($"DISCOVERY_BUTTON_PROFILE {d.Address} NOTIFY_COUNT={discoveredNotify.Count}");
                foreach(var c in discoveredNotify){
                    try{
                        c.ValueChanged-=OnValue;c.ValueChanged+=OnValue;
                        var mode=c.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Notify)?
                            GattClientCharacteristicConfigurationDescriptorValue.Notify:
                            GattClientCharacteristicConfigurationDescriptorValue.Indicate;
                        var st=await c.WriteClientCharacteristicConfigurationDescriptorAsync(mode);
                        Emit($"DISCOVERY_SUBSCRIBE {c.Uuid} {st}");
                    }catch(Exception ex){Emit($"DISCOVERY_SUBSCRIBE_ERROR {c.Uuid} {ex.Message}");}
                }
                discoveryArmed=false;
                SetDiscoveryStatus($"LOCKED {d.Address} - PRESS BUTTON TO TEST"); Emit($"DISCOVERY_LOCKED {d.Address} PRESS_BUTTON_TO_TEST");
            }else{
                Emit($"DISCOVERY_NOT_IT {d.Address} NO_FFE0_FFE1_1802");
                ble.Dispose(); discoveryBusy=false;
            }
        }catch(Exception ex){
            Emit($"DISCOVERY_ERROR {d.Address} {ex.Message}");
            discoveryBusy=false;
        }
    }

    public async Task PairAsync(DeviceRow d){
        try{
            var ble=await BluetoothLEDevice.FromBluetoothAddressAsync(d.NumericAddress);
            if(ble is null){Emit("PAIR resolve failed");return;}
            var p=ble.DeviceInformation.Pairing;
            if(p.IsPaired){d.State="Paired";Emit($"PAIR already {d.Alias}");return;}
            if(!p.CanPair){Emit($"PAIR unavailable {d.Address}");return;}
            var r=await p.PairAsync(DevicePairingProtectionLevel.Encryption);
            d.State=r.Status.ToString(); Emit($"PAIR {d.Address} {r.Status}"); ble.Dispose();
        }catch(Exception ex){Emit($"PAIR_ERROR {ex.Message}");}
    }

    public async Task ConnectAsync(DeviceRow d){
        try{
            if(connections.Remove(d.NumericAddress,out var old)) old.Dispose();
            var ble=await BluetoothLEDevice.FromBluetoothAddressAsync(d.NumericAddress);
            if(ble is null){Emit("CONNECT resolve failed");return;}
            connections[d.NumericAddress]=ble;
            d.Name=string.IsNullOrWhiteSpace(ble.Name)?d.Name:ble.Name;
            Emit($"DEVICE_RESOLVED {d.Address} NAME={ble.Name}");
            Emit($"CONNECTION_STATUS {ble.ConnectionStatus}");
            var access=DeviceAccessInformation.CreateFromId(ble.DeviceId);
            Emit($"DEVICE_ACCESS {access.CurrentStatus}");
            d.State=ble.ConnectionStatus==BluetoothConnectionStatus.Connected?"Connected":"Resolved";
            Gatt.Clear(); notifyChars[d.NumericAddress]=[];
            Emit("GATT_QUERY_BEGIN CACHE=Uncached");
            var sr=await ble.GetGattServicesAsync(BluetoothCacheMode.Uncached);
            Emit($"GATT_QUERY_STATUS {sr.Status}"); Emit($"GATT_SERVICE_COUNT {sr.Services.Count}");
            if(sr.Status!=GattCommunicationStatus.Success)d.State=$"GATT {sr.Status}";
            else if(ble.ConnectionStatus==BluetoothConnectionStatus.Connected)d.State="Connected";
            foreach(var svc in sr.Services){
                var cr=await svc.GetCharacteristicsAsync(BluetoothCacheMode.Uncached);
                foreach(var c in cr.Characteristics){
                    Gatt.Add(new(svc.Uuid.ToString(),c.Uuid.ToString(),c.CharacteristicProperties.ToString()));
                    if(c.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Notify)||c.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Indicate))
                        notifyChars[d.NumericAddress].Add(c);
                }
            }
        }catch(Exception ex){d.State="Error";Emit($"CONNECT_ERROR {ex.Message}");}
    }

    public async Task SubscribeAllAsync(DeviceRow d){
        if(!notifyChars.TryGetValue(d.NumericAddress,out var list)){Emit("SUBSCRIBE no connected GATT");return;}
        foreach(var c in list){
            try{
                c.ValueChanged-=OnValue;c.ValueChanged+=OnValue;
                var mode=c.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Notify)?
                    GattClientCharacteristicConfigurationDescriptorValue.Notify:
                    GattClientCharacteristicConfigurationDescriptorValue.Indicate;
                var st=await c.WriteClientCharacteristicConfigurationDescriptorAsync(mode);
                Emit($"SUBSCRIBE {c.Uuid} {st}");
            }catch(Exception ex){Emit($"SUBSCRIBE_ERROR {c.Uuid} {ex.Message}");}
        }
    }

    void OnValue(GattCharacteristic c,GattValueChangedEventArgs e){
        var r=DataReader.FromBuffer(e.CharacteristicValue);var b=new byte[r.UnconsumedBufferLength];r.ReadBytes(b);
        Emit($"NOTIFY {c.Uuid} HEX={Convert.ToHexString(b)}");
    }

    public void Assign(DeviceRow d,string alias){d.Alias=alias;d.Classification="Confirmed Button";Emit($"ALIAS {d.Address} => {alias}");}
    public void Ignore(DeviceRow d){d.Classification="Ignored";Emit($"IGNORE {d.Address}");}
    void Emit(string x){
        var line=$"{DateTime.Now:HH:mm:ss.fff}  {x}";
        Log?.Invoke(line);
        if(x.StartsWith("DISCOVERY_",StringComparison.OrdinalIgnoreCase) ||
           x.StartsWith("GATT_",StringComparison.OrdinalIgnoreCase) ||
           x.StartsWith("SUBSCRIBE",StringComparison.OrdinalIgnoreCase) ||
           x.StartsWith("NOTIFY",StringComparison.OrdinalIgnoreCase))
            DiscoveryLog?.Invoke(line);
    }
    void SetDiscoveryStatus(string x)=>DiscoveryStatus?.Invoke(x);
    static string Fmt(ulong a)=>string.Join(":",Enumerable.Range(0,6).Reverse().Select(i=>((a>>(i*8))&255).ToString("X2")));
}

