/***************************************************************
 * 
 *   Copyright 2019 Antti Pohjola
 *
 *  Licensed under the Apache License, Version 2.0 (the "License");
 *  you may not use this file except in compliance with the License.
 *  You may obtain a copy of the License at
 * 
 *        http://www.apache.org/licenses/LICENSE-2.0
 *
 *  Unless required by applicable law or agreed to in writing, software
 *  distributed under the License is distributed on an "AS IS" BASIS,
 *  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *  See the License for the specific language governing permissions and
 *  limitations under the License.
 *  
 ***************************************************************/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace algetiming_streamer
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Attach an event handler for missing assemblies
            AppDomain.CurrentDomain.AssemblyResolve += new ResolveEventHandler(CurrentDomain_AssemblyResolve);

            Application.Run(new TimeStreamerForm());
        }
        #region x86/x64 compatibility

        public static System.Reflection.Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            // Check for if Alge Timy assembly needs to be resolved
            if (args.Name.ToLower().Contains("algetimyusb.dummy"))
            {
                // Detach event handler as it is not needed anymore
                AppDomain.CurrentDomain.AssemblyResolve -= new ResolveEventHandler(CurrentDomain_AssemblyResolve);

                // Construct correct filename depending on the platform
                String filename = "AlgeTimyUsb." + (IsX64Process ? "x64" : "x86") + ".dll";
                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                filename = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(assembly.Location), filename);

                // Check for file exists
                if (System.IO.File.Exists(filename))
                {
                    try
                    {
                        // Try to load assembly
                        var a = System.Reflection.Assembly.LoadFile(filename);
                        return a;
                    }
                    catch (Exception ex)
                    {
                        // Error on loading assembly - Timy Usb will not work and application will crash
                        System.Windows.Forms.MessageBox.Show("Error on loading the existing file '" + System.IO.Path.GetFileName(filename) + "'. This application cannot be executed without it.\nPlease make sure that you have Microsoft Visual C++ 2012 Runtime installed.\n\n" + ex.Message, "Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Warning);

                    }
                }
                else
                {
                    // Correct assembly for platform does not exist - Timy Usb will not work and application will crash
                    System.Windows.Forms.MessageBox.Show("Unable to find " + System.IO.Path.GetFileName(filename) + ". This application cannot be executed without it.", "Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Warning);
                }
                return null;
            }
            return null;
        }

        /// <summary>
        /// Determines whether the process is running as x64 or x86
        /// </summary>
        public static bool IsX64Process
        {
            get { return IntPtr.Size == 8; }
        }

        #endregion

    }
}
