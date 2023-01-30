using System.Collections;
using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.Versioning;
using System.Text;

namespace mkpostreview
{
    internal class Program
    {
        [SupportedOSPlatform("windows")]
        static void Main(string[] args)
        {
            if (args.Length != 3)
            {
                OutputUsageAndExit();
            }

            string fromCommit = args[0];
            string toCommit = args[1];
            string exportDir = args[2];
            if (!File.GetAttributes(exportDir).HasFlag(FileAttributes.Directory))
            {
                OutputUsageAndExit();
            }

            var gitInstallDir = Microsoft.Win32.Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\GitForWindows", "InstallPath", null);
            if (!(gitInstallDir is string))
            {
                Console.Error.WriteLine("Git for Windowsがインストールされていません。");
                Environment.Exit(1);
            }

            string gitPath = Path.Combine((string)gitInstallDir, @"bin\git.exe");

            HashSet<string> oldFiles = new HashSet<string>();
            HashSet<string> newFiles = new HashSet<string>();

            string[] modifyFiles = ExecuteWithStdOut(gitPath, $"diff --name-only --diff-filter=M {fromCommit} {toCommit}").Split(new String[] { "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string modifyFile in modifyFiles)
            {
                oldFiles.Add(modifyFile.Trim('"'));
                newFiles.Add(modifyFile.Trim('"'));
            }

            string[] deleteFiles = ExecuteWithStdOut(gitPath, $"diff --name-only --diff-filter=D {fromCommit} {toCommit}").Split(new String[] { "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string deleteFile in deleteFiles)
            {
                oldFiles.Add(deleteFile.Trim('"'));
            }

            string[] addFiles = ExecuteWithStdOut(gitPath, $"diff --name-only --diff-filter=A {fromCommit} {toCommit}").Split(new String[] { "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string addFile in addFiles)
            {
                newFiles.Add(addFile.Trim('"'));
            }

            string oldArchive = $"archive --format=zip --prefix=old/ {fromCommit}";
            string oldZip = Path.Combine(exportDir, "old.zip");
            string command = oldArchive;
            foreach (string oldFile in oldFiles)
            {
                string file = Decode(oldFile);
                string? dir = Path.GetDirectoryName(file.Replace('/', '\\'));
                if (dir != null)
                {
                    Directory.CreateDirectory(Path.Combine(exportDir, "new", dir));
                }
                command += $" \"{file}\"";
                if (command.Length >= 1700)
                {
                    command += $" -o \"{oldZip}\"";
                    Execute(gitPath, command);
                    ZipFile.ExtractToDirectory(oldZip, exportDir, true);
                    File.Delete(oldZip);
                    command = oldArchive;
                }
            }
            if (command != oldArchive)
            {
                command += $" -o \"{oldZip}\"";
                Execute(gitPath, command);
                ZipFile.ExtractToDirectory(oldZip, exportDir, true);
                File.Delete(oldZip);
            }

            string newArchive = $"archive --format=zip --prefix=new/ {toCommit}";
            string newZip = Path.Combine(exportDir, "new.zip");
            command = newArchive;
            foreach (string newFile in newFiles)
            {
                string file = Decode(newFile);
                string? dir = Path.GetDirectoryName(file.Replace('/', '\\'));
                if (dir != null)
                {
                    Directory.CreateDirectory(Path.Combine(exportDir, "old", dir));
                }
                command += $" \"{file}\"";
                if (command.Length >= 1700)
                {
                    command += $" -o \"{newZip}\"";
                    Execute(gitPath, command);
                    ZipFile.ExtractToDirectory(newZip, exportDir, true);
                    File.Delete(newZip);
                    command = newArchive;
                }
            }
            if (command != newArchive)
            {
                command += $" -o \"{newZip}\"";
                Execute(gitPath, command);
                ZipFile.ExtractToDirectory(newZip, exportDir, true);
                File.Delete(newZip);
            }
        }

        private static void OutputUsageAndExit()
        {
            Console.Error.Write("Usage: mkpostreview.exe <FromCommit> <ToCommit> <Export Directory>");
            Environment.Exit(1);
        }

        private static string ExecuteWithStdOut(string command, string arguments)
        {
            var psi = new ProcessStartInfo();
            psi.FileName = command;
            psi.Arguments = arguments;
            psi.CreateNoWindow = true;
            psi.UseShellExecute = false;
            psi.RedirectStandardOutput = true;
            var p = Process.Start(psi);
            if (p == null)
            {
                throw new Exception($"プロセス ({command}) の実行に失敗しました。");
            }
            string output = p.StandardOutput.ReadToEnd();
            p.WaitForExit();
            return output;
        }

        private static void Execute(string command, string arguments)
        {
            var psi = new ProcessStartInfo();
            psi.FileName = command;
            psi.Arguments = arguments;
            psi.CreateNoWindow = true;
            psi.UseShellExecute = false;
            var p = Process.Start(psi);
            if (p == null)
            {
                throw new Exception($"プロセス ({command}) の実行に失敗しました。");
            }
            p.WaitForExit();
        }

        private static string Decode(string str)
        {
            string result = "";
            var vals = new List<Byte>();
            for (int i = 0; i < str.Length; ++i)
            {
                if ((str[i] == '\\') && (i + 3 < str.Length))
                {
                    byte val = (byte)Convert.ToInt32(new String(str[i + 3], 1));
                    val += (byte)(Convert.ToInt32(new String(str[i + 2], 1)) * 8);
                    val += (byte)(Convert.ToInt32(new String(str[i + 1], 1)) * 64);
                    vals.Add(val);
                    i += 3;
                }
                else
                {
                    if (vals.Count > 0)
                    {
                        result += Encoding.UTF8.GetString(vals.ToArray());
                        vals.Clear();
                    }
                    result += str[i];
                }
            }
            if (vals.Count > 0)
            {
                result += Encoding.UTF8.GetString(vals.ToArray());
                vals.Clear();
            }
            return result;
        }
    }
}