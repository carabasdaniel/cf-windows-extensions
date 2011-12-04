﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CloudFoundry.Net.Nats;
using Uhuru.Utilities;
using System.IO;
using System.Globalization;

namespace Uhuru.CloudFoundry.ServiceBase
{
    public abstract class NodeBase : ServiceBase
    {
        string node_id;
        string migration_nfs;
        private Dictionary<string, object> orphan_ins_hash;
        private Dictionary<string, object> orphan_binding_hash;

        public override void Start(Options options)
        {
            node_id = options.NodeId;
            migration_nfs = options.MigrationNfs;
            base.Start(options);
        }

        protected override string Flavor()
        {
            return "Node";
        }

        protected override void OnConnectNode()
        {
           Logger.Debug(Strings.ConnectedLogMessage, ServiceDescription());

            NodeNats.Subscribe(String.Format(CultureInfo.InvariantCulture, Strings.NatsSubjectProvision, ServiceName(), node_id), new SubscribeCallback(OnProvision));
            NodeNats.Subscribe(String.Format(CultureInfo.InvariantCulture, Strings.NatsSubjectUnprovision, ServiceName(), node_id), new SubscribeCallback(OnUnprovision));
            NodeNats.Subscribe(String.Format(CultureInfo.InvariantCulture, Strings.NatsSubjectBind, ServiceName(), node_id), new SubscribeCallback(OnBind));
            NodeNats.Subscribe(String.Format(CultureInfo.InvariantCulture, Strings.NatsSubjectRestore, ServiceName(), node_id), new SubscribeCallback(OnRestore));

            NodeNats.Subscribe(String.Format(CultureInfo.InvariantCulture, Strings.NatsSubjectDiscover, ServiceName()), new SubscribeCallback(OnDiscover));

            NodeNats.Subscribe(String.Format(CultureInfo.InvariantCulture, Strings.NatsSubjectDisableInstance, ServiceName(), node_id), new SubscribeCallback(OnDisableInstance));
            NodeNats.Subscribe(String.Format(CultureInfo.InvariantCulture, Strings.NatsSubjectEnableInstance, ServiceName(), node_id), new SubscribeCallback(OnEnableInstance));
            NodeNats.Subscribe(String.Format(CultureInfo.InvariantCulture, Strings.NatsSubjectImportInstance, ServiceName(), node_id), new SubscribeCallback(OnImportInstance));
            NodeNats.Subscribe(String.Format(CultureInfo.InvariantCulture, Strings.NatsSubjectCleanupNfs, ServiceName(), node_id), new SubscribeCallback(OnCleanupNfs));

            NodeNats.Subscribe(String.Format(CultureInfo.InvariantCulture, Strings.NatsSubjectCheckOrphan, ServiceName()), new SubscribeCallback(OnCheckOrphan));
            NodeNats.Subscribe(String.Format(CultureInfo.InvariantCulture, Strings.NatsSubjectPurgeOrphan, ServiceName(), node_id), new SubscribeCallback(OnPurgeOrphan));

            PreSendAnnouncement();
            SendNodeAnnouncement();

            TimerHelper.RecurringCall(30000, delegate()
            {
                SendNodeAnnouncement();
            });
        }

        private void OnProvision(string msg, string reply, string subject)
        {
           Logger.Debug("{0}: Provision request: {1} from {2}", ServiceDescription(), msg, reply);
            ProvisionResponse response = new ProvisionResponse();
            try
            {
                ProvisionRequest provision_req = new ProvisionRequest();
                provision_req.FromJson(msg);

                ProvisionedServicePlanType plan = provision_req.Plan;
                ServiceCredentials credentials = provision_req.Credentials;
                ServiceCredentials credential = Provision(plan, credentials);
                credential.NodeId = node_id;

                response.Credentials = credential;

               Logger.Debug("{0}: Successfully provisioned service for request {1}: {2}",
                    ServiceDescription(), msg, response.ToJson());

                NodeNats.Publish(reply, msg: EncodeSuccess(response));
            }
            catch (Exception ex)
            {
                Logger.Warning(ex.ToString());
                NodeNats.Publish(reply, msg: EncodeFailure(response));
            }
        }

        private void OnUnprovision(string msg, string reply, string subject)
        {
           Logger.Debug("{0}: Unprovision request: {1}.", ServiceDescription(), msg);
           
            SimpleResponse response = new SimpleResponse();
            try
            {
                UnprovisionRequest unprovision_req = new UnprovisionRequest();
                unprovision_req.FromJson(msg);

                string name = unprovision_req.Name;
                ServiceCredentials[] bindings = unprovision_req.Bindings;


                bool result = Unprovision(name, bindings);

                if (result)
                {
                    NodeNats.Publish(reply, msg: EncodeSuccess(response));
                }
                else
                {
                    NodeNats.Publish(reply, msg: EncodeFailure(response));
                }
            }
            catch (Exception ex)
            {
                Logger.Warning(ex.ToString());
                NodeNats.Publish(reply, msg: EncodeFailure(response, ex));
            }
        }

        private void OnBind(string msg, string reply, string subject)
        {
           Logger.Debug(Strings.BindRequestLogMessage, ServiceDescription(), msg, reply);
            BindResponse response = new BindResponse();
            try
            {
                BindRequest bind_message = new BindRequest();
                bind_message.FromJson(msg);
                string name = bind_message.Name;
                Dictionary<string, object> bind_opts = bind_message.BindOptions;
                ServiceCredentials credentials = bind_message.Credentials;
                response.Credentials = Bind(name, bind_opts, credentials);
                NodeNats.Publish(reply, msg: EncodeSuccess(response));
            }
            catch (Exception ex)
            {
                Logger.Warning(ex.ToString());
                NodeNats.Publish(reply, msg: EncodeFailure(response, ex));
            }
        }

        private void OnUnbind(string msg, string reply, string subject)
        {
           Logger.Debug("{0}: Unbind request: {1} from {2}", ServiceDescription(), msg, reply);
            SimpleResponse response = new SimpleResponse();
            try
            {
                UnbindRequest unbind_req = new UnbindRequest();
                unbind_req.FromJson(msg);

                bool result = Unbind(unbind_req.Credentials);

                if (result)
                {
                    NodeNats.Publish(reply, msg: EncodeSuccess(response));
                }
                else
                {
                    NodeNats.Publish(reply, msg: EncodeFailure(response));
                }
            }
            catch (Exception ex)
            {
                Logger.Warning(ex.ToString());
                NodeNats.Publish(reply, msg: EncodeFailure(response, ex));
            }
        }

        private void OnRestore(string msg, string reply, string subject)
        {
           Logger.Debug("{0}: Restore request: {1} from {2}", ServiceDescription(), msg, reply);
            SimpleResponse response = new SimpleResponse();
            try
            {
                RestoreRequest restore_message = new RestoreRequest();
                restore_message.FromJson(msg);
                string instance_id = restore_message.InstanceId;
                string backup_path = restore_message.BackupPath;

                bool result = Restore(instance_id, backup_path);
                if (result)
                {
                    NodeNats.Publish(reply, msg: EncodeSuccess(response));
                }
                else
                {
                    NodeNats.Publish(reply, msg: EncodeFailure(response));
                }
            }
            catch (Exception ex)
            {
                Logger.Warning(ex.ToString());
                NodeNats.Publish(reply, msg: EncodeFailure(response, ex));
            }
        }

        // disable and dump instance
        private void OnDisableInstance(string msg, string reply, string subject)
        {
           Logger.Debug("{0}: Disable instance {1} request from {2}", ServiceDescription(), msg, reply);
            try
            {
                object[] credentials = new object[0];
                credentials = credentials.FromJson(msg);

                ServiceCredentials prov_cred = new ServiceCredentials();
                ServiceCredentials binding_creds = new ServiceCredentials();

                prov_cred.FromJson(credentials[0].ToJson());
                binding_creds.FromJson(credentials[1].ToJson());


                string instance = prov_cred.Name;
                string file_path = GetMigrationFolder(instance);

                Directory.CreateDirectory(file_path);

                bool result = DisableInstance(prov_cred, binding_creds);

                if (result)
                {
                    result = DumpInstance(prov_cred, binding_creds, file_path);
                }
                NodeNats.Publish(reply, msg: result.ToString());
            }
            catch (Exception ex)
            {
                Logger.Warning(ex.ToString());
            }
        }

        // enable instance and send updated credentials back
        private void OnEnableInstance(string msg, string reply, string subject)
        {
           Logger.Debug("{0}: enable instance {1} request from {2}", ServiceDescription(), msg, reply);
            try
            {
                object[] credentials = new object[0];
                credentials = credentials.FromJson(msg);

                ServiceCredentials prov_cred = new ServiceCredentials();
                Dictionary<string, object> binding_creds_hash = new Dictionary<string, object>();

                prov_cred.FromJson(credentials[0].ToJson());
                binding_creds_hash = binding_creds_hash.FromJson(credentials[1].ToJson());

                bool result = EnableInstance(ref prov_cred, ref binding_creds_hash);

                // Update node_id in provision credentials..
                prov_cred.NodeId = node_id;
                credentials[0] = prov_cred.ToDictionary();
                credentials[1] = binding_creds_hash;
                NodeNats.Publish(reply, msg: credentials.ToJson());
            }
            catch (Exception ex)
            {
                Logger.Warning(ex.ToString());
            }
        }

        // Cleanup nfs folder which contains migration data
        private void OnCleanupNfs(string msg, string reply, string subject)
        {
           Logger.Debug(Strings.CleanupNfsLogMessage, ServiceDescription(), msg, reply);
            try
            {
                object[] request = new object[0];
                request = request.FromJson(msg);
                ServiceCredentials prov_cred = new ServiceCredentials();
                ServiceCredentials binding_creds = new ServiceCredentials();
                prov_cred.FromJson(request[0].ToJson());
                binding_creds.FromJson(request[1].ToJson());

                string instance = prov_cred.Name;
                Directory.Delete(GetMigrationFolder(instance), true);

                NodeNats.Publish(reply, msg: "true");
            }
            catch (Exception ex)
            {
                Logger.Warning(ex.ToString());
            }
        }

        private void OnCheckOrphan(string msg, string reply, string subject)
        {
           Logger.Debug(Strings.CheckOrphanLogMessage, ServiceDescription());

            CheckOrphanResponse response = new CheckOrphanResponse();
            try
            {
                CheckOrphanRequest request = new CheckOrphanRequest();
                request.FromJson(msg);
                CheckOrphan(request.Handles);

                response.OrphanInstances = orphan_ins_hash;
                response.OrphanBindings = orphan_binding_hash;
                response.Success = true;
            }
            catch (Exception ex)
            {
                Logger.Warning(Strings.CheckOrphanExceptionLogMessage, ex.ToString());
                response.Success = false;
                response.Error = new Dictionary<string, object>() 
                {
                    {"message", ex.Message},
                    {"stack", ex.StackTrace}
                };
            }
            finally
            {
                NodeNats.Publish(String.Format(CultureInfo.InvariantCulture, Strings.NatsSubjectOrphanResult, ServiceName()), msg: response.ToJson());
            }
        }

        private void CheckOrphan(Handle[] handles)
        {
            if (handles == null)
            {
                throw new ServiceError(ServiceError.NOT_FOUND, "No handles for checking orphan");
            }

            string[] live_ins_list = AllInstancesList();

            Dictionary<string, object> orphan_ins_hash = new Dictionary<string, object>();

            List<string> oi_list = new List<string>();

            foreach (string name in live_ins_list)
            {
                if (!handles.Any(h => h.Credentials.NodeId == node_id && h.ServiceId == name))
                {
                    oi_list.Add(name);
                }
            }

            ServiceCredentials[] live_bind_list = AllBindingsList();
            Dictionary<string, object> orphan_binding_hash = new Dictionary<string, object>();

            List<ServiceCredentials> ob_list = new List<ServiceCredentials>();

            foreach (ServiceCredentials credential in live_bind_list)
            {
                if (!handles.Any(h => h.Credentials.Name == credential.Name && h.Credentials.Username == credential.Username))
                {
                    ob_list.Add(credential);
                }
            }

           Logger.Debug("Orphan Instances: {0};  Orphan Bindings: {1}", oi_list.Count, ob_list.Count);
            orphan_ins_hash[node_id.ToString()] = oi_list;
            orphan_binding_hash[node_id.ToString()] = ob_list;
            this.orphan_ins_hash = orphan_ins_hash;
            this.orphan_binding_hash = orphan_binding_hash;
        }

        private void OnPurgeOrphan(string msg, string reply, string subject)
        {
           Logger.Debug("{0}: Message for purging orphan ", ServiceDescription());
            SimpleResponse response = new SimpleResponse();
            try
            {
                PurgeOrphanRequest request = new PurgeOrphanRequest();
                request.FromJson(msg);

                bool result = PurgeOrphan(request.OrphanInsList, request.OrphanBindingList);
                if (result)
                {
                    NodeNats.Publish(reply, msg: EncodeSuccess(response));
                }
                else
                {
                    NodeNats.Publish(reply, msg: EncodeFailure(response));
                }
            }
            catch (Exception ex)
            {
                Logger.Warning(ex.Message);
                NodeNats.Publish(reply, msg: EncodeFailure(response, ex));
            }
        }

        private bool PurgeOrphan(string[] oi_list, ServiceCredentials[] ob_list)
        {
            bool ret = true;
            ServiceCredentials[] ab_list = AllBindingsList();

            foreach (string ins in oi_list)
            {
                try
                {
                    ServiceCredentials[] bindings = ab_list.Where(b => b.Name == ins).ToArray();
                   Logger.Debug("Unprovision orphan instance {0} and its bindings {1}", ins, String.Join(", ", bindings.Select(binding => binding.ToJson()).ToArray()));

                    ret = ret && Unprovision(ins, bindings);

                    // Remove the OBs that are unbinded by unprovision
                    ob_list = (from ob in ob_list
                               where !bindings.Any(binding => binding.Name == ob.Name)
                               select ob).ToArray();
                }
                catch (Exception ex)
                {
                   Logger.Debug("Error on purge orphan instance {0}: {1}", ins, ex.Message);
                }
            }

            foreach (ServiceCredentials credential in ob_list)
            {
                try
                {
                   Logger.Debug("Unbind orphan binding {0}", credential.ToJson());
                    ret = ret && Unbind(credential);
                }
                catch (Exception ex)
                {
                   Logger.Debug("Error on purge orphan binding {0}: {1}", credential.ToJson(), ex.ToString());
                }
            }
            return ret;
        }

        // Subclass must overwrite this method to enable check orphan instance feature.
        // Otherwise it will not check orphan instance
        // The return value should be a list of instance name(handle["service_id"]).
        private string[] AllInstancesList()
        {
            return new string[0];
        }

        // Subclass must overwrite this method to enable check orphan binding feature.
        // Otherwise it will not check orphan bindings
        // The return value should be a list of binding credentials
        // Binding credential will be the argument for unbind method
        // And it should have at least username & name property for base code
        // to find the orphans
        private ServiceCredentials[] AllBindingsList()
        {
            return new ServiceCredentials[0];
        }

        // Get the tmp folder for migration
        private string GetMigrationFolder(string instance)
        {
            return Path.Combine(migration_nfs, "migration", ServiceName(), instance);
        }

        private void OnImportInstance(string msg, string reply, string subject)
        {
           Logger.Debug("{0}: import instance {1} request from {2}", ServiceDescription(), msg, reply);
            try
            {
                object[] credentials = new object[0];
                credentials = credentials.FromJson(msg);

                ProvisionedServicePlanType plan = (ProvisionedServicePlanType)Enum.Parse(typeof(ProvisionedServicePlanType), credentials[0].ToValue<string>());

                ServiceCredentials prov_cred = new ServiceCredentials();
                ServiceCredentials binding_creds = new ServiceCredentials();

                prov_cred.FromJson(credentials[1].ToJson());
                binding_creds.FromJson(credentials[2].ToJson());

                string instance = prov_cred.Name;
                string file_path = GetMigrationFolder(instance);

                bool result = ImportInstance(prov_cred, binding_creds, file_path, plan);
                NodeNats.Publish(reply, msg: result.ToString());
            }
            catch (Exception ex)
            {
                Logger.Warning(ex.ToString());
            }
        }

        private void OnDiscover(string msg, string reply, string subject)
        {
            SendNodeAnnouncement(reply);
        }

        private void PreSendAnnouncement()
        {
        }

        private void SendNodeAnnouncement(string reply = null)
        {
            try
            {
                if (!NodeReady())
                {
                   Logger.Debug("{0}: Not ready to send announcement", ServiceDescription());
                    return;
                }

               Logger.Debug("{0}: Sending announcement for {1}", ServiceDescription(), reply != null ? reply : "everyone");

                Announcement a = GetAnnouncement();
                a.Id = node_id;
                NodeNats.Publish(reply != null ? reply : String.Format("{0}.announce", ServiceName()), msg: a.ToJson());
            }
            catch (Exception ex)
            {
                Logger.Warning(ex.ToString());
            }
        }

        private bool NodeReady()
        {
            // Service Node subclasses can override this method if they depend
            // on some external service in order to operate; for example, MySQL
            // and Postgresql require a connection to the underlying server.
            return true;
        }

        protected override Dictionary<string, object> VarzDetails()
        {
            // Service Node subclasses may want to override this method to
            // provide service specific data beyond what is returned by their
            // "announcement" method.
            return GetAnnouncement().ToDictionary();
        }

        protected override Dictionary<string, string> HealthzDetails()
        {
            // Service Node subclasses may want to override this method to
            // provide service specific data
            return new Dictionary<string, string>() 
            {
                {"self", "ok"}
            };
        }

        // Helper
        private string EncodeSuccess(IWithSuccessStatus response)
        {
            response.Success = true;
            return response.ToJson();
        }

        private string EncodeFailure(IWithSuccessStatus response, Exception error = null)
        {
            response.Success = false;
            if (error == null || !(error is ServiceError))
            {
              error = new ServiceError(ServiceError.INTERNAL_ERROR);
            }
            response.Error = ((ServiceError)error).ToDictionary();
            return response.ToJson();
        }

        // Service Node subclasses must implement the following methods

        // provision(plan) --> {name, host, port, user, password}
        protected abstract ServiceCredentials Provision(ProvisionedServicePlanType plan, ServiceCredentials credentials);

        // unprovision(name) --> void
        protected abstract bool Unprovision(string name, ServiceCredentials[] bindings);

        // bind(name, bind_opts) --> {host, port, login, secret}
        protected abstract ServiceCredentials Bind(string name, Dictionary<string, object> bindOptions, ServiceCredentials credentials);

        // unbind(credentials)  --> void
        protected abstract bool Unbind(ServiceCredentials credentials);

        // announcement() --> { any service-specific announcement details }
        protected abstract Announcement GetAnnouncement();

        // <action>_instance(prov_credential, binding_credentials)  -->  true for success and nil for fail
        protected abstract bool DisableInstance(ServiceCredentials provisionedCredential, ServiceCredentials bindingCredentials);
        protected abstract bool DumpInstance(ServiceCredentials provisionedCredential, ServiceCredentials bindingCredentials, string filePath);
        protected abstract bool ImportInstance(ServiceCredentials provisionedCredential, ServiceCredentials bindingCredentials, string filePath, ProvisionedServicePlanType plan);
        protected abstract bool EnableInstance(ref ServiceCredentials provisionedCredential, ref Dictionary<string, object> bindingCredentialsHash);
        protected abstract bool Restore(string instanceId, string backupPath);

    }
}