﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.IO;
using Microsoft.Common.Core.IO;
using Microsoft.Common.Core.Logging;
using Microsoft.Common.Core.OS;
using Microsoft.Common.Core.Security;
using Microsoft.Common.Core.Shell;
using Microsoft.Common.Core.Tasks;
using Microsoft.Common.Core.Telemetry;
using Microsoft.Common.Core.Threading;

namespace Microsoft.Common.Core.Services {
    public sealed class CoreServices : ICoreServices {
        public CoreServices(IApplicationConstants appConstants
            , ITelemetryService telemetry
            , ITaskService tasks
            , IProcessServices processServices
            , ILoggingPermissions loggingPermissions
            , IMainThread mainThread
            , ISecurityService security) {

            LoggingPermissions = loggingPermissions;
            Telemetry = telemetry;
            Security = security;
            Tasks = tasks;

            ProcessServices = processServices;
            FileSystem = new FileSystem();
            MainThread = mainThread;

            Log = new Logger(appConstants.ApplicationName, Path.GetTempPath(), LoggingPermissions);
        }

        public CoreServices(ITelemetryService telemetry
            , ILoggingPermissions permissions
            , ISecurityService security
            , ITaskService tasks
            , IMainThread mainThread
            , IActionLog log
            , IFileSystem fs
            , IProcessServices ps) {

            LoggingPermissions = permissions;
            Log = log;

            Telemetry = telemetry;
            Security = security;
            Tasks = tasks;

            ProcessServices = ps;
            FileSystem = fs;
            MainThread = mainThread;
        }

        public IActionLog Log { get; }
        public IFileSystem FileSystem { get; } 
        public ILoggingPermissions LoggingPermissions { get; }
        public IProcessServices ProcessServices { get; }
        public ISecurityService Security { get; }
        public ITelemetryService Telemetry { get; }
        public ITaskService Tasks { get; }
        public IMainThread MainThread { get; }
    }
}
