using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;
using Microsoft.Xrm.Tooling.Connector;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UserBUChangeUtility
{
    class Program
    {
        public static void AssignSecurityRole(Guid guidSystemUserId, Guid guidSecurityRoleId, IOrganizationService crmService)
        {
            // Create new Associate Request object for creating a N:N relationsip between User and Security
            AssociateRequest objAssociateRequest = new AssociateRequest();
            // Create related entity reference object for associating relationship
            // In this case we SystemUser entity reference  
            objAssociateRequest.RelatedEntities = new EntityReferenceCollection();
            objAssociateRequest.RelatedEntities.Add(new EntityReference("systemuser", guidSystemUserId));
            // Create new Relationship object for System User & Security Role entity schema and assigning it 
            // to request relationship property
            objAssociateRequest.Relationship = new Relationship("systemuserroles_association");
            // Create target entity reference object for associating relationship
            objAssociateRequest.Target = new EntityReference("role", guidSecurityRoleId);
            // Passing AssosiateRequest object to Crm Service Execute method for assigning Security Role to User
            crmService.Execute(objAssociateRequest);
        }
        static void Main(string[] args)
        {
            CrmServiceClient crmSvc = new CrmServiceClient(ConfigurationManager.ConnectionStrings["MyCDSServer"].ConnectionString);
            if (crmSvc.IsReady)
            {
                // Replace this with Your BU GUID
                string businessUnitId = "18B07806-D394-EB11-B1AC-0022486EC861";

                // Replace this with your Target BU GUID
                Guid targetBuId = Guid.Parse("80c4ecda-4c5a-ec11-8f8f-002248d4e639");

                // Get List of Users
                string userQuery = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                      <entity name='systemuser'>
                                        <attribute name='fullname' />
                                        <attribute name='businessunitid' />
                                        <attribute name='title' />
                                        <attribute name='address1_telephone1' />
                                        <attribute name='positionid' />
                                        <attribute name='systemuserid' />
                                        <order attribute='fullname' descending='false' />
                                        <filter type='and'>
                                          <condition attribute='isdisabled' operator='eq' value='0' />
                                          <condition attribute='businessunitid' operator='eq' value='{businessUnitId}' />
                                        </filter>
                                      </entity>
                                    </fetch>";
                EntityCollection userEntities = crmSvc.RetrieveMultiple(new FetchExpression(userQuery));
                foreach (var user in userEntities.Entities)
                {
                    List<string> existingSecurityRoleNames = new List<string>();

                    //Get Existing Roles
                    string roleQuery = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='true'>
                                          <entity name='role'>
                                            <attribute name='name' />
                                            <attribute name='businessunitid' />
                                            <attribute name='roleid' />
                                            <order attribute='name' descending='false' />
                                            <link-entity name='systemuserroles' from='roleid' to='roleid' visible='false' intersect='true'>
                                              <link-entity name='systemuser' from='systemuserid' to='systemuserid' alias='ab'>
                                                <filter type='and'>
                                                  <condition attribute='systemuserid' operator='eq' value='{user.Id.ToString()}' />
                                                </filter>
                                              </link-entity>
                                            </link-entity>
                                          </entity>
                                        </fetch>";

                    EntityCollection roleEntities = crmSvc.RetrieveMultiple(new FetchExpression(roleQuery));
                    foreach (var role in roleEntities.Entities)
                    {
                        existingSecurityRoleNames.Add(role.GetAttributeValue<string>("name"));
                    }


                    //Change Business Unit
                    SetBusinessSystemUserRequest request= new SetBusinessSystemUserRequest();

                    request.BusinessId = targetBuId;
                    request.UserId = user.Id;

                    request.ReassignPrincipal= new EntityReference("systemuser", user.Id);

                    SetBusinessSystemUserResponse response =(SetBusinessSystemUserResponse)crmSvc.Execute(request);

                    // upon BU change exisitng role will be removed
                    //Assign Existing Roles associated to New BU
                    foreach (var roleName in existingSecurityRoleNames)
                    {
                        // Get Role ID
                        string securityRoleQuery = $@"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                      <entity name='role'>
                                                        <attribute name='name' />
                                                        <attribute name='businessunitid' />
                                                        <attribute name='roleid' />
                                                        <order attribute='name' descending='false' />
                                                        <filter type='and'>
                                                          <condition attribute='name' operator='eq' value='{roleName}' />
                                                          <condition attribute='businessunitid' operator='eq' value='{targetBuId.ToString()}' />
                                                        </filter>
                                                      </entity>
                                                    </fetch>";
                        Guid roleId = Guid.Empty;
                        EntityCollection securityRoleEntities = crmSvc.RetrieveMultiple(new FetchExpression(securityRoleQuery));
                        if (securityRoleEntities.Entities.Count > 0)
                        {
                            roleId = securityRoleEntities.Entities[0].Id;
                        }

                        // Assign Security Role
                        AssignSecurityRole(user.Id, roleId, crmSvc);
                    }
                }
            }
        }
    }
}
