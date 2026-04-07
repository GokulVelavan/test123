namespace SyslogAgent.Desktop.Data;

public static class EventDescriptions
{
    public static readonly Dictionary<string, Dictionary<int, string>> ByChannel = new()
    {
        ["Security"] = new()
        {
            [1102] = "Audit Log Cleared",
            [4616] = "System Time Changed",
            [4624] = "Successful Logon",
            [4625] = "Failed Logon",
            [4634] = "Logoff",
            [4648] = "Explicit Credential Logon (RunAs)",
            [4656] = "Handle to Object Requested",
            [4657] = "Registry Value Modified",
            [4663] = "Object Access Attempt",
            [4670] = "Permissions on Object Changed",
            [4672] = "Special Privileges Assigned",
            [4688] = "New Process Created",
            [4689] = "Process Exited",
            [4697] = "Service Installed",
            [4698] = "Scheduled Task Created",
            [4699] = "Scheduled Task Deleted",
            [4700] = "Scheduled Task Enabled",
            [4701] = "Scheduled Task Disabled",
            [4702] = "Scheduled Task Updated",
            [4719] = "System Audit Policy Changed",
            [4720] = "User Account Created",
            [4722] = "User Account Enabled",
            [4723] = "Password Change Attempted",
            [4724] = "Password Reset Attempted",
            [4725] = "User Account Disabled",
            [4726] = "User Account Deleted",
            [4728] = "Member Added to Global Group",
            [4732] = "Member Added to Local Group",
            [4735] = "Local Group Changed",
            [4737] = "Global Group Changed",
            [4740] = "Account Locked Out",
            [4756] = "Member Added to Universal Group",
            [4767] = "Account Unlocked",
            [4768] = "Kerberos TGT Requested",
            [4769] = "Kerberos Service Ticket Requested",
            [4770] = "Kerberos Ticket Renewed",
            [4771] = "Kerberos Pre-Auth Failed",
            [4776] = "NTLM Credential Validation",
            [4907] = "Auditing Settings Changed",
            [4946] = "Firewall Rule Added",
            [4947] = "Firewall Rule Modified",
            [4948] = "Firewall Rule Deleted",
            [4950] = "Firewall Setting Changed",
            [5140] = "Network Share Accessed",
            [5142] = "Network Share Added",
            [5145] = "Network Share Object Check",
            [5156] = "WFP Connection Allowed",
            [5157] = "WFP Connection Blocked"
        },
        ["System"] = new()
        {
            [41] = "Kernel-Power Unexpected Shutdown",
            [104] = "Event Log Cleared",
            [1001] = "Bugcheck / BSOD",
            [1074] = "System Shutdown/Restart",
            [6005] = "Event Log Service Started",
            [6006] = "Event Log Service Stopped",
            [6008] = "Unexpected Shutdown",
            [7034] = "Service Crashed",
            [7035] = "Service Control Sent",
            [7036] = "Service State Changed",
            [7040] = "Service Start Type Changed",
            [7045] = "New Service Installed",
            [10016] = "DCOM Permission Error"
        },
        ["Application"] = new()
        {
            [1000] = "Application Crash (WER)",
            [1001] = "Windows Error Reporting",
            [1002] = "Application Hang",
            [1026] = ".NET Runtime Error",
            [1033] = "MSI Install Completed",
            [1034] = "MSI Reconfiguration",
            [11707] = "MSI Install Success",
            [11708] = "MSI Install Failure"
        },
        ["Microsoft-Windows-Sysmon/Operational"] = new()
        {
            [1] = "Process Create",
            [2] = "File Creation Time Changed",
            [3] = "Network Connection",
            [5] = "Process Terminated",
            [6] = "Driver Loaded",
            [7] = "Image Loaded (DLL)",
            [8] = "CreateRemoteThread (Injection)",
            [9] = "RawAccessRead (Disk)",
            [10] = "Process Access (Handle)",
            [11] = "File Created",
            [12] = "Registry Key Created/Deleted",
            [13] = "Registry Value Set",
            [14] = "Registry Key Renamed",
            [15] = "File Stream Hash (ADS)",
            [17] = "Named Pipe Created",
            [18] = "Named Pipe Connected",
            [19] = "WMI Filter Created",
            [20] = "WMI Consumer Created",
            [21] = "WMI Consumer Bound",
            [22] = "DNS Query",
            [23] = "File Delete Archived",
            [24] = "Clipboard Change",
            [25] = "Process Tampering",
            [26] = "File Delete Logged"
        },
        ["Microsoft-Windows-PowerShell/Operational"] = new()
        {
            [4103] = "Module Logging",
            [4104] = "Script Block Logging",
            [4105] = "Script Block Start",
            [4106] = "Script Block Stop"
        },
        ["Microsoft-Windows-Windows Defender/Operational"] = new()
        {
            [1006] = "Malware Detected",
            [1007] = "Malware Action Taken",
            [1008] = "Malware Action Failed",
            [1009] = "Quarantine Restored",
            [1010] = "Quarantine Restore Failed",
            [1116] = "Threat Detected",
            [1117] = "Threat Action Taken",
            [1118] = "Threat Action Started",
            [1119] = "Threat Action Failed",
            [5001] = "Real-Time Protection Disabled",
            [5010] = "Antispyware Disabled",
            [5012] = "Antivirus Disabled"
        },
        ["Microsoft-Windows-TaskScheduler/Operational"] = new()
        {
            [106] = "Task Registered",
            [140] = "Task Updated",
            [141] = "Task Deleted",
            [200] = "Task Action Started",
            [201] = "Task Action Completed"
        },
        ["Microsoft-Windows-TerminalServices-LocalSessionManager/Operational"] = new()
        {
            [21] = "RDP Session Logon",
            [22] = "RDP Shell Start",
            [23] = "RDP Session Logoff",
            [24] = "RDP Session Disconnected",
            [25] = "RDP Session Reconnected"
        },
        ["Microsoft-Windows-RemoteDesktopServices-RdpCoreTS/Operational"] = new()
        {
            [131] = "RDP Connection Accepted",
            [140] = "RDP Connection Failed"
        },
        ["Microsoft-Windows-Windows Firewall With Advanced Security/Firewall"] = new()
        {
            [2003] = "Firewall Profile Changed",
            [2004] = "Firewall Rule Added",
            [2005] = "Firewall Rule Modified",
            [2006] = "Firewall Rule Deleted",
            [2033] = "All Rules Deleted"
        },
        ["Microsoft-Windows-WMI-Activity/Operational"] = new()
        {
            [5857] = "WMI Provider Started",
            [5858] = "WMI Provider Error",
            [5859] = "WMI Subscription Created",
            [5860] = "WMI Temp Subscription",
            [5861] = "WMI Permanent Subscription"
        },
        ["Microsoft-Windows-Bits-Client/Operational"] = new()
        {
            [3] = "BITS Job Created",
            [4] = "BITS Job Completed",
            [59] = "BITS Transfer Started",
            [60] = "BITS Transfer Stopped",
            [61] = "BITS Transfer Error"
        },
        ["Microsoft-Windows-PrintService/Operational"] = new()
        {
            [307] = "Print Job Completed",
            [316] = "Printer Added",
            [808] = "Spooler Error",
            [842] = "Spooler Driver Loaded"
        },
        ["Microsoft-Windows-DNS-Client/Operational"] = new()
        {
            [3006] = "DNS Query Completed",
            [3008] = "DNS Query Failed",
            [3020] = "DNS Response Received"
        }
    };
}
