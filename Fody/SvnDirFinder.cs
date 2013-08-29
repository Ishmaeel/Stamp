using System.IO;

public class SvnDirFinder
{

    public static string TreeWalkForSvnDir(string currentDirectory)
    {
        while (true)
        {
            var svnDir = Path.Combine(currentDirectory, @".svn");
            if (Directory.Exists(svnDir))
            {
                return svnDir;
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
}