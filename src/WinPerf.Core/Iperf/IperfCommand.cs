namespace WinPerf.Core.Iperf;

public sealed record IperfCommand(string ExecutablePath, IReadOnlyList<string> Arguments)
{
    public string ToDisplayString()
    {
        static string Quote(string value)
        {
            return value.Any(char.IsWhiteSpace) ? $"\"{value}\"" : value;
        }

        return string.Join(" ", new[] { Quote(ExecutablePath) }.Concat(Arguments.Select(Quote)));
    }
}
