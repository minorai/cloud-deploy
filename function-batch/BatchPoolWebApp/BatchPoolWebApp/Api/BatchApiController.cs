﻿

namespace BatchPoolWebApp.Api
{
    using BatchPoolWebApp.Services;
    using Microsoft.AspNetCore.Mvc;
    using Newtonsoft.Json.Linq;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    [Produces("application/json")]
    [Route("api/[controller]")]
    public class BatchApiController : Controller
    {
        private readonly IBatchService _batchService;

        public BatchApiController(IBatchService batchService)
        {
            _batchService = batchService;
        }

        // GET: api/BatchApi
        [HttpGet]
        public IEnumerable<string> Get()
        {
            return new string[] { "value1", "value2" };
        }

        // GET: api/BatchApi/5
        [HttpGet("{id}", Name = "Get")]
        public string Get(int id)
        {
            return "value";
        }

        // POST: api/BatchApi
        [HttpPost]
        [Route("api/Create3DSTKDeployment")]
        public async Task<IActionResult> Post(
            [FromBody] JObject jsonBody)
        {
            var signalingServer = jsonBody["signalingServer"]?.ToObject<string>();
            var signalingServerPort = jsonBody["signalingServerPort"]?.ToObject<int>();
            string turnPoolId = jsonBody["turnPoolId"]?.ToObject<string>() ?? "DefaultTurnPool";
            string renderingPoolId = jsonBody["renderingPoolId"]?.ToObject<string>() ?? "DefaultRenderingPool";
            int dedicatedTurnNodes = jsonBody["dedicatedTurnNodes"]?.ToObject<int>() ?? 1;
            int dedicatedRenderingNodes = jsonBody["dedicatedRenderingNodes"]?.ToObject<int>() ?? 1;
            int maxUsersPerRenderingNode = jsonBody["maxUsersPerRenderingNode"]?.ToObject<int>() ?? -1;
            string renderingJobId = jsonBody["renderingJobId"]?.ToObject<string>() ?? "3DSTKRenderingJob";

            // Signaling URL and port is required
            if (string.IsNullOrEmpty(signalingServer) || !signalingServerPort.HasValue)
                return BadRequest("Signaling is required");

            // Each pool must have at least one dedicated node
            if (dedicatedRenderingNodes < 1 || dedicatedTurnNodes < 1)
                return BadRequest("Pools must have at least one dedicated node");

            // Each rendering node must have at least one max user
            if (maxUsersPerRenderingNode < 1)
                return BadRequest("Rendering nodes must have at least one max user");

            var linuxPool = _batchService.GetPoolsInBatch().FirstOrDefault((s) => s.Id == turnPoolId);
            if (linuxPool == null)
            {
                await _batchService.CreateLinuxPool(turnPoolId, dedicatedTurnNodes);
            }

            var renderingPool = _batchService.GetPoolsInBatch().FirstOrDefault((s) => s.Id == renderingPoolId);
            if (renderingPool == null)
            {
                await _batchService.CreateWindowsPool(renderingPoolId, dedicatedRenderingNodes);

                await _batchService.CreateJobAsync(renderingJobId, renderingPoolId);
                for (var i = 0; i < dedicatedRenderingNodes; i++)
                {
                    await _batchService.AddWindowsTasksAsync(turnPoolId, renderingJobId, signalingServer, signalingServerPort.Value, maxUsersPerRenderingNode);
                }

                await _batchService.MonitorTasks(renderingJobId, new TimeSpan(0, 20, 0));
            }
            
            return Ok();
        }

        // PUT: api/BatchApi/5
        [HttpPut("{id}")]
        public void Put(int id, [FromBody]string value)
        {
        }

        // DELETE: api/ApiWithActions/5
        [HttpDelete("{id}")]
        public void Delete(int id)
        {
        }
    }
}