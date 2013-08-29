using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Mono.Cecil;
using SharpSvn;
using System.Collections.ObjectModel;
using System.Reflection;

public class ModuleWeaver
{
    public Action<string> LogInfo { get; set; }
    public Action<string> LogWarning { get; set; }
    public ModuleDefinition ModuleDefinition { get; set; }
    public string SolutionDirectoryPath { get; set; }
    public string AddinDirectoryPath { get; set; }
    public string AssemblyFilePath { get; set; }
    static bool isPathSet;
    readonly FormatStringTokenResolver formatStringTokenResolver;
    string assemblyInfoVersion;
    Version assemblyVersion;
    bool dotGitDirExists;

    Assembly sharpSvn;
    Assembly sharpSvnUI;

    public ModuleWeaver()
    {
        LogInfo = s => { };
        LogWarning = s => { };
        formatStringTokenResolver = new FormatStringTokenResolver();

        AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;

        sharpSvn = typeof(SharpSvn.SvnWorkingCopyClient).Assembly;
        sharpSvnUI = typeof(SharpSvn.UI.SvnUI).Assembly;

        LogInfo(sharpSvn.FullName);
        LogInfo(sharpSvnUI.FullName);
    }

    System.Reflection.Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
    {
        LogInfo(args.Name);

        if (args.Name == sharpSvn.FullName)
            return sharpSvn;

        if (args.Name == sharpSvnUI.FullName)
            return sharpSvnUI;

        return null;
    }

    public void Execute()
    {
        SetSearchPath();
        var customAttributes = ModuleDefinition.Assembly.CustomAttributes;

        var svnDir = SvnDirFinder.TreeWalkForSvnDir(SolutionDirectoryPath);
        if (svnDir == null)
        {
            LogWarning("No .svn directory found.");
            return;
        }
        dotGitDirExists = true;

        VersionInfo ver = null;
        try
        {
            ver = GetSvnInfo(svnDir);
        }
        catch (Exception ex)
        {
            LogWarning("Svn error: " + ex.ToString());
        }


        assemblyVersion = ModuleDefinition.Assembly.Name.Version;

        /* AssemblyVersionAttribute */
        var customAttribute = customAttributes.FirstOrDefault(x => x.AttributeType.Name == "AssemblyVersionAttribute");
        if (customAttribute != null)
        {
            assemblyInfoVersion = (string)customAttribute.ConstructorArguments[0].Value;
            assemblyInfoVersion = ReplaceVersion3rd(assemblyInfoVersion, ver.Revision);
            VerifyStartsWithVersion(assemblyInfoVersion);
            customAttribute.ConstructorArguments[0] = new CustomAttributeArgument(ModuleDefinition.TypeSystem.String, assemblyInfoVersion);
        }
        else
        {
            var versionAttribute = GetVersionAttribute();
            var constructor = ModuleDefinition.Import(versionAttribute.Methods.First(x => x.IsConstructor));
            customAttribute = new CustomAttribute(constructor);

            assemblyInfoVersion = (string)customAttribute.ConstructorArguments[0].Value;
            assemblyInfoVersion = ReplaceVersion3rd(assemblyInfoVersion, ver.Revision);

            customAttribute.ConstructorArguments.Add(new CustomAttributeArgument(ModuleDefinition.TypeSystem.String, assemblyInfoVersion));
            customAttributes.Add(customAttribute);
        }

        /* AssemblyFileVersionAttribute */
        customAttribute = customAttributes.FirstOrDefault(x => x.AttributeType.Name == "AssemblyFileVersionAttribute");
        if (customAttribute != null)
        {
            assemblyInfoVersion = (string)customAttribute.ConstructorArguments[0].Value;
            assemblyInfoVersion = ReplaceVersion3rd(assemblyInfoVersion, ver.Revision);
            VerifyStartsWithVersion(assemblyInfoVersion);
            customAttribute.ConstructorArguments[0] = new CustomAttributeArgument(ModuleDefinition.TypeSystem.String, assemblyInfoVersion);
        }
        else
        {
            var versionAttribute = GetVersionAttribute();
            var constructor = ModuleDefinition.Import(versionAttribute.Methods.First(x => x.IsConstructor));
            customAttribute = new CustomAttribute(constructor);

            assemblyInfoVersion = (string)customAttribute.ConstructorArguments[0].Value;
            assemblyInfoVersion = ReplaceVersion3rd(assemblyInfoVersion, ver.Revision);

            customAttribute.ConstructorArguments.Add(new CustomAttributeArgument(ModuleDefinition.TypeSystem.String, assemblyInfoVersion));
            customAttributes.Add(customAttribute);
        }

        /* AssemblyInformationalVersionAttribute */
        customAttribute = customAttributes.FirstOrDefault(x => x.AttributeType.Name == "AssemblyInformationalVersionAttribute");
        if (customAttribute != null)
        {
            assemblyInfoVersion = (string)customAttribute.ConstructorArguments[0].Value;
            assemblyInfoVersion = formatStringTokenResolver.ReplaceTokens(assemblyInfoVersion, ModuleDefinition, ver);
            VerifyStartsWithVersion(assemblyInfoVersion);
            customAttribute.ConstructorArguments[0] = new CustomAttributeArgument(ModuleDefinition.TypeSystem.String, assemblyInfoVersion);
        }
        else
        {
            var versionAttribute = GetVersionAttribute();
            var constructor = ModuleDefinition.Import(versionAttribute.Methods.First(x => x.IsConstructor));
            customAttribute = new CustomAttribute(constructor);
            if (!ver.HasChanges)
            {
                assemblyInfoVersion = string.Format("{0} Path:'{1}' Rev:{2}", assemblyVersion, ver.BranchName, ver.Revision);
            }
            else
            {
                assemblyInfoVersion = string.Format("{0} Path:'{1}' Rev:{2} HasPendingChanges", assemblyVersion, ver.BranchName, ver.Revision);
            }
            customAttribute.ConstructorArguments.Add(new CustomAttributeArgument(ModuleDefinition.TypeSystem.String, assemblyInfoVersion));
            customAttributes.Add(customAttribute);
        }


    }

    private string ReplaceVersion3rd(string versionString, int rev)
    {
        Version fake;
        if (!Version.TryParse(assemblyInfoVersion, out fake))
        {
            throw new WeavingException("The version string must be prefixed with a valid Version. The following string does not: " + versionString);
        }

        var ret = new Version(fake.Major, fake.Minor, rev, fake.Revision);

        return ret.ToString();
    }

    void VerifyStartsWithVersion(string versionString)
    {
        var prefix = new string(versionString.TakeWhile(x => char.IsDigit(x) || x == '.').ToArray());
        Version fake;
        if (!Version.TryParse(prefix, out fake))
        {
            throw new WeavingException("The version string must be prefixed with a valid Version. The following string does not: " + versionString);
        }
    }

    static VersionInfo GetSvnInfo(string targetFolder)
    {
        SvnWorkingCopyVersion version;
        using (SvnWorkingCopyClient client = new SvnWorkingCopyClient())
        {
            client.GetVersion(targetFolder, out version);
        }

        SvnInfoEventArgs info;
        using (SvnClient client = new SvnClient())
        {
            client.GetInfo(targetFolder, out info);
        }

        return new VersionInfo()
        {
            BranchName = info.Uri.AbsolutePath,
            HasChanges = version.Modified,
            Revision = (int)version.End,
        };
    }

    void SetSearchPath()
    {
        if (isPathSet)
        {
            return;
        }
        isPathSet = true;
        var nativeBinaries = Path.Combine(AddinDirectoryPath, "NativeBinaries", GetProcessorArchitecture());
        var existingPath = Environment.GetEnvironmentVariable("PATH");
        var newPath = string.Concat(nativeBinaries, Path.PathSeparator, existingPath);
        Environment.SetEnvironmentVariable("PATH", newPath);
    }

    static string GetProcessorArchitecture()
    {
        if (Environment.Is64BitProcess)
        {
            return "amd64";
        }
        return "x86";
    }

    TypeDefinition GetVersionAttribute()
    {
        var msCoreLib = ModuleDefinition.AssemblyResolver.Resolve("mscorlib");
        var msCoreAttribute = msCoreLib.MainModule.Types.FirstOrDefault(x => x.Name == "AssemblyInformationalVersionAttribute");
        if (msCoreAttribute != null)
        {
            return msCoreAttribute;
        }
        var systemRuntime = ModuleDefinition.AssemblyResolver.Resolve("System.Runtime");
        return systemRuntime.MainModule.Types.First(x => x.Name == "AssemblyInformationalVersionAttribute");
    }

    public void AfterWeaving()
    {
        if (!dotGitDirExists)
        {
            return;
        }
        var verPatchPath = Path.Combine(AddinDirectoryPath, "verpatch.exe");
        var arguments = string.Format("{0} /pv \"{1}\" /high /va {2}", AssemblyFilePath, assemblyInfoVersion, assemblyVersion);
        LogInfo(string.Format("Patching version using: {0} {1}", verPatchPath, arguments));
        var startInfo = new ProcessStartInfo
                        {
                            FileName = verPatchPath,
                            Arguments = arguments,
                            CreateNoWindow = true,
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            WorkingDirectory = Path.GetTempPath()
                        };
        using (var process = Process.Start(startInfo))
        {
            if (!process.WaitForExit(1000))
            {
                var timeoutMessage = string.Format("Failed to apply product version to Win32 resources in 1 second.\r\nFailed command: {0} {1}", verPatchPath, arguments);
                throw new WeavingException(timeoutMessage);
            }

            if (process.ExitCode == 0)
            {
                return;
            }
            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            var message = string.Format("Failed to apply product version to Win32 resources.\r\nOutput: {0}\r\nError: {1}", output, error);
            throw new WeavingException(message);
        }
    }
}