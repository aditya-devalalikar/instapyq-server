using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using pqy_server.Constants;
using pqy_server.Data;
using pqy_server.Models.Roles;
using pqy_server.Shared;

namespace pqy_server.Controllers
{
    [Authorize(Roles = RoleConstant.Admin)] // 🔐 Admin-only access
    [ApiController]
    [Route("api/[controller]")]
    public class RolesController : ControllerBase
    {
        private readonly AppDbContext _context;

        public RolesController(AppDbContext context)
        {
            _context = context;
        }

        // 📋 GET: /api/roles
        // Returns all roles with ID and name
        [HttpGet]
        public async Task<IActionResult> GetAllRoles()
        {
            var roles = await _context.Roles
                .Select(r => new { r.RoleId, r.RoleName })
                .ToListAsync();

            return Ok(ApiResponse<object>.Success(roles));
        }

        // ➕ POST: /api/roles
        // Create a new role with a unique name
        [HttpPost]
        public async Task<IActionResult> CreateRole([FromBody] CreateRoleRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.RoleName))
                return BadRequest(ApiResponse<string>.Failure(ResultCode.ValidationError, "Role name is required."));

            var exists = await _context.Roles.AnyAsync(r => r.RoleName == request.RoleName);
            if (exists)
                return BadRequest(ApiResponse<string>.Failure(ResultCode.Conflict, "Role already exists."));

            var role = new Role
            {
                RoleName = request.RoleName
            };

            _context.Roles.Add(role);
            await _context.SaveChangesAsync();

            return Ok(ApiResponse<object>.Success(role, "Role created successfully."));
        }

        // ✏️ PUT: /api/roles/{id}
        // Update an existing role's name (except for Admin role)
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateRole(int id, [FromBody] UpdateRoleRequest request)
        {
            var role = await _context.Roles.FindAsync(id);
            if (role == null)
                return NotFound(ApiResponse<string>.Failure(ResultCode.NotFound, "Role not found."));

            // Protect Admin role from modification
            if (role.RoleId == 1)
                return BadRequest(ApiResponse<string>.Failure(ResultCode.Forbidden, "Admin role name cannot be modified."));

            if (string.IsNullOrWhiteSpace(request.RoleName))
                return BadRequest(ApiResponse<string>.Failure(ResultCode.ValidationError, "Role name is required."));

            role.RoleName = request.RoleName;
            await _context.SaveChangesAsync();

            return Ok(ApiResponse<object>.Success(role, "Role updated successfully."));
        }

        // ❌ DELETE: /api/roles/{id}
        // Deletes a role if not Admin and not assigned to any user
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteRole(int id)
        {
            var role = await _context.Roles.FindAsync(id);
            if (role == null)
                return NotFound(ApiResponse<string>.Failure(ResultCode.NotFound, "Role not found."));

            if (role.RoleId == 1) // Protect Admin role
                return BadRequest(ApiResponse<string>.Failure(ResultCode.Forbidden, "Admin role cannot be deleted."));

            var isUsed = await _context.Users.AnyAsync(u => u.RoleId == id);
            if (isUsed)
                return BadRequest(ApiResponse<string>.Failure(ResultCode.Conflict, "Cannot delete a role that is assigned to users."));

            _context.Roles.Remove(role);
            await _context.SaveChangesAsync();

            return Ok(ApiResponse<string>.Success("Role deleted successfully."));
        }
    }
}
