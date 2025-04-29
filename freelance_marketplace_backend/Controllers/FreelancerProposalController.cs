﻿

using freelance_marketplace_backend.Data;
using freelance_marketplace_backend.Interfaces;
using freelance_marketplace_backend.Models.Dtos;
using freelance_marketplace_backend.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;

namespace freelance_marketplace_backend.Controllers
{
	[Route("api/[controller]")]
	[ApiController]
	public class FreelancerProposalController : ControllerBase
	{
		private readonly IProjectService _projectService;
		private readonly IProposalService _proposalService;
		private readonly IDistributedCache _cache;

		public FreelancerProposalController(IProjectService projectService, IProposalService proposalService, IDistributedCache cache)
		{
			_projectService = projectService;
			_proposalService = proposalService;
			_cache = cache;
		}

		// GET: api/FreelancerProposal/{projectId}
		[HttpGet("{projectId}")]
		public async Task<IActionResult> GetProjectById(int projectId)
		{
			try
			{
				var cacheKey = $"project:{projectId}";
				var cachedProject = await _cache.GetStringAsync(cacheKey);

				ProjectDetailsDto projectDetails;

				if (!string.IsNullOrEmpty(cachedProject))
				{
					// Found in cache
					projectDetails = JsonSerializer.Deserialize<ProjectDetailsDto>(cachedProject);
				}
				else
				{
					// Not found, get from service
					projectDetails = await _projectService.GetProjectDetailsAsync(projectId);

					if (projectDetails == null)
					{
						return NotFound($"Project with ID {projectId} not found.");
					}

					// Save to cache
					var options = new DistributedCacheEntryOptions
					{
						AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30)
					};
					var serializedData = JsonSerializer.Serialize(projectDetails);
					await _cache.SetStringAsync(cacheKey, serializedData, options);
				}

				return Ok(projectDetails);
			}
			catch (Exception ex)
			{
				return StatusCode(500, $"Internal server error: {ex.Message}");
			}
		}

		// POST: api/FreelancerProposal/{projectId}/proposals
		[HttpPost("{projectId}/proposals")]
		public async Task<IActionResult> SubmitProposal(int projectId, [FromBody] CreateProposalDto proposalDto)


		{
			if (proposalDto == null)
			{
				return BadRequest("Proposal data is required.");
			}

			try
			{
				var proposal = await _proposalService.SubmitProposalAsync(projectId, proposalDto);

				// Invalidate (remove) cache for this project
				var cacheKey = $"project:{projectId}";
				await _cache.RemoveAsync(cacheKey);

				return CreatedAtAction(nameof(SubmitProposal), new { projectId = projectId, proposalId = proposal.ProposalId }, proposal);
			}
			catch (KeyNotFoundException)
			{
				return NotFound("Project not found.");
			}
			catch (Exception ex)
			{
				return StatusCode(500, $"Internal server error: {ex.Message}");
			}
		}
	}
}
