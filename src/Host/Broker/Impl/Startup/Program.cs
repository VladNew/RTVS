﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Common.Core;
using Microsoft.Extensions.Configuration;

namespace Microsoft.R.Host.Broker.Startup {
    public class Program {
        static Program() { }

        public static void Main(string[] args) {
            var configBuilder = new ConfigurationBuilder().AddCommandLine(args);
            var configuration = configBuilder.Build();

            string startAs = configuration["start.as"];
            bool isService = !string.IsNullOrWhiteSpace(startAs) && startAs.EqualsIgnoreCase("service");

            CommonStartup.Start(configuration, isService);
        }
    }
}
