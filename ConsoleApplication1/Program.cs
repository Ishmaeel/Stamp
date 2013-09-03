using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ConsoleApplication1
{
    class Program
    {
        static void Main(string[] args)
        {
            var r1 = SvnDirFinder.TreeWalkForSvnDir(".");

            var r2 = SvnDirFinder.TreeWalkForSvnDir(@"S:\solvoyo\src\planlm_ui\JobScheduler\JobSchedulerTEMP\bin\Release");

            var r3 = ModuleWeaver.GetSvnInfo(r2);

        }
    }
}
