using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using Microsoft.Win32;
using System.Diagnostics;
using System.Threading;

namespace UEProjectRefresh
{

    [DataContract]
    partial class UProjectJson
    {
        [DataMember]
        public int FileVersion { get; set; }
        [DataMember]
        public string EngineAssociation { get; set; }
        [DataMember]
        public string Category { get; set; }
        [DataMember]
        public string Description { get; set; }

    }


    class Program
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="uprojectPath"></param>
        /// <returns></returns>
        static async Task<UProjectJson?> LoadUProject(string uprojectPath)
        {
            string json = await File.ReadAllTextAsync(uprojectPath);
            var ms = new MemoryStream(Encoding.UTF8.GetBytes((json)));
            var serializer = new DataContractJsonSerializer(typeof(UProjectJson));
            ms.Seek(0, SeekOrigin.Begin);
            return serializer.ReadObject(ms) as UProjectJson;
        }

        static string? GetUnreanEngineDir( UProjectJson uproject)
        {
            using (var prerKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
            {
                var engineKey = @$"SOFTWARE\EpicGames\Unreal Engine\{uproject.EngineAssociation}";
                using (var unrealEngineKey = prerKey.CreateSubKey(engineKey, RegistryKeyPermissionCheck.ReadSubTree))
                {
                    return unrealEngineKey.GetValue("InstalledDirectory") as string;
                }
            }
        }

        static async void CallUnrealBuildTool(string uprojectPath)
        {
            var uproject = await LoadUProject(uprojectPath);

            if (uproject == null) return;

            var unreanEngineDir = GetUnreanEngineDir(uproject);
            var unrealBuildToolPath = $"{unreanEngineDir}\\Engine\\Binaries\\DotNET\\UnrealBuildTool.exe";

            if( !File.Exists( unrealBuildToolPath ) )
            {
                return;
            }

            var process = new Process();

            using (Process myProcess = new Process())
            using (var ctoken = new CancellationTokenSource())
            {
                myProcess.StartInfo.UseShellExecute = false;
                // You can start any process, HelloWorld is a do-nothing example.
                myProcess.StartInfo.FileName = unrealBuildToolPath;
                myProcess.StartInfo.Arguments = $"-projectfiles -project=\"{uprojectPath}\" -game -rocket -progress";
                myProcess.StartInfo.CreateNoWindow = true;
                myProcess.StartInfo.RedirectStandardError = true;
                myProcess.StartInfo.RedirectStandardOutput = true;
                myProcess.EnableRaisingEvents = true;

                myProcess.OutputDataReceived += (sender, ev) =>
                {
                    Console.WriteLine($"stdout={ev.Data}");
                };
                myProcess.ErrorDataReceived += (sender, ev) =>
                {
                    Console.WriteLine($"stderr={ev.Data}");
                };
                myProcess.Exited += (sender, ev) =>
                {
                    Console.WriteLine($"exited");
                    // process exit call
                    ctoken.Cancel();
                };

                myProcess.Start();
                myProcess.BeginErrorReadLine();
                myProcess.BeginOutputReadLine();

                // unreal build tool waiting
                ctoken.Token.WaitHandle.WaitOne();
            }

        }

        static void Main(string[] args)
        {
            if( args.Length > 0 )
            {
                string filepath = args[0];

                if (!filepath.Contains(".sln"))
                {
                    // error not sln file
                    return;
                }

                string uproject = filepath.Replace(".sln", ".uproject");

                if( !File.Exists( uproject ) )
                {
                    // error not exists uproject file
                    return;
                }

                CallUnrealBuildTool(uproject);
            }


        }
    }
}
