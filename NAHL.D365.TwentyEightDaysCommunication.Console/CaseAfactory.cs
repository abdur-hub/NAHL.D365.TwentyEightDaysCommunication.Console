using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Crm.Sdk.Messages;
using NLog;

namespace NAHL.D365.TwentyEightDaysCommunication.Console
{
    class CaseAfactory
    {
        IOrganizationService _service;
        Logger _commLog;
        Guid _caseA;
        Guid _workflowId;


        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="service"></param>
        /// <param name="commLog"></param>
        /// <param name="caseA"></param>
        public CaseAfactory(IOrganizationService service, Logger commLog, Guid caseA)
        {
            _caseA = caseA;
            _service = service;
            _commLog = commLog;
            _workflowId = new Guid("96EF9FF5-1785-4974-8048-9CBD8BE030AF");
        }

        /// <summary>
        /// Run the Process 'NAL 28 days communication' against the Case Assessment record
        /// </summary>
        /// <returns></returns>
        public bool processCaseA()
        {
            bool ret = true;

            try
            {
                ExecuteWorkflowRequest request = new ExecuteWorkflowRequest()
                {
                    WorkflowId = _workflowId,
                    EntityId = _caseA
                };

                OrganizationResponse response = _service.Execute(request);
                _commLog.Info("Workflow response: " + response.ToString());
            }
            catch (Exception ex)
            {
                _commLog.Info("Error encountered at processCaseA() routine: " + ex.Message.ToString());
                ret = false;
            }
            return ret;
        }

    }
}
