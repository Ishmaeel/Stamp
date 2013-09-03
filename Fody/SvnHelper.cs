using SharpSvn;
using System.IO;
using StampSvn.Fody;

public class SvnHelper : ISvnHelper
{
    public string TreeWalkForSvnDir(string currentDirectory)
    {
        while (true)
        {
            var svnDir = Path.Combine(currentDirectory, @".svn");
            if (Directory.Exists(svnDir))
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

    public VersionInfo GetSvnInfo(string targetFolder)
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
}