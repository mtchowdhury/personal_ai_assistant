using System.Linq;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace CmdNext.Api.Infrastructure
{
    /// <summary>
    /// Supplies the value for the {version} route parameter so Swagger UI can
    /// issue requests against the versioned routes without the user typing it.
    /// </summary>
    public sealed class ApiVersionOperationFilter : IOperationFilter
    {
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            var versionParameter = operation.Parameters?
                .FirstOrDefault(p => p.Name == "version");

            if (versionParameter is null)
            {
                return;
            }

            var apiVersion = context.ApiDescription.GroupName;
            versionParameter.Required = true;
            versionParameter.Schema.Default = new OpenApiString(apiVersion);
            versionParameter.Description = "API version";
        }
    }
}
