using System.Threading.Tasks;

using Dapper;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Newtonsoft.Json.Linq;

using PluralKit.Core;

namespace PluralKit.API
{
    [ApiController]
    [ApiVersion("1.0")]
    [Route( "v{version:apiVersion}/m" )]
    public class MemberController: ControllerBase
    {
        private IDatabase _db;
        private IAuthorizationService _auth;

        public MemberController(IAuthorizationService auth, IDatabase db)
        {
            _auth = auth;
            _db = db;
        }

        [HttpGet("{hid}")]
        public async Task<ActionResult<JObject>> GetMember(string hid)
        {
            var member = await _db.Execute(conn => conn.QueryMemberByHid(hid));
            if (member == null) return NotFound("Member not found.");

            return Ok(member.ToJson(User.ContextFor(member)));
        }

        [HttpPost]
        [Authorize]
        public async Task<ActionResult<JObject>> PostMember([FromBody] JObject properties)
        {
            var system = User.CurrentSystem();
            if (!properties.ContainsKey("name"))
                return BadRequest("Member name must be specified.");

            await using var conn = await _db.Obtain();

            // Enforce per-system member limit
            var memberCount = await conn.QuerySingleAsync<int>("select count(*) from members where system = @System", new {System = system});
            if (memberCount >= Limits.MaxMemberCount)
                return BadRequest($"Member limit reached ({memberCount} / {Limits.MaxMemberCount}).");

            var member = await conn.CreateMember(system, properties.Value<string>("name"));
            MemberPatch patch;
            try
            {
                patch = JsonModelExt.ToMemberPatch(properties);
            }
            catch (JsonModelParseError e)
            {
                return BadRequest(e.Message);
            }
            
            member = await conn.UpdateMember(member.Id, patch);
            return Ok(member.ToJson(User.ContextFor(member)));
        }

        [HttpPatch("{hid}")]
        [Authorize]
        public async Task<ActionResult<JObject>> PatchMember(string hid, [FromBody] JObject changes)
        {
            await using var conn = await _db.Obtain();

            var member = await conn.QueryMemberByHid(hid);
            if (member == null) return NotFound("Member not found.");
            
            var res = await _auth.AuthorizeAsync(User, member, "EditMember");
            if (!res.Succeeded) return Unauthorized($"Member '{hid}' is not part of your system.");

            MemberPatch patch;
            try
            {
                patch = JsonModelExt.ToMemberPatch(changes);
            }
            catch (JsonModelParseError e)
            {
                return BadRequest(e.Message);
            }
            
            var newMember = await conn.UpdateMember(member.Id, patch);
            return Ok(newMember.ToJson(User.ContextFor(newMember)));
        }
        
        [HttpDelete("{hid}")]
        [Authorize]
        public async Task<ActionResult> DeleteMember(string hid)
        {
            await using var conn = await _db.Obtain();

            var member = await conn.QueryMemberByHid(hid);
            if (member == null) return NotFound("Member not found.");
            
            var res = await _auth.AuthorizeAsync(User, member, "EditMember");
            if (!res.Succeeded) return Unauthorized($"Member '{hid}' is not part of your system.");

            await conn.DeleteMember(member.Id);
            return Ok();
        }
    }
}
