using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

public interface ISvnHelper
{
    string TreeWalkForSvnDir(string currentDirectory);
    VersionInfo GetSvnInfo(string targetFolder);
}
