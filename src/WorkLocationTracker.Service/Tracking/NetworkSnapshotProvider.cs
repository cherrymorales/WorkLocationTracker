using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace WorkLocationTracker.Service.Tracking;

public sealed class NetworkSnapshotProvider : INetworkSnapshotProvider
{
    private static readonly Guid NetworkListManagerClassId =
        new("DCB00C01-570F-4A9B-8D69-199FDBA5723B");

    public NetworkSnapshot GetSnapshot()
    {
        var profileSnapshot = TryGetConnectedProfiles();
        if (profileSnapshot is not null)
        {
            return profileSnapshot;
        }

        return GetAdapterSnapshot();
    }

    private static NetworkSnapshot? TryGetConnectedProfiles()
    {
        object? managerObject = null;
        IEnumNetworks? networks = null;

        try
        {
            var managerType = Type.GetTypeFromCLSID(NetworkListManagerClassId, throwOnError: false);
            if (managerType is null)
            {
                return null;
            }

            managerObject = Activator.CreateInstance(managerType);
            if (managerObject is not INetworkListManager manager)
            {
                return null;
            }

            networks = manager.GetNetworks(NetworkEnumeration.Connected);
            var profiles = new List<(string Id, string Name)>();

            while (true)
            {
                uint fetched = 0;
                var result = networks.Next(1, out var network, ref fetched);
                if (result != 0 || fetched == 0)
                {
                    break;
                }

                try
                {
                    var id = network.GetNetworkId().ToString("D");
                    var name = network.GetName().Trim();
                    profiles.Add((id, string.IsNullOrWhiteSpace(name) ? "Connected network" : name));
                }
                finally
                {
                    if (Marshal.IsComObject(network))
                    {
                        Marshal.FinalReleaseComObject(network);
                    }
                }
            }

            if (profiles.Count == 0)
            {
                return null;
            }

            var ordered = profiles.OrderBy(profile => profile.Id, StringComparer.Ordinal).ToArray();
            var identity = string.Join('|', ordered.Select(profile => profile.Id));
            var displayName = string.Join(" + ", ordered.Select(profile => profile.Name).Distinct(StringComparer.OrdinalIgnoreCase));
            return new NetworkSnapshot(Hash(identity), displayName);
        }
        catch (COMException)
        {
            return null;
        }
        catch (InvalidCastException)
        {
            return null;
        }
        catch (ArgumentException)
        {
            return null;
        }
        finally
        {
            if (networks is not null && Marshal.IsComObject(networks))
            {
                Marshal.FinalReleaseComObject(networks);
            }

            if (managerObject is not null && Marshal.IsComObject(managerObject))
            {
                Marshal.FinalReleaseComObject(managerObject);
            }
        }
    }

    private static NetworkSnapshot GetAdapterSnapshot()
    {
        var adapters = NetworkInterface.GetAllNetworkInterfaces()
            .Where(adapter =>
                adapter.OperationalStatus == OperationalStatus.Up &&
                adapter.NetworkInterfaceType is not NetworkInterfaceType.Loopback and not NetworkInterfaceType.Tunnel)
            .Select(adapter => new
            {
                Adapter = adapter,
                Gateways = adapter.GetIPProperties().GatewayAddresses
                    .Select(address => address.Address.ToString())
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray()
            })
            .Where(item => item.Gateways.Length > 0)
            .OrderBy(item => item.Adapter.Id, StringComparer.Ordinal)
            .ToArray();

        if (adapters.Length == 0)
        {
            return new NetworkSnapshot(Hash("disconnected"), "No connected network");
        }

        var identity = string.Join('|', adapters.Select(item =>
            $"{item.Adapter.Id}:{item.Adapter.GetPhysicalAddress()}:{string.Join(',', item.Gateways)}"));
        var display = string.Join(" + ", adapters.Select(item =>
            $"{item.Adapter.Name} ({string.Join(", ", item.Gateways)})"));

        return new NetworkSnapshot(Hash(identity), display);
    }

    private static string Hash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes);
    }

    private enum NetworkEnumeration
    {
        Connected = 0x1
    }

    [ComImport]
    [Guid("DCB00000-570F-4A9B-8D69-199FDBA5723B")]
    [InterfaceType(ComInterfaceType.InterfaceIsDual)]
    private interface INetworkListManager
    {
        [return: MarshalAs(UnmanagedType.Interface)]
        IEnumNetworks GetNetworks(NetworkEnumeration flags);
    }

    [ComImport]
    [Guid("DCB00003-570F-4A9B-8D69-199FDBA5723B")]
    [InterfaceType(ComInterfaceType.InterfaceIsDual)]
    private interface IEnumNetworks
    {
        [return: MarshalAs(UnmanagedType.IUnknown)]
        object GetEnumerator();

        [PreserveSig]
        int Next(
            uint count,
            [MarshalAs(UnmanagedType.Interface)] out INetwork network,
            ref uint fetched);
    }

    [ComImport]
    [Guid("DCB00002-570F-4A9B-8D69-199FDBA5723B")]
    [InterfaceType(ComInterfaceType.InterfaceIsDual)]
    private interface INetwork
    {
        [return: MarshalAs(UnmanagedType.BStr)]
        string GetName();

        void SetName([MarshalAs(UnmanagedType.BStr)] string name);

        [return: MarshalAs(UnmanagedType.BStr)]
        string GetDescription();

        void SetDescription([MarshalAs(UnmanagedType.BStr)] string description);

        Guid GetNetworkId();
    }
}
