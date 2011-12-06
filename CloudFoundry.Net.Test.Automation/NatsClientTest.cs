﻿// -----------------------------------------------------------------------
// <copyright file="NatsClientTest.cs" company="Uhuru Software">
// Copyright (c) 2011 Uhuru Software, Inc., All Rights Reserved
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Configuration;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Uhuru.NatsClient;

namespace CloudFoundry.Net.Test.Automation
{
    [TestClass]
    public class NatsClientTest
    {
        Uri natsEndpoint;
        
        [ClassInitialize]
        public void TestFixtureSetup()
        {
            natsEndpoint = new Uri(ConfigurationManager.AppSettings["nats"]);
        }

        [TestMethod, Description("should perform basic block start and stop")]
        public void Test1()
        {
            using (Reactor natsClient = new Reactor())
            {
                natsClient.Start(natsEndpoint);
                natsClient.Stop();
            }
        }

        [TestMethod, Description("should signal connected state")]
        public void Test2()
        {
            using (Reactor natsClient = new Reactor())
            {
                natsClient.Start(natsEndpoint);
                Assert.IsTrue(natsClient.Status == ConnectionStatus.Open);
                natsClient.Stop();
            }
        }

        [TestMethod, Description("should be able to reconnect")]
        public void Test3()
        {
            Reactor natsClient;
            using (natsClient = new Reactor())
            {
                natsClient.Start(natsEndpoint);
                Assert.IsTrue(natsClient.Status == ConnectionStatus.Open);
                natsClient.Stop();
            }

            using(natsClient = new Reactor())
            {
                natsClient.Start(natsEndpoint);
                Assert.IsTrue(natsClient.Status == ConnectionStatus.Open);
                natsClient.Stop();
            }
        }

        [TestMethod, Description("should raise NATS::ServerError on error replies from NATSD")]
        public void Test4() 
        {
            bool errorThrown = false;
            AutoResetEvent resetEvent = new AutoResetEvent(false);

            using (Reactor natsClient = new Reactor())
            {
                natsClient.Pedantic = true;
                natsClient.OnError += new EventHandler<ReactorErrorEventArgs>(delegate(object sender, ReactorErrorEventArgs args)
                    {
                        errorThrown = true;
                        resetEvent.Set();
                    });
                natsClient.Start(natsEndpoint);
                natsClient.Unsubscribe(10000);
                natsClient.Publish("done");
                resetEvent.WaitOne(10000);
                natsClient.Stop();
            }
            Assert.IsTrue(errorThrown);
        }

        [TestMethod, Description("should do publish without payload and with opt_reply without error")]
        public void Test5() 
        {
            bool errorThrown = false;
            AutoResetEvent resetEvent = new AutoResetEvent(false);

            using (Reactor natsClient = new Reactor())
            {
                natsClient.OnError += new EventHandler<ReactorErrorEventArgs>(delegate(object sender, ReactorErrorEventArgs args)
                    {
                        errorThrown = true;
                        resetEvent.Set();
                    });

                natsClient.Start(natsEndpoint);

                natsClient.Publish("foo");
                natsClient.Publish("foo", null, "hello");
                natsClient.Publish("foo", null, "hello", "reply");

                resetEvent.WaitOne(5000);

                natsClient.Stop();
            }
            Assert.IsFalse(errorThrown);
        }

        [TestMethod, Description("should not complain when publishing to nil")]
        public void Test6() 
        {
            bool errorThrown = false;
            AutoResetEvent resetEvent = new AutoResetEvent(false);

            using (Reactor natsClient = new Reactor())
            {
                natsClient.OnError += new EventHandler<ReactorErrorEventArgs>(delegate(object sender, ReactorErrorEventArgs args)
                {
                    errorThrown = true;
                    resetEvent.Set();
                });

                natsClient.Start(natsEndpoint);

                //TODO: vladi: check to see if nil in Ruby evaluates to boolean true
                natsClient.Publish(null);
                natsClient.Publish(null, null, "hello");

                resetEvent.WaitOne(5000);

                natsClient.Stop();
            }
            Assert.IsFalse(errorThrown);
        }

        [TestMethod, Description("should receive a sid when doing a subscribe")]
        public void Test7() 
        {
            using (Reactor natsClient = new Reactor())
            {
                natsClient.Start(natsEndpoint);
                int mySid = natsClient.Subscribe("foo");
                natsClient.Stop();
                Assert.IsTrue(0 < mySid);
            }
        }

        [TestMethod, Description("should receive a sid when doing a request")]
        public void Test8()
        {
            using (Reactor natsClient = new Reactor())
            {
                natsClient.Start(natsEndpoint);
                int mySid = natsClient.Request("foo");
                natsClient.Stop();
                Assert.IsTrue(0 < mySid);
            }
        }

        [TestMethod, Description("should receive a message that it has a subscription to")]
        public void Test9() 
        {
            bool errorThrown = false;
            string receivedMessage = "";
            AutoResetEvent resetEvent = new AutoResetEvent(false);

            using (Reactor natsClient = new Reactor())
            {
                natsClient.OnError += new EventHandler<ReactorErrorEventArgs>(delegate(object sender, ReactorErrorEventArgs args)
                {
                    errorThrown = true;
                    resetEvent.Set();
                });

                natsClient.Start(natsEndpoint);

                natsClient.Subscribe("foo", delegate(string msg, string reply, string subject)
                {
                    receivedMessage = msg;
                    resetEvent.Set();
                });

                natsClient.Publish("foo", null, "xxx");
                resetEvent.WaitOne(5000);
                natsClient.Stop();
            }
            Assert.IsFalse(errorThrown);
            Assert.AreEqual(receivedMessage, "xxx");
        }

        [TestMethod, Description("should receive empty message")]
        public void TestEmptyMessageReceive()
        {
            bool errorThrown = false;
            string receivedMessage = "xxx";
            AutoResetEvent resetEvent = new AutoResetEvent(false);

            using (Reactor natsClient = new Reactor())
            {
                natsClient.OnError += new EventHandler<ReactorErrorEventArgs>(delegate(object sender, ReactorErrorEventArgs args)
                {
                    errorThrown = true;
                    resetEvent.Set();
                });

                natsClient.Start(natsEndpoint);

                natsClient.Subscribe("foo", delegate(string msg, string reply, string subject)
                {
                    receivedMessage = msg;
                    resetEvent.Set();
                });

                natsClient.Publish("foo", null, "");
                resetEvent.WaitOne(5000);
                natsClient.Stop();
            }
            Assert.IsFalse(errorThrown);
            Assert.AreEqual(receivedMessage, "");
        }

        [TestMethod, Description("should receive a message that it has a wildcard subscription to")]
        public void Test10() 
        {
            string errorThrown = null;
            string receivedMessage = "";
            AutoResetEvent resetEvent = new AutoResetEvent(false);

            using (Reactor natsClient = new Reactor())
            {
                natsClient.OnError += new EventHandler<ReactorErrorEventArgs>(delegate(object sender, ReactorErrorEventArgs args)
                {
                    errorThrown = args.Message == null ? String.Empty : args.Message;
                    resetEvent.Set();
                });

                natsClient.Start(natsEndpoint);

                //TODO: vladi: if not testing agains an empty server, this subscription may fail
                natsClient.Subscribe("*", delegate(string msg, string reply, string subject)
                {
                    receivedMessage = msg;
                    resetEvent.Set();
                });

                natsClient.Publish("foo", null, "xxx");
                resetEvent.WaitOne(5000);
                natsClient.Stop();
            }
            Assert.IsTrue(errorThrown == null, errorThrown);
            Assert.AreEqual(receivedMessage, "xxx");
        }

        [TestMethod, Description("should not receive a message that it has unsubscribed from")]
        public void Test11() 
        {
            bool errorThrown = false;
            int receivedCount = 0;
            int sid = 0;

            AutoResetEvent resetEvent = new AutoResetEvent(false);

            Reactor natsClient = new Reactor();
            natsClient.OnError += new EventHandler<ReactorErrorEventArgs>(delegate(object sender, ReactorErrorEventArgs args)
            {
                errorThrown = true;
                resetEvent.Set();
            });

            natsClient.Start(natsEndpoint);

            sid = natsClient.Subscribe("foo", delegate(string msg, string reply, string subject)
            {
                receivedCount++;
                if (receivedCount == 2)
                {
                    natsClient.Unsubscribe(sid);
                }
                resetEvent.Set();
            });

            natsClient.Publish("foo", null, "xxx");
            natsClient.Publish("foo", null, "xxx");
            natsClient.Publish("foo", null, "xxx");
            resetEvent.WaitOne(5000);
            natsClient.Stop();

            Assert.IsFalse(errorThrown);
            Assert.AreEqual(2, receivedCount);
        }

        [TestMethod, Description("should receive a response from a request")]
        public void Test12() 
        {
            bool errorThrown = false;
            string receivedMessage = "";
            string receivedReply = "";

            AutoResetEvent resetEvent = new AutoResetEvent(false);

            Reactor natsClient = new Reactor();
            natsClient.OnError += new EventHandler<ReactorErrorEventArgs>(delegate(object sender, ReactorErrorEventArgs args)
            {
                errorThrown = true;
                resetEvent.Set();
            });

            natsClient.Start(natsEndpoint);

            natsClient.Subscribe("need_help", delegate(string msg, string reply, string subject)
            {
                receivedMessage = msg;
                natsClient.Publish(reply, null, "help");
            });

            //TODO: vladi: this doesn't work if no message is sent.
            natsClient.Request("need_help", null, delegate(string msg, string reply, string subject)
            {
                receivedReply = msg;
                resetEvent.Set();
            }, "yyy");

            resetEvent.WaitOne(5000);
            natsClient.Stop();

            Assert.IsFalse(errorThrown);
            Assert.AreEqual(receivedMessage, "yyy");
            Assert.AreEqual(receivedReply, "help");
        }

        [TestMethod, Description("should return inside closure on publish when server received msg")]
        public void Test14() 
        {
            bool errorThrown = false;
            bool done = false;

            AutoResetEvent resetEvent = new AutoResetEvent(false);

            using (Reactor natsClient = new Reactor())
            {
                natsClient.OnError += new EventHandler<ReactorErrorEventArgs>(delegate(object sender, ReactorErrorEventArgs args)
                {
                    errorThrown = true;
                    resetEvent.Set();
                });

                natsClient.Start(natsEndpoint);


                natsClient.Publish("foo", delegate()
                {
                    done = true;
                    resetEvent.Set();
                });

                resetEvent.WaitOne(5000);
                natsClient.Stop();
            }
            Assert.IsFalse(errorThrown);
            Assert.IsTrue(done);
        }

        [TestMethod, Description("should return inside closure in ordered fashion when server received msg")]
        public void Test15() 
        {

            bool errorThrown = false;
            List<int> expected = new List<int>();
            List<int> response = new List<int>();

            AutoResetEvent resetEvent = new AutoResetEvent(false);

            using (Reactor natsClient = new Reactor())
            {
                natsClient.OnError += new EventHandler<ReactorErrorEventArgs>(delegate(object sender, ReactorErrorEventArgs args)
                {
                    errorThrown = true;
                    resetEvent.Set();
                });

                natsClient.Start(natsEndpoint);

                SimpleCallback[] delegates = new SimpleCallback[] {
                delegate() { response.Add(0); },
                delegate() { response.Add(1); },
                delegate() { response.Add(2); },
                delegate() { response.Add(3); },
                delegate() { response.Add(4); },
                delegate() { response.Add(5); },
                delegate() { response.Add(6); },
                delegate() { response.Add(7); },
                delegate() { response.Add(8); },
                delegate() { response.Add(9); },
                delegate() { response.Add(10); },
                delegate() { response.Add(11); },
                delegate() { response.Add(12); },
                delegate() { response.Add(13); },
                delegate() { response.Add(14); }
                };

                for (int i = 0; i < 15; i++)
                {
                    expected.Add(i);
                    natsClient.Publish("foo", delegates[i]);
                }

                natsClient.Publish("foo", delegate()
                {
                    resetEvent.Set();
                });

                resetEvent.WaitOne(5000);
                natsClient.Stop();
            }
            Assert.IsFalse(errorThrown);

            for (int i = 0; i < 15; i++)
            {
                Assert.AreEqual(expected[i], response[i]);
            }
        }

        [TestMethod, Description("should be able to start and use a new connection inside of start block")]
        public void Test16() 
        {
            bool errorThrown = false;
            string receivedMessage = "";
            AutoResetEvent resetEvent = new AutoResetEvent(false);

            using (Reactor natsClient = new Reactor())
            {
                natsClient.OnError += new EventHandler<ReactorErrorEventArgs>(delegate(object sender, ReactorErrorEventArgs args)
                {
                    errorThrown = true;
                    resetEvent.Set();
                });

                natsClient.Start(natsEndpoint);

                natsClient.Subscribe("foo", delegate(string msg, string reply, string subject)
                {
                    receivedMessage = msg;
                    resetEvent.Set();
                });

                using (Reactor natsClient2 = new Reactor())
                {
                    natsClient2.Start(natsEndpoint);
                    natsClient2.Publish("foo", null, "xxx");

                    resetEvent.WaitOne(5000);

                    natsClient.Stop();
                    natsClient2.Stop();
                }
            }
            Assert.IsFalse(errorThrown);
            Assert.AreEqual(receivedMessage, "xxx");
        }

        [TestMethod, Description("should allow proper request/reply across multiple connections")]
        public void Test17() 
        {
            bool errorThrown = false;
            string receivedMessage = "";
            string receivedReply = "";

            AutoResetEvent resetEvent = new AutoResetEvent(false);

            using (Reactor natsClient = new Reactor())
            {
                natsClient.OnError += new EventHandler<ReactorErrorEventArgs>(delegate(object sender, ReactorErrorEventArgs args)
                {
                    errorThrown = true;
                    resetEvent.Set();
                });

                natsClient.Start(natsEndpoint);

                natsClient.Subscribe("need_help", delegate(string msg, string reply, string subject)
                {
                    receivedMessage = msg;
                    natsClient.Publish(reply, null, "help");
                });

                using (Reactor natsClient2 = new Reactor())
                {
                    natsClient2.Start(natsEndpoint);
                    //TODO: vladi: this doesn't work if no message is sent.
                    natsClient2.Request("need_help", null, delegate(string msg, string reply, string subject)
                    {
                        receivedReply = msg;
                        resetEvent.Set();
                    }, "yyy");

                    resetEvent.WaitOne(5000);

                    natsClient.Stop();
                    natsClient2.Stop();
                }
            }
            Assert.IsFalse(errorThrown);
            Assert.AreEqual(receivedMessage, "yyy");
            Assert.AreEqual(receivedReply, "help");
        }

        [TestMethod, Description("should allow proper unsubscribe from within blocks")]
        public void Test22() 
        {
            bool errorThrown = false;
            int receivedMessageCount = 0;
            int sid = 0;
            AutoResetEvent resetEvent = new AutoResetEvent(false);

            using (Reactor natsClient = new Reactor())
            {
                natsClient.OnError += new EventHandler<ReactorErrorEventArgs>(delegate(object sender, ReactorErrorEventArgs args)
                {
                    errorThrown = true;
                    resetEvent.Set();
                });

                natsClient.Start(natsEndpoint);

                sid = natsClient.Subscribe("foo", delegate(string msg, string reply, string subject)
                {
                    receivedMessageCount++;
                    natsClient.Unsubscribe(sid);
                });

                natsClient.Publish("foo", null, "xxx");
                natsClient.Publish("foo", delegate()
                {
                    resetEvent.Set();
                }, "xxx");

                resetEvent.WaitOne(5000);
                natsClient.Stop();
            }
            Assert.IsFalse(errorThrown);
            Assert.AreEqual(1, receivedMessageCount);
        }

        [TestMethod, Description("should not call error handler for double unsubscribe unless in pedantic mode")]
        public void Test23() 
        {
            bool errorThrown = false;
            AutoResetEvent resetEvent = new AutoResetEvent(false);

            using (Reactor natsClient = new Reactor())
            {
                natsClient.OnError += new EventHandler<ReactorErrorEventArgs>(delegate(object sender, ReactorErrorEventArgs args)
                {
                    errorThrown = true;
                    resetEvent.Set();
                });
                natsClient.Start(natsEndpoint);

                int sid = natsClient.Subscribe("foo");
                natsClient.Unsubscribe(sid);
                natsClient.Unsubscribe(sid);

                resetEvent.WaitOne(5000);
                natsClient.Stop();
            }
            Assert.IsFalse(errorThrown);
        }

        [TestMethod, Description("should call error handler for double unsubscribe if in pedantic mode")]
        public void Test24() 
        {
            bool errorThrown = false;
            AutoResetEvent resetEvent = new AutoResetEvent(false);

            using (Reactor natsClient = new Reactor())
            {
                natsClient.OnError += new EventHandler<ReactorErrorEventArgs>(delegate(object sender, ReactorErrorEventArgs args)
                {
                    errorThrown = true;
                    resetEvent.Set();
                });
                natsClient.Pedantic = true;
                natsClient.Start(natsEndpoint);

                int sid = natsClient.Subscribe("foo");
                natsClient.Unsubscribe(sid);
                natsClient.Unsubscribe(sid);

                resetEvent.WaitOne(5000);
                natsClient.Stop();
            }
            Assert.IsTrue(errorThrown);
        }
    }
}