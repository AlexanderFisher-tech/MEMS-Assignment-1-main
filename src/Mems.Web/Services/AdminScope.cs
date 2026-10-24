/*
 * Project: Municipal Elections Management System (MEMS)
 * Course: COMP4976
 * Authors: Abdullah Rehan, Alex Fisher (project contributors)
 * Description: Restricts administrative data access to permitted municipalities.
 */
using System.Security.Claims;
using Mems.Web.Data;
using Mems.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace Mems.Web.Services;

/// <summary>Builds SQL-composable queries for Super Admin or assigned municipality memberships.</summary>
public class AdminScope(ApplicationDbContext db, IHttpContextAccessor accessor)
{
    public const string SuperRole = "Super Admin";
    public const string MunicipalRole = "Municipality Admin";
    public bool IsSuper => accessor.HttpContext?.User.IsInRole(SuperRole) == true;
    /// <summary>Resolves permitted municipality IDs from stored Identity role memberships, not submitted IDs.</summary>
    private IQueryable<int> MunicipalityIds
    {
        get
        {
            var userId = accessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
            return from membership in db.UserRoles
                   join role in db.Roles on membership.RoleId equals role.Id
                   where membership.UserId == userId && role.MunicipalityId != null
                   select role.MunicipalityId!.Value;
        }
    }
    // Municipality admins need a linked role for data access; Super Admin bypasses these filters.
    public IQueryable<Municipality> Municipalities => IsSuper ? db.Municipalities : db.Municipalities.Where(x => MunicipalityIds.Contains(x.MunicipalityId));
    public IQueryable<Position> Positions => IsSuper ? db.Positions : db.Positions.Where(x => MunicipalityIds.Contains(x.MunicipalityId));
    public IQueryable<Candidate> Candidates => IsSuper ? db.Candidates : db.Candidates.Where(x => MunicipalityIds.Contains(x.Position.MunicipalityId));
    /// <summary>Checks submitted parent ownership with the same scope used for list and detail queries.</summary>
    public Task<bool> CanManage(int municipalityId) => Municipalities.AnyAsync(x => x.MunicipalityId == municipalityId);
}
