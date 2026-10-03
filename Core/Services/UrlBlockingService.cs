using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Text;

namespace FocusPomodoro.Core.Services;

public class UrlBlockingService
{
    private static readonly string HostsFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.System),
        "drivers",
        "etc",
        "hosts"
    );

    private const string BlockHeader = "# --- FocusPomodoro Block Start ---";
    private const string BlockFooter = "# --- FocusPomodoro Block End ---";

    public void BlockUrls(string blockedUrlsText)
    {
        if (string.IsNullOrWhiteSpace(blockedUrlsText))
            return;

        var urls = blockedUrlsText
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(u => u.Trim())
            .Where(u => !string.IsNullOrEmpty(u))
            .ToList();

        if (urls.Count == 0)
            return;

        string content = ReadHostsFile();
        content = RemoveBlockSection(content);

        var sb = new StringBuilder(content);
        if (!content.EndsWith(Environment.NewLine) && content.Length > 0)
        {
            sb.AppendLine();
        }

        sb.AppendLine(BlockHeader);
        foreach (var url in urls)
        {
            var domain = NormalizeDomain(url);
            if (!string.IsNullOrEmpty(domain))
            {
                sb.AppendLine($"127.0.0.1 {domain}");
                sb.AppendLine($"127.0.0.1 www.{domain}");
            }
        }
        sb.AppendLine(BlockFooter);

        WriteHostsFile(sb.ToString());
        FlushDns();
    }

    public void UnblockUrls()
    {
        try
        {
            if (!File.Exists(HostsFilePath))
                return;

            string content = ReadHostsFile();
            string newContent = RemoveBlockSection(content);

            if (content != newContent)
            {
                WriteHostsFile(newContent);
                FlushDns();
            }
        }
        catch
        {
            // Ignore unblock errors
        }
    }

    private string ReadHostsFile()
    {
        if (!File.Exists(HostsFilePath))
            return string.Empty;

        return File.ReadAllText(HostsFilePath);
    }

    private void WriteHostsFile(string content)
    {
        File.WriteAllText(HostsFilePath, content);
    }

    private string RemoveBlockSection(string content)
    {
        int startIndex = content.IndexOf(BlockHeader);
        if (startIndex == -1)
            return content;

        int endIndex = content.IndexOf(BlockFooter, startIndex);
        if (endIndex == -1)
        {
            return content.Substring(0, startIndex);
        }

        string before = content.Substring(0, startIndex);
        string after = content.Substring(endIndex + BlockFooter.Length);

        after = after.TrimStart('\r', '\n');

        return before + after;
    }

    private string NormalizeDomain(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return string.Empty;

        string domain = url.Trim();

        if (domain.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            domain = domain.Substring(7);
        else if (domain.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            domain = domain.Substring(8);

        int slashIndex = domain.IndexOf('/');
        if (slashIndex != -1)
            domain = domain.Substring(0, slashIndex);

        if (domain.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
            domain = domain.Substring(4);

        return domain.Trim();
    }

    private void FlushDns()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "ipconfig",
                Arguments = "/flushdns",
                CreateNoWindow = true,
                UseShellExecute = false
            };
            Process.Start(psi)?.WaitForExit();
        }
        catch { }
    }
}
