//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE.md file in the project root for full license information.
//

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Net;
using Newtonsoft.Json.Linq;
using Azure.AI.Details.Common.CLI.ConsoleGui;

namespace Azure.AI.Details.Common.CLI
{
    public class InitCommand : Command
    {
        internal InitCommand(ICommandValues values)
        {
            _values = values.ReplaceValues();
            _quiet = _values.GetOrDefault("x.quiet", false);
            _verbose = _values.GetOrDefault("x.verbose", true);
        }

        internal bool RunCommand()
        {
            RunInitCommand().Wait();
            return _values.GetOrDefault("passed", true);
        }

        private async Task<bool> RunInitCommand()
        {
            try
            {
                await DoCommand(_values.GetCommand());
                return _values.GetOrDefault("passed", true);
            }
            catch (ApplicationException ex)
            {
                Console.WriteLine(ex.Message);
                Console.WriteLine();
                _values.Reset("passed", "false");
                return false;
            }
        }

        private async Task DoCommand(string command)
        {
            DisplayInitServiceBanner();

            CheckPath();

            var interactive = _values.GetOrDefault("init.service.interactive", true);
            switch (command)
            {
                case "init": await DoInitServiceCommand(); break;
                case "init.openai": await DoInitOpenAiCommand(); break;
                case "init.search": await DoInitSearchCommand(); break;
                case "init.resource": await DoInitResourceCommand(); break;
                case "init.project": await DoInitProjectCommand(); break;
            }
        }

        private async Task DoInitServiceCommand()
        {
            StartCommand();

            var interactive = _values.GetOrDefault("init.service.interactive", true);
            await DoInitService(interactive);

            StopCommand();
            DisposeAfterStop();
            DeleteTemporaryFiles();
        }

        private async Task DoInitOpenAiCommand()
        {
            StartCommand();

            var interactive = _values.GetOrDefault("init.service.interactive", true);
            await DoInitServiceParts(interactive, "openai");

            StopCommand();
            DisposeAfterStop();
            DeleteTemporaryFiles();
        }

        private async Task DoInitSearchCommand()
        {
            StartCommand();

            var interactive = _values.GetOrDefault("init.service.interactive", true);
            await DoInitServiceParts(interactive, "openai", "search");

            StopCommand();
            DisposeAfterStop();
            DeleteTemporaryFiles();
        }

        private async Task DoInitResourceCommand()
        {
            StartCommand();

            var interactive = _values.GetOrDefault("init.service.interactive", true);
            await DoInitServiceParts(interactive, "resource");

            StopCommand();
            DisposeAfterStop();
            DeleteTemporaryFiles();
        }

        private async Task DoInitProjectCommand()
        {
            StartCommand();

            var interactive = _values.GetOrDefault("init.service.interactive", true);
            await DoInitServiceParts(interactive, "openai", "search", "resource", "project");

            StopCommand();
            DisposeAfterStop();
            DeleteTemporaryFiles();
        }

        private async Task DoInitService(bool interactive)
        {
            if (!interactive) ThrowInteractiveNotSupportedApplicationException(); // TODO: Add back non-interactive mode support
            await DoInitServiceInteractively();
        }

        private async Task DoInitServiceInteractively()
        {
            Console.Write("Initialize: ");

            var choiceLookup = new Dictionary<string, string>
            {
                ["Azure OpenAI"] = "openai",
                ["Azure OpenAI + Cognitive Search"] = "openai;search",
                ["Azure AI Resource + Project"] = "resource;project",
                ["Azure AI Resource + Project + OpenAI"] = "openai;resource;project",
                ["Azure AI Resource + Project + OpenAI + Cognitive Search"] = "openai;search;resource;project",
            };

            var choices = choiceLookup.Keys.ToArray();

            var picked = ListBoxPicker.PickIndexOf(choices.ToArray(), choices.Count() - 1);
            if (picked < 0)
            {
                Console.WriteLine("\rInitialize: (canceled)");
                return;
            }
    
            var choice = choices[picked];
            Console.WriteLine($"\rInitialize: {choice}");

            await DoInitServiceParts(true, choiceLookup[choice].Split(';', StringSplitOptions.RemoveEmptyEntries));
        }

        private async Task DoInitServiceParts(bool interactive, params string[] operations)
        {
            foreach (var operation in operations)
            {
                var task = operation switch
                {
                    "openai" => DoInitOpenAi(interactive),
                    "search" => DoInitSearch(interactive),
                    "resource" => DoInitHub(interactive),
                    "project" => DoInitProject(interactive),
                    _ => throw new ApplicationException($"WARNING: NOT YET IMPLEMENTED")
                };
                await task;
            }
        }

        private async Task DoInitOpenAi(bool interactive)
        {
            var subscriptionFilter = _values.GetOrDefault("init.service.subscription", "");
            var regionFilter = _values.GetOrDefault("init.service.resource.region.name", "");
            var groupFilter = _values.GetOrDefault("init.service.resource.group.name", "");
            var resourceFilter = _values.GetOrDefault("init.service.cognitiveservices.resource.name", "");
            var kind = _values.GetOrDefault("init.service.cognitiveservices.resource.kind", Program.CognitiveServiceResourceKind);
            var sku = _values.GetOrDefault("init.service.cognitiveservices.resource.sku", Program.CognitiveServiceResourceSku);
            var yes = _values.GetOrDefault("init.service.cognitiveservices.terms.agree", false);

            var subscriptionId = await AzCliConsoleGui.PickSubscriptionIdAsync(interactive, subscriptionFilter);

            ConsoleHelpers.WriteLineWithHighlight($"\n`{Program.SERVICE_RESOURCE_DISPLAY_NAME_ALL_CAPS}`");
            var regionLocation = new AzCli.AccountRegionLocationInfo(); // await AzCliConsoleGui.PickRegionLocationAsync(interactive, regionFilter);
            var resource = await AzCliConsoleGui.PickOrCreateCognitiveResource(interactive, subscriptionId, regionLocation.Name, groupFilter, resourceFilter, kind, sku, yes);
            var region = resource.RegionLocation;
            var endpoint = resource.Endpoint;
            var id = resource.Id;

            ConsoleHelpers.WriteLineWithHighlight($"\n`OPEN AI DEPLOYMENT (CHAT)`");
            var deployment = await AzCliConsoleGui.AiResourceDeploymentPicker.PickOrCreateDeployment(interactive, "Chat", subscriptionId, resource.Group, resource.RegionLocation, resource.Name, null);
            var chatDeploymentName = deployment.Name;

            ConsoleHelpers.WriteLineWithHighlight($"\n`OPEN AI DEPLOYMENT (EMBEDDINGS)`");

            var embeddingsDeployment = await AzCliConsoleGui.AiResourceDeploymentPicker.PickOrCreateDeployment(interactive, "Embeddings", subscriptionId, resource.Group, resource.RegionLocation, resource.Name, null);
            var embeddingsDeploymentName = embeddingsDeployment.Name;

            var keys = await AzCliConsoleGui.LoadCognitiveServicesResourceKeys(subscriptionId, resource);
            var key = keys.Key1;

            ConfigServiceResource(subscriptionId, region, endpoint, chatDeploymentName, embeddingsDeploymentName, key);

            _values.Reset("init.service.subscription", subscriptionId);
            _values.Reset("service.resource.group.name", resource.Group);
            _values.Reset("service.resource.region.name", resource.RegionLocation);
            _values.Reset("service.openai.endpoint", endpoint);
            _values.Reset("service.openai.key", key);
            _values.Reset("service.openai.resource.id", id);
        }

        private async Task DoInitSearch(bool interactive)
        {
            if (!interactive) ThrowInteractiveNotSupportedApplicationException(); // TODO: Add back non-interactive mode support

            var subscription = _values.GetOrDefault("init.service.subscription", "");
            var location = _values.GetOrDefault("service.resource.region.name", "");
            var groupName = _values.GetOrDefault("service.resource.group.name", "");

            var resource = await AzCliConsoleGui.PickOrCreateCognitiveSearchResource(subscription, location, groupName);
            var keys = await AzCliConsoleGui.LoadSearchResourceKeys(subscription, resource);
            ConfigSearchResource(resource.Endpoint, keys.Key1);

            _values.Reset("service.search.endpoint", resource.Endpoint);
            _values.Reset("service.search.key", keys.Key1);
        }

        private async Task DoInitHub(bool interactive)
        {
            if (!interactive) ThrowInteractiveNotSupportedApplicationException(); // TODO: Add back non-interactive mode support

            var subscription = _values.GetOrDefault("init.service.subscription", "");
            if (string.IsNullOrEmpty(subscription))
            {
                subscription = await AzCliConsoleGui.PickSubscriptionIdAsync(interactive);
                _values.Reset("init.service.subscription", subscription);
            }

            var aiHubResource = await AiSdkConsoleGui.PickOrCreateAiHubResource(_values, subscription);
        }

        private async Task DoInitProject(bool interactive)
        {
            if (!interactive) ThrowInteractiveNotSupportedApplicationException(); // TODO: Add back non-interactive mode support

            var subscription = _values.GetOrDefault("init.service.subscription", "");
            var resourceId = _values.GetOrDefault("service.resource.id", null);
            var groupName = _values.GetOrDefault("service.resource.group.name", null);

            var project = AiSdkConsoleGui.PickOrCreateAiHubProject(_values, subscription, resourceId, out var createdProject);

            var projectName = project["name"].Value<string>();
            var projectId = project["id"].Value<string>();

            _values.Reset("service.project.name", projectName);
            _values.Reset("service.project.id", projectId);

            AiSdkConsoleGui.GetOrCreateAiHubProjectConnections(_values, createdProject, subscription, groupName, projectName,
                _values.GetOrDefault("service.openai.endpoint", null),
                _values.GetOrDefault("service.openai.key", null),
                _values.GetOrDefault("service.search.endpoint", null),
                _values.GetOrDefault("service.search.key", null));

            AiSdkConsoleGui.CreatAiHubProjectConfigJsonFile(subscription, groupName, projectName);
        }

        private void DisplayInitServiceBanner()
        {
            if (_quiet) return;

            var logo = FileHelpers.FindFileInHelpPath($"help/include.{Program.Name}.init.ascii.logo");
            if (!string.IsNullOrEmpty(logo))
            {
                var text = FileHelpers.ReadAllHelpText(logo, Encoding.UTF8);
                ConsoleHelpers.WriteLineWithHighlight(text + "\n");
            }
            else
            {
                ConsoleHelpers.WriteLineWithHighlight($"`{Program.Name.ToUpper()} INIT`");
            }
        }

        private static void ConfigServiceResource(string subscriptionId, string region, string endpoint, string chatDeployment, string embeddingsDeployment, string key)
        {
            ConsoleHelpers.WriteLineWithHighlight($"\n`CONFIG {Program.SERVICE_RESOURCE_DISPLAY_NAME_ALL_CAPS}`");
            Console.WriteLine();

            int maxLabelWidth = 0;
            var actions = new List<Action<int>>(new Action<int>[] {
                Program.InitConfigsSubscription ?
                ConfigSetLambda("@subscription", subscriptionId, "Subscription", subscriptionId, ref maxLabelWidth) : null,
                Program.InitConfigsEndpoint ?
                ConfigSetLambda("@chat.endpoint", endpoint, "Endpoint (chat)", endpoint, ref maxLabelWidth) : null,
                ConfigSetLambda("@chat.deployment", chatDeployment, "Deployment (chat)", chatDeployment, ref maxLabelWidth),
                ConfigSetLambda("@chat.key", key, "Key (chat)", key.Substring(0, 4) + "****************************", ref maxLabelWidth),
                Program.InitConfigsEndpoint ?
                ConfigSetLambda("@search.embeddings.endpoint", endpoint, "Endpoint (embeddings)", endpoint, ref maxLabelWidth) : null,
                ConfigSetLambda("@search.embeddings.deployment", embeddingsDeployment, "Deployment (embeddings)", embeddingsDeployment, ref maxLabelWidth),
                ConfigSetLambda("@search.embeddings.key", key, "Key (embeddings)", key.Substring(0, 4) + "****************************", ref maxLabelWidth),
                ConfigSetLambda("@chat.region", region, "Region", region, ref maxLabelWidth),
            });
            actions.ForEach(x => x?.Invoke(maxLabelWidth));
        }

        private static void ConfigSearchResource(string endpoint, string key)
        {
            ConsoleHelpers.WriteLineWithHighlight($"\n`CONFIG COGNITIVE SEARCH RESOURCE`");
            Console.WriteLine();

            int maxLabelWidth = 0;
            var actions = new List<Action<int>>(new Action<int>[]
            {
                ConfigSetLambda("@search.endpoint", endpoint, "Endpoint (search)", endpoint, ref maxLabelWidth),
                ConfigSetLambda("@search.key", key, "Key (search)", key.Substring(0, 4) + "****************************", ref maxLabelWidth),
            });
            actions.ForEach(x => x(maxLabelWidth));
        }

        private static void ConfigSet(string atFile, string setValue, string message)
        {
            Console.Write($"*** SETTING *** {message}");
            ConfigSet(atFile, setValue);
            Console.WriteLine($"\r  *** SET ***   {message}");
        }

        private static Action<int> ConfigSetLambda(string atFile, string setValue, string displayLabel, string displayValue, ref int maxWidth)
        {
            maxWidth = Math.Max(maxWidth, displayLabel.Length);
            return (int labelWidth) =>
            {
                ConfigSet(atFile, setValue, labelWidth, displayLabel, displayValue);
            };
        }

        private static void ConfigSet(string atFile, string setValue, int labelWidth, string displayLabel, string displayValue)
        {
            displayLabel = displayLabel.PadLeft(labelWidth);
            Console.Write($"*** SETTING *** {displayLabel}");
            ConfigSet(atFile, setValue);
            // Thread.Sleep(50);
            Console.WriteLine($"\r  *** SET ***   {displayLabel}: {displayValue}");
        }

        private static void ConfigSet(string atFile, string setValue)
        {
            var setCommandValues = new CommandValues();
            setCommandValues.Add("x.command", "config");
            setCommandValues.Add("x.config.scope.hive", "local");
            setCommandValues.Add("x.config.command.at.file", atFile);
            setCommandValues.Add("x.config.command.set", setValue);
            var fileName = FileHelpers.GetOutputConfigFileName(atFile, setCommandValues);
            FileHelpers.WriteAllText(fileName, setValue, Encoding.UTF8);
        }

        private static void ThrowInteractiveNotSupportedApplicationException()
        {
            throw new ApplicationException("WARNING: Non-interactive mode not supported");
        }

        private void StartCommand()
        {
            CheckPath();
            LogHelpers.EnsureStartLogFile(_values);

            // _display = new DisplayHelper(_values);

            // _output = new OutputHelper(_values);
            // _output.StartOutput();

            _lock = new SpinLock();
            _lock.StartLock();
        }

        private void StopCommand()
        {
            _lock.StopLock(5000);

            // LogHelpers.EnsureStopLogFile(_values);
            // _output.CheckOutput();
            // _output.StopOutput();

            _stopEvent.Set();
        }

        private SpinLock _lock = null;
        private readonly bool _quiet = false;
        private readonly bool _verbose = false;
    }
}
