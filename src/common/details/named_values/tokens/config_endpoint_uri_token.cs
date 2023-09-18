//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE.md file in the project root for full license information.
//

namespace Azure.AI.Details.Common.CLI
{
    public class ConfigEndpointUriToken
    {
        public static NamedValueTokenData Data() => new NamedValueTokenData(_optionName, _fullName, _optionExample, _requiredDisplayName);
        public static INamedValueTokenParser Parser() => new NamedValueTokenParser(_optionName, _fullName, "0010;0001", "1");

        private const string _requiredDisplayName = "endpoint uri";
        private const string _optionName = "--uri";
        private const string _optionExample = "URI";
        private const string _fullName = "service.config.endpoint.uri";
    }
}
