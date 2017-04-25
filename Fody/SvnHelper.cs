extern alias svn17;
extern alias svn18;

using System.IO;
using StampSvn.Fody;
using System;

public class SvnHelper : ISvnHelper
{
    SvnHelper18 svn18;
    SvnHelper17 svn17;

    public SvnHelper()
    {
        svn17 = new SvnHelper17();
        svn18 = new SvnHelper18();
    }

    public string TreeWalkForSvnDir(string currentDirectory)
    {
        return svn18.TreeWalkForSvnDir(currentDirectory);
    }

    public VersionInfo GetSvnInfo(string targetFolder)
    {
        try
        {
            return svn18.GetSvnInfo(targetFolder);
        }
        catch (Exception)
        {
        }

        // lame.
        return svn17.GetSvnInfo(targetFolder);
    }
}

public class SvnHelper18 : SvnHelper17
{
    public override VersionInfo GetSvnInfo(string targetFolder)
    {
        svn18::SharpSvn.SvnWorkingCopyVersion version;
        using (var client = new svn18::SharpSvn.SvnWorkingCopyClient())
        {
            client.GetVersion(targetFolder, out version);
        }

        svn18::SharpSvn.SvnInfoEventArgs info;
        using (var client = new svn18::SharpSvn.SvnClient())
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
}

public class SvnHelper17 : ISvnHelper
{
    public string TreeWalkForSvnDir(string currentDirectory)
    {
        while (true)
        {
            var svnDir = Path.Combine(currentDirectory, @".svn");
            var svnDir_underscoreTortoiseHack = Path.Combine(currentDirectory, @".svn");

            if (Directory.Exists(svnDir) || Directory.Exists(svnDir_underscoreTortoiseHack))
            {
                return currentDirectory;
            }

            try
            {
                var parent = Directory.GetParent(currentDirectory);
                if (parent == null)
                {
                    break;
                }
                currentDirectory = parent.FullName;
            }
            catch
            {
                // trouble with tree walk.
                return null;
            }
        }
        return null;
    }

    public virtual VersionInfo GetSvnInfo(string targetFolder)
    {
        svn17::SharpSvn.SvnWorkingCopyVersion version;
        using (var client = new svn17::SharpSvn.SvnWorkingCopyClient())
        {
            client.GetVersion(targetFolder, out version);
        }

        svn17::SharpSvn.SvnInfoEventArgs info;
        using (var client = new svn17::SharpSvn.SvnClient())
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
}