using System;
using System.Text.RegularExpressions;
using Mono.Cecil;

public class FormatStringTokenResolver
{
    static Regex reEnvironmentToken = new Regex(@"%env\[([^\]]+)]%");

    public string ReplaceTokens(string template, ModuleDefinition moduleDefinition, VersionInfo ver)
    {
        var assemblyVersion = moduleDefinition.Assembly.Name.Version;
        var branch = ver.BranchName;

        template = template.Replace("%version%", assemblyVersion.ToString());
        template = template.Replace("%version1%", assemblyVersion.ToString(1));
        template = template.Replace("%version2%", assemblyVersion.ToString(2));
        template = template.Replace("%version3%", assemblyVersion.ToString(3));
        template = template.Replace("%version4%", assemblyVersion.ToString(4));

        template = template.Replace("%revno%", ver.Revision.ToString());

        template = template.Replace("%branch%", branch);

        template = template.Replace("%haschanges%", ver.HasChanges ? "HasChanges" : string.Empty);

        template = template.Replace("%user%", FormatUserName());
        template = template.Replace("%machine%", Environment.MachineName);

        template = reEnvironmentToken.Replace(template, FormatEnvironmentVariable);

        return template.Trim();
    }

    string FormatUserName()
    {
        return string.IsNullOrWhiteSpace(Environment.UserDomainName)
                   ? Environment.UserName
                   : string.Format(@"{0}\{1}", Environment.UserDomainName, Environment.UserName);
    }

    string FormatEnvironmentVariable(Match match)
    {
        return Environment.GetEnvironmentVariable(match.Groups[1].Value);
    }
}