using System;
using System.ServiceModel;
using System.Configuration;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Microsoft.Xrm.Tooling.Connector;
using NLog;
using System.Net;

//NAHLLogger
using NAHL.D365.SQLLogger;

namespace NAHL.D365.TwentyEightDaysCommunication.Console
{
    class TwentyEightDaysComm
    {
        //NLog support
        public static Logger localNLog = LogManager.GetLogger("localLog");
        //NAHL Logger
        public static NAHLLogger nahlLogger = new NAHLLogger("TwentyEightDaysCommunication", "Once every 24 hours", true, true, true, 1);        
        //CRM Org
        public static IOrganizationService _service;

        /// <summary>
        /// IS TESTING
        /// See App.Config for setting.
        /// will allow the user to enter custom values. Set this to FALSE in production
        /// </summary>
        public static bool IsTesting = bool.Parse(ConfigurationManager.AppSettings.Get("isTesting"));
        //custom datetime 
        public static int testDay;
        //custom matter reference 
        public static string testMatter;


        static void Main(string[] args)
        {
            //       TaskFactory taskfactory = new TaskFactory(_service,localNLog,IsTesting, TestingTime);
            try
            {
                localNLog.Info("Testing Status : " + IsTesting);
                System.Console.WriteLine("28 days communication console app started");
                nahlLogger.Info("Start Execution");

                bool isConnectedToSQL = nahlLogger.isConnectedForSQLLogging();
                if (!isConnectedToSQL)
                {
                    localNLog.Warn("Unable to establish SQL Logging");
                }

                #region Testing
                //if testing prompt user for instructions
                if (IsTesting)
                {
                    System.Console.WriteLine("Please enter chase in whole number i.e. 28,34, 56 etc. ");
                    testDay = Int32.Parse(System.Console.ReadLine());

                    System.Console.WriteLine("Please enter matter reference i.e. SHIY000001-01 ");
                    testMatter = System.Console.ReadLine();

                    System.Console.WriteLine("Will run application on Matter:{0} Day:{1}", testMatter, testDay);                    
                    System.Console.WriteLine("press any key to continue.");
                    System.Console.ReadKey();

                    localNLog.Info("Runing application on Matter: " + testMatter);
                }
                #endregion 

                localNLog.Info("Attempting connection to CRM and initialise the Service Client");
                bool isConnected = ConnectToCrm();

                if (isConnected)
                {
                    localNLog.Info("Successful Connection! Lets get started------------------->");
                    nahlLogger.Info("Connection Success");

                    //Get all active case assessments where cfa consent is yes to process
                    QueryExpression query = new QueryExpression("nal_caseassessment");
                    query.ColumnSet = new ColumnSet() { AllColumns = true };
                    query.Criteria = new FilterExpression();
                    query.Criteria.FilterOperator = LogicalOperator.And;
                    query.Criteria.AddCondition("statecode", ConditionOperator.Equal, 0);
                    query.Criteria.AddCondition("nal_cfaconsent", ConditionOperator.Equal, "956660001" );
                    #region Testing
                    if (IsTesting)
                        query.Criteria.AddCondition("nal_mattername", ConditionOperator.Like, "%"+ testMatter + "%"); // for testing only
                    #endregion


                    EntityCollection CaseA = _service.RetrieveMultiple(query);

                    localNLog.Info("Found (" + CaseA.Entities.Count + ") Case assessments to process");
                    nahlLogger.Info("Found (" + CaseA.Entities.Count + ") Case assessments to process");

                    foreach (Entity caseA in CaseA.Entities)
                    {
                        Guid matter=new Guid();
                        Guid claim = new Guid();
                        int chaseDay = 0;

                        if (caseA.Attributes.Contains("nal_matter"))                        
                            matter = ((EntityReference)(caseA.Attributes["nal_matter"])).Id;

                        if (caseA.Attributes.Contains("nal_cnf"))
                            claim = ((EntityReference)(caseA.Attributes["nal_cnf"])).Id;

                        if (caseA.Attributes.Contains("nah_cfaconsentdate")){
                            DateTime cfaDate= (DateTime)caseA.Attributes["nah_cfaconsentdate"];
                            DateTime today = DateTime.Today;
                            chaseDay = (int)(today - cfaDate).TotalDays;
                        }
                        #region Testing
                        if (IsTesting)
                            chaseDay = testDay;  // for testing only
                        #endregion

                        if (chaseDay % 28 == 0 &&
                            chaseDay != 0         )
                        {
                            CaseAfactory caseAfactory = new CaseAfactory(_service, localNLog, caseA.Id);

                            var caseMatter = _service.Retrieve("incident", matter, new ColumnSet(true));
                            var matterRef = caseMatter["title"].ToString();

                            bool caseAprocessed = caseAfactory.processCaseA();
                            if (!caseAprocessed)
                            {
                                localNLog.Warn("Process failed for record  : " + matterRef);
                                nahlLogger.Warn("Process failed for record  : " + matterRef);
                            }
                            else
                            {
                                localNLog.Warn("Process complete for record  : " + matterRef);
                                nahlLogger.Warn("Process complete for record  : " + matterRef);
                            }
                        }
                    }


                }
            }
            catch (Exception ex)
            {
                nahlLogger.updateServiceStatusBoard("Down", "Bad", 0, 1);
                localNLog.Fatal(ex.Message + ". Exiting the Service with fatal errors");
                nahlLogger.Fatal(ex.Message + ". Exiting the (TwentyEightDaysCommunication) Service with fatal errors : Stack : " + ex.StackTrace);
            }
            finally
            {
                if (nahlLogger.getFatalErrorCount() == 0)
                {
                    nahlLogger.updateServiceStatusBoard("Live", "Good", 1, 0);
                }
                nahlLogger.closeSQLConnection();
            }
        }

        private static bool ConnectToCrm()
        {
            bool ret = false;

            //Remember to change this ConnectionString based on the Environment in the App.Config

            var connection = ConfigurationManager.ConnectionStrings["CrmConnection"].ConnectionString;
            CrmServiceClient conn = new CrmServiceClient(connection);

            localNLog.Info("Connected Org Friendly Name : " + conn.ConnectedOrgFriendlyName);
            nahlLogger.Info("Connected Org Friendly Name : " + conn.ConnectedOrgFriendlyName);
            localNLog.Info("CrmConnectOrgUriActual :" + conn.CrmConnectOrgUriActual);
            localNLog.Info("Service Is Ready :  " + conn.IsReady);
            localNLog.Info("Trying to initialise OrganizationServiceProxy");

            try
            {
                if (conn.IsReady)
                {
                    localNLog.Info("Connection is ready : initiating Service Client");
                    ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                    _service = (IOrganizationService)conn.OrganizationWebProxyClient != null ? (IOrganizationService)conn.OrganizationWebProxyClient : (IOrganizationService)conn.OrganizationServiceProxy;

                    if (_service != null)
                    {
                        localNLog.Info("Seeking CRM Version");
                        RetrieveVersionRequest versionRequest = new RetrieveVersionRequest();
                        RetrieveVersionResponse versionResponse = (RetrieveVersionResponse)_service.Execute(versionRequest);
                        System.Console.WriteLine("Microsoft Dynamics CRM Version {0}", versionResponse.Version);
                        localNLog.Info("Found CRM Version {0}", versionResponse.Version);

                        // Test Call
                        Guid WhoAmIid = ((WhoAmIResponse)_service.Execute(new WhoAmIRequest())).UserId;
                        if (WhoAmIid != Guid.Empty)
                        {
                            localNLog.Info("Successful connection to CRM");
                            localNLog.Info("WhoAmI : " + WhoAmIid);
                            nahlLogger.Info("Successful connection to CRM : WhoAmI : " + WhoAmIid);
                            Entity user = _service.Retrieve("systemuser", WhoAmIid, new ColumnSet(true));
                            if (user != null)
                            {
                                localNLog.Info("UserName : " + user.GetAttributeValue<String>("fullname"));
                                localNLog.Info("DomainName : " + user.GetAttributeValue<String>("domainname"));

                                ret = true;
                            }
                            else
                            {
                                localNLog.Fatal("Unable to get user from CRM : WhoAmI request failed");
                                nahlLogger.Fatal("Unable to get user from CRM : WhoAmI request failed");
                            }
                        }
                    }

                }
                else
                {
                    localNLog.Fatal("Last CRM Error : " + conn.LastCrmError);
                    localNLog.Fatal("Last CRM Exception : " + conn.LastCrmException);
                    localNLog.Fatal("Service was not ready for initialisation : IOrganizationService provision failed. Exiting");

                    nahlLogger.Fatal("Last CRM Error : " + conn.LastCrmError + " : Last Exception : " + conn.LastCrmException);
                }

            }
            catch (FaultException<IOrganizationService> ex)
            {
                localNLog.Fatal(ex.Message);
                nahlLogger.Fatal("Fatal OrgnizationFault : " + ex.Message + " : Stack : " + ex.StackTrace);
                throw;
            }
            catch (CommunicationException ex)
            {
                localNLog.Fatal(ex);
                nahlLogger.Fatal("Fatal CommunicationException : " + ex.Message + " : Stack : " + ex.StackTrace);
                throw;
            }
            catch (Exception ex)
            {
                localNLog.Fatal(ex);
                nahlLogger.Fatal("Fatal GeneralException : " + ex.Message + " : Stack : " + ex.StackTrace);
                throw;
            }

            return ret;

        }

    }
}
