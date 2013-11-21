using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Mono.Cecil;
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

    public ISvnHelper SvnHelper { get; set; }

    static bool isPathSet;
    readonly FormatStringTokenResolver formatStringTokenResolver;
    string assemblyInfoVersion;

    Version assemblyVersion;

    string assemblyVersionReplaced;

    bool dotSvnDirExists;

    public ModuleWeaver()
    {
        LogInfo = s => { };
        LogWarning = s => { };
        formatStringTokenResolver = new FormatStringTokenResolver();

        AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;
    }

    Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
    {
        if (args.Name == "SharpSvn, Version=1.8003.2513.15185, Culture=neutral, PublicKeyToken=d729672594885a28")
        {
            LogInfo("Loading AssemblyResolve Name: " + args.Name);

            var sharpSvnPath = Path.Combine(AddinDirectoryPath, "SharpSvn18.dll");
            var asm = Assembly.LoadFrom(sharpSvnPath);

            LogInfo("Loaded AssemblyResolve FullName: " + asm.FullName);

            return asm;
        }
        else if (args.Name == "SharpSvn, Version=1.7013.2566.15257, Culture=neutral, PublicKeyToken=d729672594885a28")
        {
            LogInfo("Loading AssemblyResolve Name: " + args.Name);

            var sharpSvnPath = Path.Combine(AddinDirectoryPath, "SharpSvn.dll");
            var asm = Assembly.LoadFrom(sharpSvnPath);

            LogInfo("Loaded AssemblyResolve FullName: " + asm.FullName);

            return asm;
        }

        return null;
    }

    public void Execute()
    {
        if (SvnHelper == null)
        {
            SvnHelper = new SvnHelper();
        }

        var customAttributes = ModuleDefinition.Assembly.CustomAttributes;

        var svnDir = SvnHelper.TreeWalkForSvnDir(SolutionDirectoryPath);
        if (svnDir == null)
        {
            LogWarning("No .svn directory found.");
            return;
        }

        dotSvnDirExists = true;

        VersionInfo ver = null;
        try
        {
            ver = SvnHelper.GetSvnInfo(svnDir);
        }
        catch (Exception ex)
        {
            LogWarning("GetSvnInfo error: " + ex.ToString());
        }

        LogInfo("svnInfo found.");

        assemblyVersion = ModuleDefinition.Assembly.Name.Version;

        assemblyVersionReplaced = ReplaceVersion3rd(assemblyVersion.ToString(), ver.Revision);

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

            if (versionAttribute == null)
            {
                throw new InvalidOperationException("versionAttribute not found.");
            }

            var constructor = ModuleDefinition.Import(versionAttribute.Methods.First(x => x.IsConstructor));

            customAttribute = new CustomAttribute(constructor);

            assemblyInfoVersion = customAttribute.ConstructorArguments.Count == 1
                ? (string)customAttribute.ConstructorArguments[0].Value
                : assemblyVersion.ToString();

            assemblyInfoVersion = ReplaceVersion3rd(assemblyInfoVersion, ver.Revision);

            customAttribute.ConstructorArguments.Add(new CustomAttributeArgument(ModuleDefinition.TypeSystem.String, assemblyInfoVersion));
            customAttributes.Add(customAttribute);
        }
        LogInfo("AssemblyVersionAttribute processed.");

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
            var versionAttribute = GetFileVersionAttribute();
            var constructor = ModuleDefinition.Import(versionAttribute.Methods.First(x => x.IsConstructor));
            customAttribute = new CustomAttribute(constructor);

            assemblyInfoVersion = customAttribute.ConstructorArguments.Count == 1
                ? (string)customAttribute.ConstructorArguments[0].Value
                : assemblyVersion.ToString();

            assemblyInfoVersion = ReplaceVersion3rd(assemblyInfoVersion, ver.Revision);

            customAttribute.ConstructorArguments.Add(new CustomAttributeArgument(ModuleDefinition.TypeSystem.String, assemblyInfoVersion));
            customAttributes.Add(customAttribute);
        }
        LogInfo("AssemblyFileVersionAttribute processed.");

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
            var versionAttribute = GetInformationalVersionAttribute();
            var constructor = ModuleDefinition.Import(versionAttribute.Methods.First(x => x.IsConstructor));
            customAttribute = new CustomAttribute(constructor);
            if (!ver.HasChanges)
            {
                assemblyInfoVersion = string.Format("{0} {1} Path:'{2}' r{3}",
                    assemblyVersionReplaced,
                    Environment.MachineName,
                    ver.BranchName,
                    ver.Revision);
            }
            else
            {
                assemblyInfoVersion = string.Format("{0} {1} Path:'{2}' r{3} HasChanges",
                    assemblyVersionReplaced,
                    Environment.MachineName,
                    ver.BranchName,
                    ver.Revision);
            }
            customAttribute.ConstructorArguments.Add(new CustomAttributeArgument(ModuleDefinition.TypeSystem.String, assemblyInfoVersion));
            customAttributes.Add(customAttribute);
        }

        LogInfo(string.Format("AssemblyInformationalVersionAttribute processed: {0}", assemblyInfoVersion));
    }

    private string ReplaceVersion3rd(string versionString, int rev)
    {
        Version fake;
        if (!Version.TryParse(versionString, out fake))
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
            throw new WeavingException("The version string must be prefixed with a valid Version. The following string does not:  " + versionString
                + " prefix: " + prefix);
        }
    }

    TypeDefinition GetAttribute(string typeName)
    {
        var msCoreLib = ModuleDefinition.AssemblyResolver.Resolve("mscorlib");
        var msCoreAttribute = msCoreLib.MainModule.Types.FirstOrDefault(x => x.Name == typeName);
        if (msCoreAttribute != null)
        {
            return msCoreAttribute;
        }
        var systemReflection = ModuleDefinition.AssemblyResolver.Resolve("System.Reflection");
        return systemReflection.MainModule.Types.First(x => x.Name == typeName);
    }

    TypeDefinition GetInformationalVersionAttribute()
    {
        return GetAttribute("AssemblyInformationalVersionAttribute");
    }

    TypeDefinition GetFileVersionAttribute()
    {
        return GetAttribute("AssemblyFileVersionAttribute");
    }

    TypeDefinition GetVersionAttribute()
    {
        return GetAttribute("AssemblyVersionAttribute");
    }

    public void AfterWeaving()
    {
        if (!dotSvnDirExists)
        {
            return;
        }

        var verPatchPath = Path.Combine(AddinDirectoryPath, "verpatch.exe");
        var arguments = string.Format("\"{0}\" /pv \"{1}\" /high /va {2}", AssemblyFilePath, assemblyInfoVersion, assemblyVersionReplaced);

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