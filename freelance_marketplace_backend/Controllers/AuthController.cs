﻿using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;
using freelance_marketplace_backend.Data;
using freelance_marketplace_backend.Models.Dtos;
using AdvancedAjax.Models.Dtos;

namespace freelance_marketplace_backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly FreelancingPlatformContext _context;
        private readonly IDistributedCache _cache;

        public AuthController(FreelancingPlatformContext context, IDistributedCache cache)
        {
            _context = context;
            _cache = cache;
        }

        [HttpGet("users/{userId}")]
        public async Task<ActionResult<UserProfileDto>> GetUserById(string userId)
        {
            var cacheKey = $"UserProfile_{userId}";
            var cachedUser = await _cache.GetStringAsync(cacheKey);

            if (!string.IsNullOrEmpty(cachedUser))
            {
                var userProfileDto = JsonSerializer.Deserialize<UserProfileDto>(cachedUser);
                return Ok(userProfileDto);
            }

            var user = await _context.Users
                .Where(u => u.Usersid == userId && (u.IsDeleted == null || u.IsDeleted == false))
                .Include(u => u.UsersSkills).ThenInclude(us => us.Skill)
                .Include(u => u.Projects)
                .ThenInclude(p => p.ProjectSkills)
                .ThenInclude(ps => ps.Skill)
                .FirstOrDefaultAsync();

            if (user == null)
            {
                return NotFound();
            }

            var userProfile = new UserProfileDto
            {
                UserId = user.Usersid,
                Name = user.Name,
                Email = user.Email,
                Phone = user.phone,
                ImageUrl = user.ImageUrl,
                AboutMe = user.AboutMe,
                Rating = user.Rating,
                Balance = user.Balance,
                Skills = user.UsersSkills.Select(us => new SkillDto
                {
                    SkillId = us.Skill.SkillId,
                    Skill = us.Skill.Skill1,
                    Category = us.Skill.Category
                }).ToList(),
                Projects = user.Projects
                    .Where(p => p.Status == "Completed" && p.FreelancerId == userId)
                    .Select(p => new ProfileProjectDto
                    {
                        ProjectId = p.ProjectId,
                        Title = p.Title,
                        ProjectOverview = p.Overview,
                        RequiredTasks = p.RequiredTasks,
                        AdditionalNotes = p.AdditionalNotes,
                        Budget = p.Budget,
                        Deadline = p.Deadline,
                        Status = p.Status,
                        CreatedAt = p.CreatedAt,
                        Skills = p.ProjectSkills.Select(ps => new SkillDto
                        {
                            SkillId = ps.Skill.SkillId,
                            Skill = ps.Skill.Skill1,
                            Category = ps.Skill.Category
                        }).ToList()
                       
                    }).ToList()
            };

            // Save to Redis cache
            var cacheOptions = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(30) //
            };

            var serializedUserProfile = JsonSerializer.Serialize(userProfile);
            await _cache.SetStringAsync(cacheKey, serializedUserProfile, cacheOptions);

            return Ok(userProfile);
        }

        [HttpPut("users/{userId}/balance/change")]
        public async Task<IActionResult> ChangeBalance(string userId, [FromBody] BalanceChangeDto request)
        {
            if (request == null)
                return BadRequest("Request body is missing.");

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Usersid == userId);

            if (user == null)
                return NotFound("User not found.");

            user.Balance += request.Amount;

            _context.Users.Update(user);
            await _context.SaveChangesAsync();

            // Invalidate cache after balance change
            var cacheKey = $"UserProfile_{userId}";
            await _cache.RemoveAsync(cacheKey);

            return Ok(new { message = "Balance updated successfully.", newBalance = user.Balance });
        }
    }
}
