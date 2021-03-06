﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.R.Components.Sql.Publish;

namespace Microsoft.VisualStudio.R.Sql.Publish {
    internal sealed class DacPackageServices : IDacPackageServices {
        public IDacPacBuilder GetBuilder() {
            return new DacPacBuilder();
        }

        public IDacPackage Load(string dacpacPath) {
            return new DacPackageImpl(dacpacPath);
        }
    }
}
