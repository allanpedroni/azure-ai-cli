//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE.md file in the project root for full license information.
//

namespace Azure.AI.Details.Common.CLI
{
    public class NamedValueTokenData
    {
        public NamedValueTokenData(string optionName, string fullName, string optionExample, string requiredDisplayName)
        {
            _optionName = optionName;
            _fullName = fullName;
            _optionExample = optionExample;
            _requiredDisplayName = requiredDisplayName;
        }

        public string Demand(INamedValues values, string action, string command)
        {
            return NamedValueTokenDataHelpers.Demand(values, _fullName, _requiredDisplayName, $"{_optionName} {_optionExample}", action, command);
        }

        public string GetOrDefault(INamedValues values, string defaultValue = null)
        {
            return values.GetOrDefault(_fullName, defaultValue);
        }

        public bool GetOrDefault(INamedValues values, bool defaultValue)
        {
            return values.GetOrDefault(_fullName, defaultValue);
        }

        public int GetOrDefault(INamedValues values, int defaultValue)
        {
            return values.GetOrDefault(_fullName, defaultValue);
        }

        public void Set(INamedValues values, string value = null)
        {
            values.Reset(_fullName, value);
        }

        private readonly string _requiredDisplayName;
        private readonly string _optionName;
        private readonly string _optionExample;
        private readonly string _fullName;

    }
}
