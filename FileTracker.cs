using System.Security.Cryptography;
using System.Text;

public class ProcessedFileTracker : IDisposable
{
    private readonly HashSet<string> alreadyProcessed;
    private readonly StreamWriter logWriter;

    // Take log file, and add them to internal hash
    public ProcessedFileTracker(string logPath)
    {
        alreadyProcessed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (File.Exists(logPath))
        {
            foreach (var line in File.ReadLines(logPath))
            {
                alreadyProcessed.Add(line.Trim());
            }
        }

        logWriter = new StreamWriter(logPath, append: true);
    }

    public void MarkProcessed(string fileName)
    {
        var hash = ComputeHash(fileName);
        alreadyProcessed.Add(fileName);
        logWriter.Write(fileName);
    }

    public bool HasBeenProcessed(string fileName)
    {
        return alreadyProcessed.Contains(fileName);
    }

    private static string ComputeHash(string input)
    {
        using var sha256 = SHA256.Create();
        byte[] hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
        return BitConverter.ToString(hashBytes).Replace("-", "");
    }

    public void Dispose()
    {
        logWriter?.Dispose();
    }
}
