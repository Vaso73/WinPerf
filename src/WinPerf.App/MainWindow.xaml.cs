using System.Windows;
using System.Windows.Controls;
using WinPerf.Core.Iperf;

namespace WinPerf.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void StartButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var options = new IperfTestOptions
            {
                Server = ServerBox.Text.Trim(),
                Port = ParsePositiveInt(PortBox, "Port"),
                Streams = ParsePositiveInt(StreamsBox, "Streams"),
                DurationSeconds = ParsePositiveInt(DurationBox, "Duration"),
                Mode = GetSelectedMode(),
                AddressFamily = IperfAddressFamily.IPv4
            };

            var command = IperfCommandBuilder.BuildClientCommand("iperf3.exe", options);

            EngineOutputText.Text =
                "Command preview:" + Environment.NewLine +
                command.ToDisplayString();
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException or FormatException)
        {
            EngineOutputText.Text = "Invalid test configuration:" + Environment.NewLine + ex.Message;
        }
    }



    private void AdvancedCommandButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new AdvancedCommandWindow
        {
            Owner = this
        };

        if (dialog.ShowDialog() == true)
        {
            EngineOutputText.Text =
                "Advanced command preview:" + Environment.NewLine +
                dialog.CommandText;
        }
    }

    private void CustomCommandButton_Click(object sender, RoutedEventArgs e)
    {
        var initialCommand = EngineOutputText.Text.StartsWith("Command preview:", StringComparison.Ordinal)
            ? EngineOutputText.Text.Replace("Command preview:", string.Empty, StringComparison.Ordinal).Trim()
            : "iperf3.exe -c 10.100.100.1 -p 5201 -t 10 -P 10 --json-stream -4";

        var dialog = new CustomCommandWindow(initialCommand)
        {
            Owner = this
        };

        if (dialog.ShowDialog() == true)
        {
            EngineOutputText.Text =
                "Custom command preview:" + Environment.NewLine +
                dialog.CommandText;
        }
    }

    private IperfMode GetSelectedMode()
    {
        var selectedText = (ModeBox.SelectedItem as ComboBoxItem)?.Content?.ToString();

        return selectedText switch
        {
            "TCP Upload" => IperfMode.TcpUpload,
            "TCP Download" => IperfMode.TcpDownload,
            "TCP Bidirectional" => IperfMode.TcpBidirectional,
            "UDP Upload" => IperfMode.UdpUpload,
            "UDP Download" => IperfMode.UdpDownload,
            _ => IperfMode.TcpUpload
        };
    }

    private static int ParsePositiveInt(TextBox box, string fieldName)
    {
        if (!int.TryParse(box.Text.Trim(), out var value) || value < 1)
        {
            throw new FormatException($"{fieldName} must be a positive number.");
        }

        return value;
    }
}
