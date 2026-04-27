using System.Text;
using System.Windows;
using System.Windows.Controls;
using WinPerf.App.Settings;

namespace WinPerf.App;

public partial class AdvancedCommandWindow : Window
{
    public AdvancedCommandWindow()
    {
        InitializeComponent();
        WindowPlacementStore.Track(this, "AdvancedCommandWindow");
    }

    public string CommandText => PreviewBox.Text.Trim();

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        UpdatePreview();
    }

    private void AnyInputChanged(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded)
        {
            return;
        }

        UpdatePreview();
    }

    private void UseButton_Click(object sender, RoutedEventArgs e)
    {
        UpdatePreview();

        if (string.IsNullOrWhiteSpace(CommandText) || !string.IsNullOrWhiteSpace(ValidationText.Text))
        {
            MessageBox.Show(
                this,
                "Fix the advanced command options first.",
                "WinPerf",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void UpdatePreview()
    {
        var validation = ValidateOptions();

        if (!string.IsNullOrWhiteSpace(validation))
        {
            PreviewBox.Text = string.Empty;
            ValidationText.Text = validation;
            return;
        }

        var args = BuildArguments();

        PreviewBox.Text = string.Join(" ", new[] { "iperf3.exe" }.Concat(args.Select(QuoteIfNeeded)));
        ValidationText.Text = "Ready";
        ValidationText.Foreground = (System.Windows.Media.Brush)FindResource("AccentGreen");
    }

    private string? ValidateOptions()
    {
        if (!IsPositiveInt(PortBox.Text, out var port) || port is < 1 or > 65535)
        {
            return "Port must be between 1 and 65535.";
        }

        if (IsClientMode() && string.IsNullOrWhiteSpace(ServerAddressBox.Text))
        {
            return "Client mode requires a server address.";
        }

        if (IsClientMode() && !IsPositiveInt(StreamsBox.Text, out _))
        {
            return "Streams must be a positive number.";
        }

        if (IsClientMode() && !IsPositiveInt(DurationBox.Text, out _))
        {
            return "Duration must be a positive number.";
        }

        if (!string.IsNullOrWhiteSpace(IntervalBox.Text) && !IsPositiveInt(IntervalBox.Text, out _))
        {
            return "Report interval must be empty or a positive number.";
        }

        if (ReverseBox.IsChecked == true && BidirectionalBox.IsChecked == true)
        {
            return "Reverse and bidirectional cannot be enabled together.";
        }

        return null;
    }

    private List<string> BuildArguments()
    {
        var args = new List<string>();

        if (IsServerMode())
        {
            args.Add("-s");
        }
        else
        {
            args.Add("-c");
            args.Add(ServerAddressBox.Text.Trim());
        }

        AddPair(args, "-p", PortBox.Text.Trim());

        var bind = BindAddressBox.Text.Trim();
        if (!string.IsNullOrWhiteSpace(bind))
        {
            AddPair(args, "-B", bind);
        }

        switch (SelectedText(IpVersionBox))
        {
            case "IPv4":
                args.Add("-4");
                break;
            case "IPv6":
                args.Add("-6");
                break;
        }

        if (SelectedText(ProtocolBox) == "UDP")
        {
            args.Add("-u");

            if (!string.IsNullOrWhiteSpace(UdpBandwidthBox.Text))
            {
                AddPair(args, "-b", UdpBandwidthBox.Text.Trim());
            }
        }

        if (IsClientMode())
        {
            AddPair(args, "-P", StreamsBox.Text.Trim());
            AddPair(args, "-t", DurationBox.Text.Trim());

            if (ReverseBox.IsChecked == true)
            {
                args.Add("-R");
            }

            if (BidirectionalBox.IsChecked == true)
            {
                args.Add("--bidir");
            }
        }

        if (!string.IsNullOrWhiteSpace(IntervalBox.Text))
        {
            AddPair(args, "-i", IntervalBox.Text.Trim());
        }

        var bufferLength = BufferLengthBox.Text.Trim();
        if (!string.IsNullOrWhiteSpace(bufferLength))
        {
            AddPair(args, "-l", bufferLength);
        }

        var windowSize = WindowSizeBox.Text.Trim();
        if (!string.IsNullOrWhiteSpace(windowSize))
        {
            AddPair(args, "-w", windowSize);
        }

        var dscp = DscpBox.Text.Trim();
        if (!string.IsNullOrWhiteSpace(dscp))
        {
            AddPair(args, "--dscp", dscp);
        }

        var format = SelectedTag(FormatBox);
        if (!string.IsNullOrWhiteSpace(format))
        {
            AddPair(args, "-f", format);
        }

        if (IsServerMode() && OneOffServerBox.IsChecked == true)
        {
            args.Add("-1");
        }

        if (VerboseBox.IsChecked == true)
        {
            args.Add("-V");
        }

        if (JsonStreamBox.IsChecked == true)
        {
            args.Add("--json-stream");
        }

        return args;
    }

    private static void AddPair(List<string> args, string name, string value)
    {
        args.Add(name);
        args.Add(value);
    }

    private bool IsClientMode() => SelectedText(RunModeBox) == "Client mode";

    private bool IsServerMode() => SelectedText(RunModeBox) == "Server mode";

    private static bool IsPositiveInt(string value, out int number)
    {
        return int.TryParse(value.Trim(), out number) && number > 0;
    }

    private static string SelectedText(ComboBox comboBox)
    {
        return (comboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? string.Empty;
    }

    private static string SelectedTag(ComboBox comboBox)
    {
        return (comboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? string.Empty;
    }

    private static string QuoteIfNeeded(string value)
    {
        return value.Any(char.IsWhiteSpace) ? $"\"{value}\"" : value;
    }
}
