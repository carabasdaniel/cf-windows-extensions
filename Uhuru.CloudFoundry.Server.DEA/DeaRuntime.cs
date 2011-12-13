﻿// -----------------------------------------------------------------------
// <copyright file="DeaRuntime.cs" company="Uhuru Software">
// Copyright (c) 2011 Uhuru Software, Inc., All Rights Reserved
// </copyright>
// -----------------------------------------------------------------------

namespace Uhuru.CloudFoundry.DEA
{
    using System.Collections.Generic;
    
    public class DeaRuntime
    {
        /// <summary>
        /// Gets or sets the executable of the runtime.
        /// </summary>
        public string Executable { get; set; }

        /// <summary>
        /// Gets or sets the version of the runtime.
        /// </summary>
        public string Version { get; set; }

        /// <summary>
        /// Gets or sets the version flag.
        /// </summary>
        public string VersionFlag { get; set; }

        /// <summary>
        /// Gets or sets the additional checks for the runtime.
        /// </summary>
        public string AdditionalChecks { get; set; }

        /// <summary>
        /// Gets or sets the debug environment of the runtime. Passed to the appliction instance as needed.
        /// </summary>
        public Dictionary<string, Dictionary<string, string>> DebugEnv { get; set; }

        /// <summary>
        /// Gets or sets the environment variables for the runtime. Passed to the application instance when started.
        /// </summary>
        public Dictionary<string, string> Environment { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this <see cref="DeaRuntime"/> is enabled.
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// Initializes a new instance of the DeaRuntime class.
        /// </summary>
        public DeaRuntime()
        {
            this.DebugEnv = new Dictionary<string, Dictionary<string, string>>();
            this.Environment = new Dictionary<string, string>();
        }
    }
}
