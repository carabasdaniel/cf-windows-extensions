﻿namespace CloudFoundry.WinDEA.WindowsService
{
    using System;
    using System.ServiceProcess;

    /// <summary>
    /// The Windows Service hosting the DEA.
    /// </summary>
    public partial class DeaWindowsService : ServiceBase
    {
        /// <summary>
        /// The droplet execution agent running in this 
        /// </summary>
        private Agent agent;

        /// <summary>
        /// Initializes a new instance of the <see cref="DeaWindowsService"/> class.
        /// </summary>
        public DeaWindowsService()
        {
            this.InitializeComponent();
        }

        /// <summary>
        /// Starts this instance.
        /// </summary>
        internal void Start()
        {
            try
            {
                this.agent = new Agent();
                this.agent.Run();

            }
            catch (Exception e)
            {
                CloudFoundry.Utilities.Logger.Error(e.ToString());
                Environment.FailFast("Unhandled exception in DeaWindowsService.Start", e);
            }
        }

        /// <summary>
        /// When implemented in a derived class, executes when a Start command is sent to the service by the Service Control Manager (SCM) or when the operating system starts (for a service that starts automatically). Specifies actions to take when the service starts.
        /// </summary>
        /// <param name="args">Data passed by the start command.</param>
        protected override void OnStart(string[] args)
        {
            this.Start();
        }

        /// <summary>
        /// When implemented in a derived class, executes when a Stop command is sent to the service by the Service Control Manager (SCM). Specifies actions to take when a service stops running.
        /// </summary>
        protected override void OnStop()
        {
            this.agent.Shutdown();
        }
    }
}
