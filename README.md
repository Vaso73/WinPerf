# WinPerf 🚀

**Portable Windows GUI for iperf3 and iperf2 network throughput testing.**

WinPerf makes common iperf tests easier to run on Windows by wrapping client,
server, TCP, UDP, reverse, and saved-profile workflows in a clean desktop
interface.

It is built for admins, homelab users, and network troubleshooting sessions
where repeatable checks and readable results matter more than remembering
every command-line flag.

![Windows](https://img.shields.io/badge/Windows-10%20%7C%2011-0078D4?style=flat-square&logo=windows11&logoColor=white)
![.NET](https://img.shields.io/badge/.NET-9-512BD4?style=flat-square&logo=dotnet&logoColor=white)
![iperf](https://img.shields.io/badge/iperf3%20%7C%20iperf2-throughput%20testing-16A34A?style=flat-square)
![Portable](https://img.shields.io/badge/Portable-no%20installer-8B5CF6?style=flat-square)

## ✨ Highlights

- 🧭 Run guided iperf client tests from a Windows dashboard
- 🖥️ Use server mode when this machine should receive tests
- 🔁 Test TCP upload, TCP download / reverse, and UDP throughput
- 📊 Read live throughput samples without parsing terminal output
- 💾 Save reusable iperf profiles for repeatable network checks
- ⚙️ Configure iperf3 and iperf2 executable paths or portable engine folders
- 🧪 Open custom command mode when advanced flags are needed

## 🧩 Good For

| Scenario | Why it helps |
|---|---|
| 🏠 Homelab checks | Quickly compare VM, NAS, switch, Wi-Fi, and router paths |
| 🧑‍💻 Admin troubleshooting | Repeat the same test profile while changing cables, VLANs, VPNs, or routes |
| 📶 Wi-Fi and LAN validation | Spot real throughput changes without building commands by hand |
| 🔐 VPN testing | Compare tunnel performance with consistent duration and stream settings |
| 🧰 Portable toolkits | Keep a small Windows throughput tool ready without a full installer workflow |

## 🛠️ Typical Workflow

1. Point WinPerf to an available `iperf3.exe` or imported portable engine.
2. Choose the target server, port, protocol, duration, streams, and mode.
3. Run the test and watch the live result output.
4. Save the configuration as a profile when it is useful enough to repeat.
5. Use custom command mode for advanced troubleshooting cases.

## ⚙️ Requirements

- Windows 10 or Windows 11
- .NET Desktop Runtime 9
- `iperf3.exe` for the main workflow
- optional `iperf.exe` / `iperf2.exe` for iperf2-compatible tests

## 🧪 Test Types

| Mode | Notes |
|---|---|
| TCP upload | Standard client-to-server throughput test |
| TCP download / reverse | Server sends traffic back to the client |
| UDP upload | Useful for jitter, packet-loss, and bandwidth-limit checks |
| Server mode | Run this Windows machine as the iperf receiver |
| Custom command | Use advanced flags directly when the dashboard is not enough |

## 💾 Profiles

WinPerf can save reusable iperf profiles so common checks do not have to be
rebuilt every time. Profiles keep the important test settings together and make
repeatable before/after troubleshooting easier.

## 📦 Releases

Release builds are published as a ZIP containing `WinPerf.exe`.

[View releases](https://github.com/Vaso73/WinPerf/releases)

## 💡 Goal

WinPerf is meant to be a calm, practical companion for network testing: quick
enough for daily troubleshooting, clear enough for repeatable checks, and still
close enough to iperf for advanced users to know what is happening.
