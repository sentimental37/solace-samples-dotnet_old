﻿#region Copyright & License
//
// Solace Systems Messaging API
// Copyright 2008-2016 Solace Systems, Inc.
//
// Permission is hereby granted, free of charge, to any person obtaining a copy of
// this software and associated documentation files (the "Software"), to use and
// copy the Software, and to permit persons to whom the Software is furnished to
// do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// UNLESS STATED ELSEWHERE BETWEEN YOU AND SOLACE SYSTEMS, INC., THE SOFTWARE IS
// PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING
// BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR
// PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS
// BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF
// CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE
// SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//
// http://www.SolaceSystems.com
//
//                        * RRDirectReplier *
// This sample demonstrates a service or a replier that responds to requesters using  
// the Request/Reply message exchange pattern with direct messaging. 
// The service is represented by this sample which computes arithmetic expressions of the
// form <left operand> <operation> <right operand> (e.g. 3 + 9 = 12)
// Request messages are sent from RRDirectRequester.
// 
// This sample will perform the following:
//   Connect a session to the appliance and listen on a request topic passed in as a command
//   line argument. It will process received requests and reply back with a message
//   which has withn its payload a SDT stream with the following fields:
//   (1) bool: A boolean value indicating whether the operation was a success or not.
//   (2) double: Only present when the first field is true. This field contains the result value if 
//               the operation was successful (Infinite and NaN are considered failures)
// 
// Note: see RRDirectRequeter.cs for more details
#endregion

using System;
using System.Collections.Generic;
using System.Text;
using SolaceSystems.Solclient.Messaging;
using System.Threading;
using SolaceSystems.Solclient.Messaging.SDT;

namespace SolaceSystems.Solclient.Examples.Messaging
{

    class RRDirectReplier : SampleApp
    {
        // The topic to send the request message to
        // It represents the topic string which the RRDirectReplier 
        // is listening for requests
        private string requestTopicStr = null;

        // API session used to recieve requests and send replies
        private ISession session = null;

        /// <summary>
        /// Message event callback, this is where we handle message requests
        /// </summary>
        /// <param name="source"></param>
        /// <param name="args"></param>
        private void HandleRequestMessage (Object source, MessageEventArgs args)
        {
            // Received a request message
            IMessage requestMessage = args.Message;
            Console.WriteLine("\nReceived request message, trying to parse it");

            // Parse it
            ISDTContainer sdt_data = SDTUtils.GetContainer(requestMessage);
            if (sdt_data is IStreamContainer)
            {
                IStreamContainer sdt_stream = (IStreamContainer)sdt_data;
                ISDTField operation = sdt_stream.GetNext();
                if (operation != null && operation.Type == SDTFieldType.INT8)
                {
                    ISDTField leftOperand = sdt_stream.GetNext();
                    if (leftOperand != null && leftOperand.Type == SDTFieldType.INT32)
                    {
                        ISDTField rightOperand = sdt_stream.GetNext();
                        if (rightOperand != null && rightOperand.Type == SDTFieldType.INT32)
                        {
                            int enumVal = Int32.Parse(operation.Value.ToString());
                            Operation op = (Operation)enumVal;
                            Console.WriteLine(ARITHMETIC_EXPRESSION, 
                                leftOperand.Value, op,rightOperand.Value,"?");
                            IMessage replyMessage =
                                ProcessArithmeticExpression((short)operation.Value, (Int32)leftOperand.Value, (Int32)rightOperand.Value);
                            try
                            {
                                Console.WriteLine("Sending replyMessage to requester...");
                                session.SendReply(requestMessage, replyMessage);
                                Console.WriteLine("Listening for request messages ... Press any key(except for Ctrl+C) to exit");
                                return;
                            }
                            catch (Exception ex)
                            {
                                PrintException(ex);
                                return;
                            }
                            finally
                            {
                                requestMessage.Dispose();
                                replyMessage.Dispose();
                            }
                        }
                    }
                }
            }
            // If we reach this point, the message format is not expected.
            Console.WriteLine("Failed to parse the request message, here's a message dump:\n{0}", requestMessage.Dump());
            requestMessage.Dispose(); /* A good practice*/
        }

        /// <summary>
        /// Computes the results and stores it in a reply message
        /// </summary>
        /// <param name="operation"></param>
        /// <param name="leftOperand"></param>
        /// <param name="rightOperand"></param>
        /// <returns></returns>
        private IMessage ProcessArithmeticExpression(short operation, Int32 leftOperand, Int32 rightOperand)
        {
            // Create the reply message
            IMessage replyMessage = ContextFactory.Instance.CreateMessage();
            replyMessage.DeliveryMode = MessageDeliveryMode.Direct;
            IStreamContainer stream = SDTUtils.CreateStream(replyMessage, 256);
            double? result = null;
            try
            {   
                switch ((int)operation)
                {
                    case (int)Operation.DIVIDE:
                        result = (double)((double)leftOperand / (double)rightOperand);
                        break;
                    case (int)Operation.MINUS:
                        result = (double)(leftOperand - rightOperand);
                        break;
                    case (int)Operation.PLUS:
                        result = (double)(leftOperand + rightOperand);
                        break;
                    case (int)Operation.TIMES:
                        result = (double)(leftOperand * rightOperand);
                        break;
                    default:
                        result = null;
                        break;
                }
                if (result == null) {
                   stream.AddBool(false);
                } else {
                    double val = result.Value;
                    if (double.IsNaN(val) || double.IsInfinity(val)) {
                        stream.AddBool(false);
                    } else {
                        stream.AddBool(true);
                        stream.AddDouble(val);
                    }
                }

            } catch (Exception) {
                 stream.Rewind();
                 stream.AddBool(false);
            }
            Operation opEnum = (Operation)((int)operation);
            if (Enum.IsDefined(typeof(Operation), (Int32)operation))
            {
                Console.WriteLine(ARITHMETIC_EXPRESSION, leftOperand, opEnum, rightOperand,
                    (result == null ? "operation failed" : result.ToString()));
            }
            else
            {
                Console.WriteLine(ARITHMETIC_EXPRESSION, leftOperand, "UNKNOWN", rightOperand,
                    (result == null ? "operation failed" : result.ToString()));
            }
            return replyMessage;
        }

        /// <summary>
        /// Main entry point to the sample
        /// </summary>
        /// <param name="args"></param>
        public override void SampleCall(string[] args)
        {
            // Parse command line arguments
            ArgParser cmdLineParser = new ArgParser();
            if (!cmdLineParser.Parse(args) || !SampleParseArgs(cmdLineParser))
            {
                // Parse failed.
                PrintUsage(INVALID_ARGUMENTS_ERROR);
                return;
            }

            // Create the API components: starting with the properties 
            ContextProperties contextProps = new ContextProperties();
            SessionProperties sessionProps = new SessionProperties();
            sessionProps.Host = cmdLineParser.Config.IpPort.ip;
            sessionProps.UserName = cmdLineParser.Config.RouterUserVpn.user;
            sessionProps.Password = cmdLineParser.Config.UserPassword;
            sessionProps.SSLValidateCertificate = false;
            sessionProps.ReconnectRetries = 3;
            if (cmdLineParser.Config.RouterUserVpn.vpn != null)
            {
                sessionProps.VPNName = cmdLineParser.Config.RouterUserVpn.vpn;
            }
            if (cmdLineParser.Config.Compression)
            {
                /* Compression is set as a number from 0-9, where 0 means "disable
                   compression", and 9 means max compression. The default is no
                   compression.
                   Selecting a non-zero compression level auto-selects the
                   compressed SMF port on the appliance, as long as no SMF port is
                   explicitly specified. */
                sessionProps.CompressionLevel = 9;
            }

            // Create and connect the API components: create the context and session objects
            IContext context = null;
            session = null;
            try
            {
                // Creating the context
                InitContext(cmdLineParser.LogLevel);
                Console.WriteLine("Creating the context ...");
                context = ContextFactory.Instance.CreateContext(contextProps, null);

                // Creating the session
                Console.WriteLine("Creating the session ...");
                session = context.CreateSession(sessionProps, HandleRequestMessage, SampleUtils.HandleSessionEvent);

                // Connecting the session
                Console.WriteLine("Connecting the session ...");
                if (session.Connect() == ReturnCode.SOLCLIENT_OK)
                {
                    Console.WriteLine("Session successfully connected");
                }
                
                // Add subscription to request topic
                ITopic listenTopic = ContextFactory.Instance.CreateTopic(requestTopicStr);
                Console.WriteLine("Adding subscription to {0}", requestTopicStr);
                if (session.Subscribe(listenTopic, true) == ReturnCode.SOLCLIENT_OK)
                {
                    Console.WriteLine("Listening for request messages ... Press any key(except for Ctrl+C) to exit");
                }
                Console.In.Read();

            }
            catch (Exception ex)
            {
                PrintException(ex);
            }
            finally
            {
                if (session != null)
                {
                    session.Dispose();
                }
                if (context != null)
                {
                    context.Dispose();
                }
                // Must cleanup after. 
                CleanupContext();
            }
        }

        /// <summary>
        /// Operations a client can use when talking to the RRDirectReplier.
        /// </summary>
        private enum Operation
        {
            PLUS = 1, MINUS = 2, TIMES = 3, DIVIDE = 4
        }

        /// <summary>
        /// Short description.
        /// </summary>
        /// <returns></returns>
        public override string ShortDescription()
        {
            return "An API sample that demonstrates a replier using direct messaging";
        }

        /// <summary>
        /// Command line arguments options
        /// </summary>
        /// <param name="extraOptionsForCommonArgs"></param>
        /// <param name="sampleSpecificUsage"></param>
        /// <returns></returns>
        public override bool GetIsUsingCommonArgs(out string extraOptionsForCommonArgs, out string sampleSpecificUsage)
        {
            extraOptionsForCommonArgs = "\t -rt  \t the topic to send the request message to (RRDirectReplier should be sending to this topic)";
            sampleSpecificUsage = null;
            return true;
        }

        /// <summary>
        /// Parse the sample's extra command line arguments.
        /// </summary>
        /// <param name="args"></param>
        private bool SampleParseArgs(ArgParser cmdLineParser)
        {
            string requestTopicArgOption = "-rt";
            try
            {
                requestTopicStr = cmdLineParser.Config.ArgBag[requestTopicArgOption];
            }
            catch (KeyNotFoundException)
            {
                Console.WriteLine(String.Format("Invalid command line argument '{0}' ", requestTopicStr));
                return false;
            }
            return true;
        }

        // format for the arithmetic operation
        private readonly string ARITHMETIC_EXPRESSION = "\t=================================\n\t  {0} {1} {2} = {3}  \t\n\t=================================\n";
    }
}
