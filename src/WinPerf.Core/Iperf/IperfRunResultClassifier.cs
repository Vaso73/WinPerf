namespace WinPerf.Core.Iperf;

public static class IperfRunResultClassifier
{
    public static IperfRunOutcome Classify(
        IperfEngine engine,
        IperfRunResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (result.ExitCode != 0)
        {
            return new IperfRunOutcome(
                IperfRunOutcomeKind.Failed,
                $"Test failed: process exited with code {result.ExitCode}.");
        }

        // Preserve the established iperf3 behaviour. iperf3 reliably uses
        // its exit code for process-level failures.
        if (engine != IperfEngine.Iperf2)
        {
            return new IperfRunOutcome(
                IperfRunOutcomeKind.Completed,
                "Test completed.");
        }

        var standardError = result.Output
            .Where(line => line.Stream == IperfOutputStream.StandardError)
            .Select(line => line.Text.Trim())
            .Where(text => text.Length > 0)
            .ToArray();

        if (standardError.Length == 0)
        {
            return new IperfRunOutcome(
                IperfRunOutcomeKind.Completed,
                "Test completed.");
        }

        var hasThroughputSample = result.Output.Any(
            line =>
                line.Stream == IperfOutputStream.StandardOutput &&
                Iperf2TextParser.TryParseIntervalSample(line.Text, out _));

        var fatalError = standardError.FirstOrDefault(
            text =>
                !IsKnownIperf2CompletionWarning(text) &&
                IsFatalIperf2Error(text));

        if (fatalError is not null)
        {
            return new IperfRunOutcome(
                IperfRunOutcomeKind.Failed,
                "Test failed: " + fatalError);
        }

        if (hasThroughputSample)
        {
            return new IperfRunOutcome(
                IperfRunOutcomeKind.CompletedWithWarning,
                "Test completed with warning: " +
                string.Join(" | ", standardError));
        }

        return new IperfRunOutcome(
            IperfRunOutcomeKind.Failed,
            "Test failed: " + string.Join(" | ", standardError));
    }

    private static bool IsKnownIperf2CompletionWarning(string text)
    {
        return
            Contains(text, "read udp fin failed") ||
            Contains(text, "did not receive ack of last datagram");
    }

    private static bool IsFatalIperf2Error(string text)
    {
        return
            Contains(text, "connection refused") ||
            Contains(text, "unable to connect") ||
            Contains(text, "no route to host") ||
            Contains(text, "network is unreachable") ||
            Contains(text, "connection timed out") ||
            Contains(text, "operation timed out") ||
            Contains(text, "could not resolve") ||
            Contains(text, "name or service not known") ||
            Contains(text, "unknown host") ||
            Contains(text, "server is busy") ||
            (Contains(text, "connect") && Contains(text, "failed"));
    }

    private static bool Contains(string text, string value)
    {
        return text.Contains(value, StringComparison.OrdinalIgnoreCase);
    }
}
