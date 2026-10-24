/*
 * Project: Municipal Elections Management System (MEMS)
 * Course: COMP4976
 * Authors: Abdullah Rehan, Alex Fisher (project contributors)
 * Description: Persists anonymous candidate selections in the current browser session.
 */
using System.Text.Json;

namespace Mems.Web.Services;

/// <summary>Stores only unique candidate IDs; selections are independent of administrator accounts.</summary>
public class VotingSelection(IHttpContextAccessor accessor)
{
    private ISession Session => accessor.HttpContext!.Session;
    /// <summary>Returns a detached set so callers can enforce seat limits before saving the session.</summary>
    public HashSet<int> Read()
    {
        var json = Session.GetString("SelectedCandidates");
        return json is null ? [] : JsonSerializer.Deserialize<HashSet<int>>(json) ?? [];
    }
    public void Save(HashSet<int> ids) => Session.SetString("SelectedCandidates", JsonSerializer.Serialize(ids));
}
