//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE.md file in the project root for full license information.
//

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Net;
using Newtonsoft.Json.Linq;
using Azure.AI.Details.Common.CLI.ConsoleGui;
using System.Runtime.InteropServices;

namespace Azure.AI.Details.Common.CLI
{

    public class ScenarioWizardCommand : Command
    {
        internal ScenarioWizardCommand(ICommandValues values)
        {
            _values = values.ReplaceValues();
            _quiet = _values.GetOrDefault("x.quiet", false);
            _verbose = _values.GetOrDefault("x.verbose", true);
        }

        internal bool RunCommand()
        {
            RunScenarioWizardCommand().Wait();
            return _values.GetOrDefault("passed", true);
        }

        private async Task<bool> RunScenarioWizardCommand()
        {
            try
            {
                await DoCommand(_values.GetCommand());
                return _values.GetOrDefault("passed", true);
            }
            catch (ApplicationException)
            {
                Console.WriteLine();
                _values.Reset("passed", "false");
                return false;
            }
        }

        private async Task DoCommand(string command)
        {
            CheckPath();

            switch (command)
            {
                case "wizard": await DoScenario(); break;
            }
        }

        private async Task DoScenario()
        {
            var interactive = _values.GetOrDefault("scenario.wizard.interactive", true);
            await DoScenario(interactive);
        }

        private async Task DoScenario(bool interactive)
        {
            DisplayScenarioBanner();

            Console.Write("\rName: *** Loading choices ***");

            var scenarios = GetScenarios();
            Console.Write("\rName: ");

            var scenario = ListBoxPicker.PickString(scenarios);
            if (scenario == null) return;
            Console.WriteLine($"\rName: {scenario} ");

            var actions = GetActions(scenario);
            var action = (ScenarioAction)null;
            if (actions.Count(x => !string.IsNullOrEmpty(x.Action)) > 0)
            {
                Console.Write($"\rTask: ");

                var index = ListBoxPicker.PickIndexOf(actions.Select(x => x.ActionWithPrefix).ToArray());
                if (index < 0) return;
                action = actions[index];

                Console.WriteLine($"\rTask: {action.Action.Trim()}\n");
            }

            if (scenario.ToLower().Contains("your data") && action.Action.ToLower().Contains("quickstart"))
            {
                ChatWithYourDataScenario(scenario);
            }
            else if (scenario.ToLower().StartsWith("chat") && action.Action.ToLower().Contains("quickstart"))
            {
                SimpleChatScenario(scenario);
            }
            else if (action.InvokeAction != null)
            {
                action.InvokeAction(action);
            }
            else
            {
                ConsoleHelpers.WriteLineError("NOT YET IMPLEMENTED");
            }
        }

        private static async void ChatWithYourDataScenario(string scenario)
        {
            var subscription = AzCliConsoleGui.PickSubscriptionAsync(true).Result;

            ConsoleHelpers.WriteLineWithHighlight($"\n`AI PROJECT`");
            Console.Write("\rProject: *** Loading choices ***");

            var projects = GetProjects();
            Console.Write("\rProject: ");

            var project = ListBoxPicker.PickString(projects);
            Console.WriteLine($"\rProject: {project}");

            if (project == projects[0]) // (Create new)
            {
                project = AskPrompt("Name: ");
                if (string.IsNullOrEmpty(project)) return;

                Console.Write("*** CREATING ***");
                Thread.Sleep(400);
                Console.Write("\r*** CREATED ***  ");
                Console.WriteLine();

                var process = StartShellAiInitProcess($"project --subscription {subscription.Id}");
                if (process.ExitCode == 0)
                {
                    ConsoleHelpers.WriteLineWithHighlight("\n`CONFIG AI SCENARIO DATA`");

                    Console.Write("\rWhere: ");
                    var source = ListBoxPicker.PickString(GetDataLocationChoices());
                    Console.WriteLine($"\rWhere: {source}");

                    if (!source.Contains("files"))
                    {
                        Console.WriteLine();
                        ConsoleHelpers.WriteLineError($"SCENARIO: {scenario} w/ source={source} is not yet implemented.");
                        return;
                    }

                    var path = AskPrompt("Files: ");
                    var fi = new FileInfo(path);
                    var files = Directory.Exists(path)
                        ? Directory.GetFiles(path)
                        : Directory.GetFiles(fi.DirectoryName, fi.Name);
                    Console.WriteLine($"Found: {files.Count()}");

                    ConsoleHelpers.WriteLineWithHighlight("\n`CONFIG AI SCENARIO ADDITIONAL RESOURCES`");

                    var searchName = AskPrompt("Cognitive Search Resource Name: ");
                    var storageName = AskPrompt("Blob Storage Resource Name: ");
                    var indexName = AskPrompt("Search Index Name: ");
                    Console.WriteLine();

                    Console.Write("*** CREATING ***");
                    Thread.Sleep(1500);
                    Console.Write("\r*** CREATED ***  ");
                    Console.WriteLine();

                    StartShellAiChatScenario(scenario).WaitForExit();
                }
            }
            else
            {
                StartShellAiChatScenario(scenario).WaitForExit();
            }

            ConsoleHelpers.WriteLineWithHighlight($"\n`NEXT STEPS: {scenario}`");
            Console.WriteLine("");
            Console.WriteLine("  To chat w/ your data as configured here, try:");
            Console.WriteLine("");
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"      ai chat --interactive --project \"{project}\"");
            Console.ResetColor();
            Console.WriteLine("");
            Console.WriteLine("  To share with others, try:");
            Console.WriteLine("");
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"      ai chat --interactive --project \"{project}\" --zip \"{project}.zip\"");
            Console.ResetColor();
            Console.WriteLine("");
            Console.WriteLine("  To generate code, try:");
            Console.WriteLine("");
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"      ai wizard --scenario \"{scenario}\" --code --language C#");
            Console.ResetColor();
            Console.WriteLine("");
            Console.WriteLine("  To chat using public data, try:");
            Console.WriteLine("");
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("      ai wizard --scenario \"Chat (OpenAI)\" --explore");
            Console.ResetColor();
            Console.WriteLine("");
        }

        private static void SimpleChatScenario(string scenario)
        {
            Process process = StartShellAiInitProcess("openai");
            if (process.ExitCode == 0)
            {
                StartShellAiChatScenario(scenario).WaitForExit();;

                ConsoleHelpers.WriteLineWithHighlight($"\n`NEXT STEPS: {scenario}`");
                Console.WriteLine("");
                Console.WriteLine("  To chat as configured here, try:");
                Console.WriteLine("");
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("      ai chat --interactive");
                Console.ResetColor();
                Console.WriteLine("");
                Console.WriteLine("  To share with others, try:");
                Console.WriteLine("");
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("      ai chat --interactive --zip chat.zip");
                Console.ResetColor();
                Console.WriteLine("");
                Console.WriteLine("  To generate code, try:");
                Console.WriteLine("");
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"      ai wizard --scenario \"{scenario}\" --code --language C#");
                Console.ResetColor();
                Console.WriteLine("");
                Console.WriteLine("  To chat w/ your own data (files, etc.), try:");
                Console.WriteLine("");
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("      ai wizard --scenario \"Chat w/ your data (OpenAI)\"");
                Console.ResetColor();
                Console.WriteLine("");
            }
        }

        private static Process StartShellAiInitProcess(string extraArguments = "")
        {
            var command = "ai";
            var arguments = $"init {extraArguments} --quiet";

            var process = TryCatchHelpers.TryCatchNoThrow<Process>(() => StartShellCommandProcess(command, arguments), null, out Exception processException);
            process?.WaitForExit();

            return process;
        }

        private static Process StartShellAiChatScenario(string scenario)
        {
            ConsoleHelpers.WriteLineWithHighlight($"\r\n`SCENARIO: {scenario}`\n");
            return StartShellAiChatProcess();
        }

        private static Process StartShellAiChatProcess()
        {
            var command = "ai";
            var arguments = "chat --interactive --quiet";

            var process = TryCatchHelpers.TryCatchNoThrow<Process>(() => StartShellCommandProcess(command, arguments), null, out var processException);
            process?.WaitForExit();

            return process;
        }

        private static string[] GetDataLocationChoices()
        {
            return new string[] {
                "Upload files",
                "Azure Blob Storage",
                "Azure Cognitive Search"
            };
        }

        private static string[] GetProjects()
        {
            Thread.Sleep(400);
            return new string[]
            {
                "(Create new)",
                "Copilot-project-1",
                "My-2nd-attempt",
            };
        }

        private static void DisplayScenarioBanner()
        {
            var logo = FileHelpers.FindFileInHelpPath($"help/include.{Program.Name}.wizard.ascii.logo")
                    ?? FileHelpers.FindFileInHelpPath($"help/include.{Program.Name}.ascii.logo");
            var text = !string.IsNullOrEmpty(logo)
                     ? FileHelpers.ReadAllHelpText(logo, Encoding.UTF8) + "\n"
                     : $"`{Program.Name.ToUpper()} SCENARIO`";
            ConsoleHelpers.WriteLineWithHighlight(text);

            ConsoleHelpers.WriteLineWithHighlight("`SCENARIO`");
        }

        private static string[] GetScenarios()
        {
            Thread.Sleep(400);
            return ScenarioActions.Actions
                .Select(x => x.Scenario)
                .Distinct()
                .ToArray();
        }

        private static ScenarioAction[] GetActions(string scenario)
        {
            return ScenarioActions.Actions
                .Where(x => x.Scenario == scenario)
                .ToArray();
        }

        private static Process StartShellCommandProcess(string command, string arguments)
        {
            var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            return  isWindows
                ? StartProcess("cmd", $"/c {command} {arguments}")
                : StartProcess(command,  arguments);
        }

        private static Process StartProcess(string fileName, string arguments)
        {
            var start = new ProcessStartInfo(fileName, arguments);
            start.UseShellExecute = false;
            start.RedirectStandardOutput = false;
            start.RedirectStandardError = false;

            return Process.Start(start);
        }

        private static string AskPrompt(string prompt, string value = null, bool useEditBox = false)
        {
            Console.Write(prompt);

            if (useEditBox)
            {
                var normal = new Colors(ConsoleColor.White, ConsoleColor.Blue);
                var text = EditBoxQuickEdit.Edit(40, 1, normal, value, 128);
                ColorHelpers.ResetColor();
                Console.WriteLine(text);
                return text;
            }

            if (!string.IsNullOrEmpty(value))
            {
                Console.WriteLine(value);
                return value;
            }

            return Console.ReadLine();
        }

        private readonly bool _quiet = false;
        private readonly bool _verbose = false;
    }
}
