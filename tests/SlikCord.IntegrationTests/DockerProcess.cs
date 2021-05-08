using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace Slik.Cord.IntegrationTests
{
    public class DockerProcess
    {
        private async Task PrintOutput(StreamReader reader, Action<string>? customStdOutReader = null)
        {
            do
            {
                string? output = await reader.ReadLineAsync();

                if (!string.IsNullOrWhiteSpace(output))
                {
                    Console.WriteLine(output);
                    customStdOutReader?.Invoke(output);
                }
            } while (!reader.EndOfStream);
        }

        private async Task ExecuteDockerCommandAsync(string arguments, Action<string>? customStdOutReader = null)
        {
            using var process = Process.Start(new ProcessStartInfo
            { 
                CreateNoWindow = true,
                FileName = "docker",
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            }) ?? throw new Exception("Failure starting a process");
                        
            await Task.WhenAll(
                PrintOutput(process.StandardOutput, customStdOutReader),
                PrintOutput(process.StandardError), 
                process.WaitForExitAsync())
                .ConfigureAwait(false);

            if (process.ExitCode != 0)
                throw new Exception($"Process exit code indicates failure: {process.ExitCode}");
        }

        public async Task BuildAsync(string tag, string folder, string dockerFile = "Dockerfile", string buildArgs = "")
        {
            folder = Path.GetFullPath(folder);
            dockerFile = Path.GetFullPath(Path.Combine(folder, dockerFile));
            buildArgs = string.IsNullOrWhiteSpace(buildArgs) ? "" : $"--build-arg {buildArgs}";
            await ExecuteDockerCommandAsync($"build {folder} --tag {tag} --file {dockerFile} {buildArgs}").ConfigureAwait(false);
        }

        public async Task RunAsync(string name, string image, string? ports = null)
        {
            ports = string.IsNullOrWhiteSpace(ports) ? "" : $"--publish {ports}";
            await ExecuteDockerCommandAsync($"run -d --name {name} {ports} {image}").ConfigureAwait(false);
        }

        public async Task StopAsync(string name)
        {
            await ExecuteDockerCommandAsync($"stop {name}").ConfigureAwait(false);
        }

        public async Task RemoveContainerAsync(string name)
        {
            await ExecuteDockerCommandAsync($"rm --force {name}").ConfigureAwait(false);
        }

        public async Task<bool> DoesContainerExistAsync(string name)
        {
            int lines = 0;

            await ExecuteDockerCommandAsync($"ps -a -f \"name={name}\"", s => 
            {
                lines++;
            }).ConfigureAwait(false);

            return lines > 1; // should be at least 2 lines: headers and container itself
        }
    }
}
