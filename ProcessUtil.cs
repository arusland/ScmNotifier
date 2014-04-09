using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Management;

namespace ScmNotifier
{
    internal class ProcessUtil
    {
        #region Methods

        #region Public

        public static void KillProcessTree(Process root)
        {
            if (root != null)
            {
                var list = new List<Process>();
                GetProcessAndChildren(Process.GetProcesses(), root, list, 1);

                foreach (Process p in list)
                {
                    try
                    {
                        p.Kill();
                    }
                    catch (Exception)
                    {
                        // Ignore such errors
                    }
                }
            }
        }

        #endregion

        #region Private

        private static int GetParentProcessId(Process p)
        {
            int parentId = 0;
            try
            {
                ManagementObject mo = new ManagementObject("win32_process.handle='" + p.Id + "'");
                mo.Get();
                parentId = Convert.ToInt32(mo["ParentProcessId"]);
            }
            catch (Exception)
            {
                parentId = 0;
            }
            return parentId;
        }

        private static void GetProcessAndChildren(Process[] plist, Process parent, List<Process> output, int indent)
        {
            foreach (Process p in plist)
            {
                if (GetParentProcessId(p) == parent.Id)
                {
                    GetProcessAndChildren(plist, p, output, indent + 1);
                }
            }
            output.Add(parent);
        }

        #endregion

        #endregion
    }
}
