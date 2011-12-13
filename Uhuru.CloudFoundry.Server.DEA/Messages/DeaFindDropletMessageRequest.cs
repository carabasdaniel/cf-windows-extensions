﻿// -----------------------------------------------------------------------
// <copyright file="DeaFindDropletMessageRequest.cs" company="Uhuru Software, Inc.">
// Copyright (c) 2011 Uhuru Software, Inc., All Rights Reserved
// </copyright>
// -----------------------------------------------------------------------

namespace Uhuru.CloudFoundry.DEA
{
    using System;
    using System.Collections.Generic;
    using Uhuru.Utilities;

    public class DeaFindDropletMessageRequest : JsonConvertibleObject
    {
        [JsonName("droplet")]
        public int DropletId
        {
            get;
            set;
        }

        [JsonName("version")]
        public string Version
        {
            get;
            set;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly"),
        JsonName("instance_ids")]
        public HashSet<string> InstanceIds
        {
            get;
            set;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly"), 
        JsonName("indices")]
        public HashSet<int> Indexes
        {
            get;
            set;
        }

        // todo: change this conversion mechanism
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly"), 
        JsonName("states")]
        public HashSet<string> StatesInterchangeableFormat
        {
            get 
            { 
                HashSet<string> res = new HashSet<string>();
                foreach (DropletInstanceState state in this.States)
                {
                    res.Add(state.ToString());
                }

                return res;
            }

            set 
            {
                if (value == null)
                {
                    throw new ArgumentNullException("value");
                }

                this.States = new HashSet<DropletInstanceState>();
                foreach (string state in value)
                {
                    // States.Add((DropletInstanceState)Enum.Parse(typeof(DropletInstanceState), state));
                    this.States.Add(JsonConvertibleObject.ObjectToValue<DropletInstanceState>(state));
                }
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public HashSet<DropletInstanceState> States
        {
            get;
            set;
        }

        [JsonName("include_stats")]
        public bool IncludeStates
        {
            get;
            set;
        }
    }
}
