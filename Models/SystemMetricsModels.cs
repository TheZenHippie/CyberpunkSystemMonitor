using System;
using System.Collections.Generic;

namespace CyberpunkSystemMonitor.Models
{
    public class CoreMetric
    {
        public int CoreIndex { get; set; }
        public string Label => $"C{CoreIndex:00}:";
        public double UsagePercent { get; set; }
    }

    public class CpuMetrics
    {
        public double TotalUsagePercent { get; set; }
        public string CpuName { get; set; } = "Generic CPU";
        public int LogicalCoresCount { get; set; } = 1;
        public int PhysicalCoresCount { get; set; } = 1;
        public int ThreadCount { get; set; }
        public int ProcessCount { get; set; }
        public int HandleCount { get; set; }
        public List<double> CoreUsages { get; set; } = new List<double>();
        public List<CoreMetric> Cores { get; set; } = new List<CoreMetric>();
    }

    public class MemoryMetrics
    {
        public ulong TotalPhysicalBytes { get; set; }
        public ulong AvailablePhysicalBytes { get; set; }
        public ulong UsedPhysicalBytes { get; set; }
        public double UsagePercent { get; set; }

        public double TotalPhysicalGb => TotalPhysicalBytes / (1024.0 * 1024.0 * 1024.0);
        public double UsedPhysicalGb => UsedPhysicalBytes / (1024.0 * 1024.0 * 1024.0);
        public double AvailablePhysicalGb => AvailablePhysicalBytes / (1024.0 * 1024.0 * 1024.0);

        public double CommitTotalGb { get; set; }
        public double CommitUsedGb { get; set; }
        public double CommitUsagePercent => CommitTotalGb > 0 ? (CommitUsedGb / CommitTotalGb) * 100.0 : 0.0;
    }

    public class GpuMetrics
    {
        public string GpuName { get; set; } = "GPU";
        public double UsagePercent { get; set; }
        public ulong TotalVramBytes { get; set; }
        public ulong UsedVramBytes { get; set; }
        public ulong AvailableVramBytes { get; set; }
        public double VramUsagePercent { get; set; }

        public double TotalVramGb => TotalVramBytes / (1024.0 * 1024.0 * 1024.0);
        public double UsedVramGb => UsedVramBytes / (1024.0 * 1024.0 * 1024.0);
        public double AvailableVramGb => AvailableVramBytes / (1024.0 * 1024.0 * 1024.0);

        public bool IsAvailable { get; set; }
    }

    public class DriveMetric
    {
        public string Name { get; set; } = "C:";
        public string DriveLetter => Name;
        public string VolumeLabel { get; set; } = "";
        public string DriveType { get; set; } = "Fixed";
        public string DriveFormat { get; set; } = "NTFS";
        public long TotalBytes { get; set; }
        public long FreeBytes { get; set; }
        public long UsedBytes { get; set; }
        public double UsagePercent { get; set; }
        public double UsedPercent => UsagePercent;

        public double TotalGb => TotalBytes / (1024.0 * 1024.0 * 1024.0);
        public double FreeGb => FreeBytes / (1024.0 * 1024.0 * 1024.0);
        public double UsedGb => UsedBytes / (1024.0 * 1024.0 * 1024.0);

        public string SpaceSummary => $"{UsedGb:F1} GB / {TotalGb:F1} GB ({UsagePercent:F1}%)";
        public string FreeSpaceFormatted => $"{FreeGb:F1} GB";

        public bool IsReading { get; set; }
        public bool IsWriting { get; set; }
        public double ReadRateBytesSec { get; set; }
        public double WriteRateBytesSec { get; set; }

        public string ReadSpeedFormatted => FormatSpeed(ReadRateBytesSec);
        public string WriteSpeedFormatted => FormatSpeed(WriteRateBytesSec);

        private static string FormatSpeed(double bytesPerSec)
        {
            if (bytesPerSec < 1024) return $"{bytesPerSec:0} B/s";
            if (bytesPerSec < 1024 * 1024) return $"{bytesPerSec / 1024.0:F1} KB/s";
            if (bytesPerSec < 1024 * 1024 * 1024) return $"{bytesPerSec / (1024.0 * 1024.0):F1} MB/s";
            return $"{bytesPerSec / (1024.0 * 1024.0 * 1024.0):F2} GB/s";
        }
    }

    public class NetworkMetrics
    {
        public string AdapterName { get; set; } = "Network Adapter";
        public string AdapterType { get; set; } = "Ethernet";
        public string IpAddress { get; set; } = "127.0.0.1";
        public double DownloadSpeedBytesSec { get; set; }
        public double UploadSpeedBytesSec { get; set; }
        public long TotalPacketsReceived { get; set; }
        public long TotalPacketsSent { get; set; }
        public long TotalBytesReceived { get; set; }
        public long TotalBytesSent { get; set; }
        public bool IsConnected { get; set; }

        public string DownloadSpeedFormatted => FormatSpeed(DownloadSpeedBytesSec);
        public string UploadSpeedFormatted => FormatSpeed(UploadSpeedBytesSec);
        public double TotalMbReceived => TotalBytesReceived / (1024.0 * 1024.0);
        public double TotalMbSent => TotalBytesSent / (1024.0 * 1024.0);

        private static string FormatSpeed(double bytesPerSec)
        {
            if (bytesPerSec < 1024) return $"{bytesPerSec:0} B/s";
            if (bytesPerSec < 1024 * 1024) return $"{bytesPerSec / 1024.0:F1} KB/s";
            if (bytesPerSec < 1024 * 1024 * 1024) return $"{bytesPerSec / (1024.0 * 1024.0):F1} MB/s";
            return $"{bytesPerSec / (1024.0 * 1024.0 * 1024.0):F2} GB/s";
        }
    }

    public class ProcessMetric
    {
        public int Id { get; set; }
        public string Name { get; set; } = "Unknown";
        public string ProcessName => Name;
        public double WorkingSetMb { get; set; }
        public double CpuPercent { get; set; }
        public int ThreadCount { get; set; }

        public string MemoryFormatted => WorkingSetMb >= 1024 ? $"{WorkingSetMb / 1024.0:F2} GB" : $"{WorkingSetMb:F1} MB";
        public string CpuFormatted => $"{CpuPercent:F1}%";
    }

    public class SystemSnapshot
    {
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public TimeSpan Uptime { get; set; }
        public CpuMetrics Cpu { get; set; } = new CpuMetrics();
        public MemoryMetrics Memory { get; set; } = new MemoryMetrics();
        public GpuMetrics Gpu { get; set; } = new GpuMetrics();
        public List<DriveMetric> Drives { get; set; } = new List<DriveMetric>();
        public NetworkMetrics Network { get; set; } = new NetworkMetrics();
        public List<ProcessMetric> TopProcesses { get; set; } = new List<ProcessMetric>();
    }
}

