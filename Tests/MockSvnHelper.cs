using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

internal class MockSvnHelper : ISvnHelper
{
    public string TreeWalkForSvnDir(string currentDirectory)
    {
        return "dir";
    }

    public VersionInfo GetSvnInfo(string targetFolder)
    {
        return new VersionInfo()
        {
            BranchName = "branchName",
            HasChanges = true,
            Revision = 9999,
        };
    }
}
