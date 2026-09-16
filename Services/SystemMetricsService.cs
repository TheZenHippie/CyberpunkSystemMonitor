using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using CyberpunkSystemMonitor.Models;

namespace CyberpunkSystemMonitor.Services
{
    public class SystemMetricsService : IDisposable
    {
        #region Native Memory API

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private class MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;

            public MEMORYSTATUSEX()
            {
                dwLength = (uint)Marshal.SizeOf(this);
            }
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX lpBuffer);

        #endregion

        private readonly PerformanceCounter? _cpuTotalCounter;
        private readonly List<PerformanceCounter> _coreCounters = new List<PerformanceCounter>();
        private readonly Dictionary<string, (PerformanceCounter readCounter, PerformanceCounter writeCounter)> _diskCounters = new Dictionary<string, (PerformanceCounter, PerformanceCounter)>();
        private readonly List<PerformanceCounter> _gpu3dCounters = new List<PerformanceCounter>();
        private readonly List<PerformanceCounter> _gpuMemoryCounters = new List<PerformanceCounter>();

        private string _cachedCpuName = "Generic CPU";
        private int _physicalCores = 1;
        private string _cachedGpuName = "Generic GPU";
        private ulong _cachedGpuVramBytes = 0;

        private long _lastNetBytesRecv = 0;
        private long _lastNetBytesSent = 0;
        private DateTime _lastNetSampleTime = DateTime.UtcNow;

        private readonly Dictionary<int, (TimeSpan totalCpuTime, DateTime sampleTime)> _processCpuHistory = new Dictionary<int, (TimeSpan, DateTime)>();
        private readonly object _syncLock = new object();
        private bool _isDisposed = false;

        public SystemMetricsService()
        {
            // 1. Initialize CPU metadata & counters
            try
            {
                _cachedCpuName = GetCpuNameFromRegistry();
                _physicalCores = GetPhysicalCoresCount();

                _cpuTotalCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total", true);
                _cpuTotalCounter.NextValue(); // First call primes counter

                int coreCount = Environment.ProcessorCount;
                for (int i = 0; i < coreCount; i++)
                {
                    try
                    {
                        var coreCounter = new PerformanceCounter("Processor", "% Processor Time", i.ToString(), true);
                        coreCounter.NextValue();
                        _coreCounters.Add(coreCounter);
                    }
                    catch { }
                }
            }
            catch { }

            // 2. Initialize Disk I/O counters
            try
            {
                var drives = DriveInfo.GetDrives().Where(d => d.IsReady && d.DriveType == System.IO.DriveType.Fixed);
                foreach (var drive in drives)
                {
                    string driveLetter = drive.Name.TrimEnd('\\');
                    try
                    {
                        var read = new PerformanceCounter("LogicalDisk", "Disk Read Bytes/sec", driveLetter, true);
                        var write = new PerformanceCounter("LogicalDisk", "Disk Write Bytes/sec", driveLetter, true);
                        read.NextValue();
                        write.NextValue();
                        _diskCounters[driveLetter] = (read, write);
                    }
                    catch { }
                }
            }
            catch { }

            // 3. Initialize GPU metadata & counters
            try
            {
                QueryGpuMetadata();
                InitializeGpuCounters();
            }
            catch { }
        }

        private static string GetCpuNameFromRegistry()
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
                if (key != null)
                {
                    var val = key.GetValue("ProcessorNameString") as string;
                    if (!string.IsNullOrWhiteSpace(val))
                    {
                        return val.Trim();
                    }
                }
            }
            catch { }

            return Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER") ?? "CPU";
        }

        private static int GetPhysicalCoresCount()
        {
            try
            {
                int count = 0;
                using var searcher = new ManagementObjectSearcher("Select NumberOfCores from Win32_Processor");
                using var collection = searcher.Get();
                foreach (var item in collection)
                {
                    if (int.TryParse(item["NumberOfCores"]?.ToString(), out int c))
                    {
                        count += c;
                    }
                    item.Dispose();
                }
                if (count > 0) return count;
            }
            catch { }

            return Math.Max(1, Environment.ProcessorCount / 2);
        }

        private void QueryGpuMetadata()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("Select Name, AdapterRAM from Win32_VideoController");
                using var collection = searcher.Get();
                foreach (var obj in collection)
                {
                    string? name = obj["Name"]?.ToString();
                    if (!string.IsNullOrWhiteSpace(name) && !name.Contains("Basic", StringComparison.OrdinalIgnoreCase))
                    {
                        _cachedGpuName = name.Trim();
                        if (ulong.TryParse(obj["AdapterRAM"]?.ToString(), out ulong vram) && vram > 0)
                        {
                            _cachedGpuVramBytes = vram;
                        }
                        obj.Dispose();
                        break;
                    }
                    obj.Dispose();
                }
            }
            catch { }
        }

        private void InitializeGpuCounters()
        {
            try
            {
                var cat3D = new PerformanceCounterCategory("GPU Engine");
                var instanceNames = cat3D.GetInstanceNames();
                foreach (var instance in instanceNames)
                {
                    if (instance.Contains("engtype_3D", StringComparison.OrdinalIgnoreCase))
                    {
                        var counter = new PerformanceCounter("GPU Engine", "Utilization Percentage", instance, true);
                        counter.NextValue();
                        _gpu3dCounters.Add(counter);
                    }
                }
            }
            catch { }

            try
            {
                var catMem = new PerformanceCounterCategory("GPU Adapter Memory");
                var memInstances = catMem.GetInstanceNames();
                foreach (var instance in memInstances)
                {
                    var counter = new PerformanceCounter("GPU Adapter Memory", "Dedicated Usage", instance, true);
                    counter.NextValue();
                    _gpuMemoryCounters.Add(counter);
                }
            }
            catch { }
        }

        public SystemSnapshot CaptureSnapshot(string topProcessSortBy = "Memory", int topProcessCount = 8)
        {
            if (_isDisposed)
            {
                return new SystemSnapshot();
            }

            lock (_syncLock)
            {
                var snapshot = new SystemSnapshot
                {
                    Timestamp = DateTime.Now,
                    Uptime = TimeSpan.FromMilliseconds(Environment.TickCount64)
                };

                // Single-pass process and CPU sampling
                var (topProcesses, processCount, threadCount, handleCount) = SampleProcessesAndCounts(topProcessSortBy, topProcessCount);

                snapshot.Cpu = SampleCpu(processCount, threadCount, handleCount);
                snapshot.Memory = SampleMemory();
                snapshot.Gpu = SampleGpu();
                snapshot.Drives = SampleDrives();
                snapshot.Network = SampleNetwork();
                snapshot.TopProcesses = topProcesses;

                return snapshot;
            }
        }

        private CpuMetrics SampleCpu(int processCount, int threadCount, int handleCount)
        {
            var cpu = new CpuMetrics
            {
                CpuName = _cachedCpuName,
                LogicalCoresCount = Environment.ProcessorCount,
                PhysicalCoresCount = _physicalCores,
                ProcessCount = processCount,
                ThreadCount = threadCount,
                HandleCount = handleCount
            };

            try
            {
                if (_cpuTotalCounter != null)
                {
                    cpu.TotalUsagePercent = Math.Clamp(_cpuTotalCounter.NextValue(), 0.0, 100.0);
                }

                foreach (var counter in _coreCounters)
                {
                    try
                    {
                        cpu.CoreUsages.Add(Math.Clamp(counter.NextValue(), 0.0, 100.0));
                    }
                    catch
                    {
                        cpu.CoreUsages.Add(0.0);
                    }
                }

                for (int i = 0; i < cpu.CoreUsages.Count; i++)
                {
                    cpu.Cores.Add(new CoreMetric
                    {
                        CoreIndex = i,
                        UsagePercent = cpu.CoreUsages[i]
                    });
                }
            }
            catch { }

            return cpu;
        }

        private MemoryMetrics SampleMemory()
        {
            var mem = new MemoryMetrics();
            try
            {
                var memStatus = new MEMORYSTATUSEX();
                if (GlobalMemoryStatusEx(memStatus))
                {
                    mem.TotalPhysicalBytes = memStatus.ullTotalPhys;
                    mem.AvailablePhysicalBytes = memStatus.ullAvailPhys;
                    mem.UsedPhysicalBytes = memStatus.ullTotalPhys > memStatus.ullAvailPhys ? memStatus.ullTotalPhys - memStatus.ullAvailPhys : 0;
                    mem.UsagePercent = memStatus.dwMemoryLoad;

                    mem.CommitTotalGb = memStatus.ullTotalPageFile / (1024.0 * 1024.0 * 1024.0);
                    ulong commitUsed = memStatus.ullTotalPageFile > memStatus.ullAvailPageFile ? memStatus.ullTotalPageFile - memStatus.ullAvailPageFile : 0;
                    mem.CommitUsedGb = commitUsed / (1024.0 * 1024.0 * 1024.0);
                }
            }
            catch { }

            return mem;
        }

        private GpuMetrics SampleGpu()
        {
            var gpu = new GpuMetrics
            {
                GpuName = _cachedGpuName,
                TotalVramBytes = _cachedGpuVramBytes,
                IsAvailable = !string.IsNullOrWhiteSpace(_cachedGpuName)
            };

            // Sample GPU 3D utilization
            try
            {
                double max3D = 0.0;
                foreach (var counter in _gpu3dCounters)
                {
                    try
                    {
                        double val = counter.NextValue();
                        if (val > max3D) max3D = val;
                    }
                    catch { }
                }
                gpu.UsagePercent = Math.Clamp(max3D, 0.0, 100.0);
            }
            catch { }

            // Sample Dedicated VRAM usage
            try
            {
                ulong totalDedicatedUsage = 0;
                foreach (var counter in _gpuMemoryCounters)
                {
                    try
                    {
                        float val = counter.NextValue();
                        if (val > 0) totalDedicatedUsage += (ulong)val;
                    }
                    catch { }
                }

                if (totalDedicatedUsage > 0)
                {
                    gpu.UsedVramBytes = totalDedicatedUsage;
                    if (gpu.TotalVramBytes == 0 || gpu.UsedVramBytes > gpu.TotalVramBytes)
                    {
                        gpu.TotalVramBytes = Math.Max(gpu.TotalVramBytes, (ulong)(Math.Ceiling(gpu.UsedVramBytes / (1024.0 * 1024.0 * 1024.0)) * 1024.0 * 1024.0 * 1024.0));
                    }

                    gpu.AvailableVramBytes = gpu.TotalVramBytes > gpu.UsedVramBytes ? gpu.TotalVramBytes - gpu.UsedVramBytes : 0;
                    gpu.VramUsagePercent = gpu.TotalVramBytes > 0 ? (gpu.UsedVramBytes * 100.0 / gpu.TotalVramBytes) : 0;
                }
                else if (gpu.TotalVramBytes > 0)
                {
                    gpu.AvailableVramBytes = gpu.TotalVramBytes;
                }
            }
            catch { }

            return gpu;
        }

        private List<DriveMetric> SampleDrives()
        {
            var list = new List<DriveMetric>();
            try
            {
                var drives = DriveInfo.GetDrives().Where(d => d.IsReady);
                foreach (var d in drives)
                {
                    var driveMetric = new DriveMetric
                    {
                        Name = d.Name.TrimEnd('\\'),
                        VolumeLabel = string.IsNullOrWhiteSpace(d.VolumeLabel) ? "LOCAL_DISK" : d.VolumeLabel,
                        DriveType = d.DriveType.ToString(),
                        DriveFormat = d.DriveFormat,
                        TotalBytes = d.TotalSize,
                        FreeBytes = d.AvailableFreeSpace,
                        UsedBytes = Math.Max(0, d.TotalSize - d.AvailableFreeSpace)
                    };

                    driveMetric.UsagePercent = driveMetric.TotalBytes > 0
                        ? (driveMetric.UsedBytes * 100.0 / driveMetric.TotalBytes)
                        : 0.0;

                    // Check I/O Activity
                    if (_diskCounters.TryGetValue(driveMetric.Name, out var counters))
                    {
                        try
                        {
                            float r = counters.readCounter.NextValue();
                            float w = counters.writeCounter.NextValue();
                            driveMetric.ReadRateBytesSec = r;
                            driveMetric.WriteRateBytesSec = w;
                            driveMetric.IsReading = r > 1024; // > 1 KB/s reads
                            driveMetric.IsWriting = w > 1024; // > 1 KB/s writes
                        }
                        catch { }
                    }

                    list.Add(driveMetric);
                }
            }
            catch { }

            return list;
        }

        private NetworkMetrics SampleNetwork()
        {
            var net = new NetworkMetrics();
            try
            {
                var interfaces = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(ni => ni.OperationalStatus == OperationalStatus.Up &&
                                 ni.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                                 ni.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
                    .ToList();

                var primary = interfaces.FirstOrDefault(ni => ni.GetIPProperties().GatewayAddresses.Count > 0) ?? interfaces.FirstOrDefault();

                if (primary != null)
                {
                    net.AdapterName = primary.Name;
                    net.AdapterType = primary.NetworkInterfaceType.ToString();
                    net.IsConnected = true;

                    var ipProps = primary.GetIPProperties();
                    var unicast = ipProps.UnicastAddresses.FirstOrDefault(u => u.Address.AddressFamily == AddressFamily.InterNetwork);
                    if (unicast != null)
                    {
                        net.IpAddress = unicast.Address.ToString();
                    }

                    var stats = primary.GetIPStatistics();
                    net.TotalBytesReceived = stats.BytesReceived;
                    net.TotalBytesSent = stats.BytesSent;
                    net.TotalPacketsReceived = stats.UnicastPacketsReceived + stats.NonUnicastPacketsReceived;
                    net.TotalPacketsSent = stats.UnicastPacketsSent + stats.NonUnicastPacketsSent;

                    DateTime now = DateTime.UtcNow;
                    double elapsedSec = Math.Max(0.1, (now - _lastNetSampleTime).TotalSeconds);

                    if (_lastNetBytesRecv > 0 && stats.BytesReceived >= _lastNetBytesRecv)
                    {
                        net.DownloadSpeedBytesSec = (stats.BytesReceived - _lastNetBytesRecv) / elapsedSec;
                    }
                    if (_lastNetBytesSent > 0 && stats.BytesSent >= _lastNetBytesSent)
                    {
                        net.UploadSpeedBytesSec = (stats.BytesSent - _lastNetBytesSent) / elapsedSec;
                    }

                    _lastNetBytesRecv = stats.BytesReceived;
                    _lastNetBytesSent = stats.BytesSent;
                    _lastNetSampleTime = now;
                }
            }
            catch { }

            return net;
        }

        private (List<ProcessMetric> topProcesses, int totalProcesses, int totalThreads, int totalHandles) SampleProcessesAndCounts(string sortBy, int count)
        {
            var list = new List<ProcessMetric>();
            int totalThreads = 0;
            int totalHandles = 0;
            int totalProcesses = 0;

            try
            {
                var procs = Process.GetProcesses();
                totalProcesses = procs.Length;
                DateTime now = DateTime.UtcNow;
                int processorCount = Environment.ProcessorCount;

                foreach (var p in procs)
                {
                    try
                    {
                        int id = p.Id;
                        int threads = p.Threads.Count;
                        totalThreads += threads;
                        totalHandles += p.HandleCount;

                        if (id == 0) continue; // Skip idle process for top table

                        string name = p.ProcessName;
                        double memMb = p.WorkingSet64 / (1024.0 * 1024.0);

                        double cpuPercent = 0.0;
                        try
                        {
                            TimeSpan totalCpu = p.TotalProcessorTime;
                            if (_processCpuHistory.TryGetValue(id, out var last))
                            {
                                double cpuTimeMs = (totalCpu - last.totalCpuTime).TotalMilliseconds;
                                double totalMs = (now - last.sampleTime).TotalMilliseconds;
                                if (totalMs > 0)
                                {
                                    cpuPercent = Math.Clamp((cpuTimeMs / (totalMs * processorCount)) * 100.0, 0.0, 100.0);
                                }
                            }
                            _processCpuHistory[id] = (totalCpu, now);
                        }
                        catch { }

                        list.Add(new ProcessMetric
                        {
                            Id = id,
                            Name = name,
                            WorkingSetMb = memMb,
                            CpuPercent = Math.Round(cpuPercent, 1),
                            ThreadCount = threads
                        });
                    }
                    catch { }
                    finally
                    {
                        try { p.Dispose(); } catch { }
                    }
                }

                // Cleanup dead process IDs from history to prevent memory growth
                if (_processCpuHistory.Count > 500)
                {
                    var liveIds = new HashSet<int>(list.Select(p => p.Id));
                    var deadIds = _processCpuHistory.Keys.Where(k => !liveIds.Contains(k)).ToList();
                    foreach (var k in deadIds) _processCpuHistory.Remove(k);
                }

                // Sort based on user setting (case-insensitive)
                bool isCpuSort = string.Equals(sortBy, "Cpu", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(sortBy, "CPU", StringComparison.OrdinalIgnoreCase);

                IEnumerable<ProcessMetric> sorted = isCpuSort
                    ? list.OrderByDescending(p => p.CpuPercent).ThenByDescending(p => p.WorkingSetMb)
                    : list.OrderByDescending(p => p.WorkingSetMb).ThenByDescending(p => p.CpuPercent);

                return (sorted.Take(count).ToList(), totalProcesses, totalThreads, totalHandles);
            }
            catch
            {
                return (list, totalProcesses, totalThreads, totalHandles);
            }
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            lock (_syncLock)
            {
                try { _cpuTotalCounter?.Dispose(); } catch { }
                foreach (var c in _coreCounters) try { c.Dispose(); } catch { }
                _coreCounters.Clear();

                foreach (var kv in _diskCounters)
                {
                    try { kv.Value.readCounter.Dispose(); } catch { }
                    try { kv.Value.writeCounter.Dispose(); } catch { }
                }
                _diskCounters.Clear();

                foreach (var c in _gpu3dCounters) try { c.Dispose(); } catch { }
                _gpu3dCounters.Clear();

                foreach (var c in _gpuMemoryCounters) try { c.Dispose(); } catch { }
                _gpuMemoryCounters.Clear();

                _processCpuHistory.Clear();
            }
        }
    }
}

