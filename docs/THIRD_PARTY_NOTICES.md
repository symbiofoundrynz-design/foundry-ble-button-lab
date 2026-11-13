# Dependencies and third-party notices

The C# project uses the Microsoft .NET SDK, Windows Presentation Foundation (WPF), and Windows Runtime APIs for Bluetooth Low Energy (`Windows.Devices.Bluetooth`, `Windows.Devices.Enumeration`, `Windows.Storage.Streams`).

- **.NET 10 and WPF:** Microsoft platform components, not included as redistributable binaries in this source snapshot.
- **Windows SDK / WinRT:** Windows platform interfaces. The archived private executable's dependency packet contained `Microsoft.Windows.SDK.NET.dll` and `WinRT.Runtime.dll`; neither is included in this GitHub candidate.
- **SXK/iSearching devices and software:** third-party test subjects only. This project is not endorsed by their vendors and does not bundle their applications, firmware or software.

No additional NuGet `PackageReference` entries were present in the preserved project file. This is a preliminary source-level dependency inventory, not a full transitive license audit. Any future binary distribution requires verifying the applicable redistributable terms and notices independently.
