using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Repositories.Interfaces;

namespace MVC.Controllers
{
    [Authorize]
    [Route("Role")]
    public class RoleController : Controller
    {
        private readonly IRoleInterface _roleRepository;

        public RoleController(IRoleInterface roleRepository)
        {
            _roleRepository = roleRepository;
        }

        [HttpPost("SwitchRole")]
        public async Task<IActionResult> SwitchRole(int roleId)
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!int.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized(new
                {
                    success = false,
                    message = "Unable to identify the current user."
                });
            }

            var result = await _roleRepository.SwitchActiveRoleAsync(userId, roleId);

            if (!result.Success)
            {
                return BadRequest(new
                {
                    success = false,
                    message = result.Message
                });
            }

            return Ok(new
            {
                success = true,
                message = result.Message
            });
        }
    }
}
