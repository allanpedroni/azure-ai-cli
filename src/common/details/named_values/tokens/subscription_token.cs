//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE.md file in the project root for full license information.
//

namespace Azure.AI.Details.Common.CLI
{
    public class SubscriptionToken
    {
        public static NamedValueTokenData Data() => new NamedValueTokenData(_optionName, _fullName, _optionExample, _requiredDisplayName);
        public static INamedValueTokenParser Parser() => new NamedValueTokenParser(_optionName, _fullName, "01", "1");

        public static string Demand(INamedValues values, string action, string command)
        {
            var subscription = values.Get(_fullName, true);
            if (string.IsNullOrEmpty(subscription) || subscription.Contains("rror"))
            {
                values.AddThrowError(
                    "ERROR:", $"{action}; requires {_requiredDisplayName}.",
                            "",
                      "TRY:", $"{Program.Name} init",
                              $"{Program.Name} config --set subscription {_optionExample}",
                              $"{Program.Name} {command} {_optionName} {_optionExample}",
                            "",
                      "SEE:", $"{Program.Name} help {command}");
            }
            return subscription;
        }

        private const string _requiredDisplayName = "subscription";
        private const string _optionName = "--subscription";
        private const string _optionExample = "SUBSCRIPTION";
        private const string _fullName = "service.subscription";
    }
}
