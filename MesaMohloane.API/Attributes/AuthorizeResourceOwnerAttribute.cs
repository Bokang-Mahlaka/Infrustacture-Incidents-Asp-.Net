using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Security.Claims;

namespace MesaMohloane.API.Attributes
{
    /// <summary>
    /// Ensures that the current user is the owner/creator of the resource being modified.
    /// Used for write operations (PUT, DELETE) on contractor-owned resources.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class AuthorizeResourceOwnerAttribute : Attribute, IAsyncAuthorizationFilter
    {
        private readonly string _parameterName;

        public AuthorizeResourceOwnerAttribute(string parameterName = "id")
        {
            _parameterName = parameterName;
        }

        public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            var userId = context.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                context.Result = new UnauthorizedResult();
                return;
            }

            // Extract resource ID from route
            if (!context.RouteData.Values.TryGetValue(_parameterName, out var resourceIdObj))
            {
                context.Result = new BadRequestObjectResult("Resource ID not found in route.");
                return;
            }

            if (!int.TryParse(resourceIdObj?.ToString(), out var resourceId))
            {
                context.Result = new BadRequestObjectResult("Invalid resource ID format.");
                return;
            }

            // Verify ownership
            var dbContext = context.HttpContext.RequestServices
                .GetService(typeof(MesaMohloane.API.Data.ApplicationDbContext)) 
                as MesaMohloane.API.Data.ApplicationDbContext;

            if (dbContext == null)
            {
                context.Result = new StatusCodeResult(500);
                return;
            }

            bool isOwner = false;

            // Determine resource type from controller name
            var controllerName = context.RouteData.Values["controller"]?.ToString() ?? "";
            
            if (_parameterName == "proposalId" || controllerName == "Proposals")
            {
                var proposal = await dbContext.Proposals.FindAsync(resourceId);
                isOwner = proposal?.ContractorId == userId;
            }
            else if (controllerName == "Invoices")
            {
                var invoice = await dbContext.Invoices.FindAsync(resourceId);
                if (invoice != null)
                {
                    isOwner = invoice.ContractorId == userId;
                }
            }

            if (!isOwner)
            {
                string pId = "null";
                string cId = "null";
                if (_parameterName == "proposalId" || controllerName == "Proposals")
                {
                    var p = await dbContext.Proposals.FindAsync(resourceId);
                    pId = p?.Id.ToString() ?? "null";
                    cId = p?.ContractorId ?? "null";
                }
                context.Result = new ObjectResult($"Forbidden. User: {userId}, Contractor: {cId}, ResId: {resourceId}, Param: {_parameterName}") { StatusCode = 403 };
            }
        }
    }
}
