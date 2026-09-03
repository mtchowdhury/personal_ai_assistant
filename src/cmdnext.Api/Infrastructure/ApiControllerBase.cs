using System;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace CmdNext.Api.Infrastructure
{
    /// <summary>
    /// Base controller exposing the authenticated user's id. Replaces the
    /// hardcoded default-user Guid that was used while auth was disabled.
    /// </summary>
    public abstract class ApiControllerBase : ControllerBase
    {
        /// <summary>
        /// The current user's id, taken from the "sub" claim.
        /// </summary>
        protected Guid CurrentUserId
        {
            get
            {
                if (!TryGetUserId(out var userId))
                {
                    throw new UnauthorizedAccessException("The request is not associated with an authenticated user.");
                }

                return userId;
            }
        }

        protected bool TryGetUserId(out Guid userId)
        {
            // "sub" is the claim the token is issued with; NameIdentifier is checked
            // as a fallback in case inbound claim mapping is ever re-enabled.
            var raw = User.FindFirst("sub")?.Value
                      ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            return Guid.TryParse(raw, out userId);
        }
    }
}
