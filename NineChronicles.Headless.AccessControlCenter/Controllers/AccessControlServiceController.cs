using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using NineChronicles.Headless.AccessControlCenter.AccessControlService;
using System.Linq;
using Libplanet.Crypto;

namespace NineChronicles.Headless.AccessControlCenter.Controllers
{
    [ApiController]
    public class AccessControlServiceController : ControllerBase
    {
        private readonly IMutableAccessControlService _accessControlService;

        public AccessControlServiceController(IMutableAccessControlService accessControlService)
        {
            _accessControlService = accessControlService;
        }

        [HttpGet("entries/{address}")]
        public ActionResult<int?> GetTxQuota(string address)
        {
            return _accessControlService.GetTxQuota(new Address(address));
        }

        [HttpPost("entries/add-tx-quota/{address}/{quota:int}")]
        public ActionResult AddTxQuota(string address, int quota)
        {
            var maxQuota = 10;
            if (quota > maxQuota)
            {
                return BadRequest($"The quota cannot exceed {maxQuota}.");
            }

            _accessControlService.AddTxQuota(new Address(address), quota);
            return Ok();
        }

        [HttpPost("entries/remove-tx-quota/{address}")]
        public ActionResult RemoveTxQuota(string address)
        {
            _accessControlService.RemoveTxQuota(new Address(address));
            return Ok();
        }

        [HttpGet("entries")]
        public ActionResult<List<string>> ListBlockedAddresses(int offset, int limit)
        {
            var maxLimit = 10;
            if (_accessControlService is MutableRedisAccessControlService)
            {
                maxLimit = 10;
            }
            else if (_accessControlService is MutableSqliteAccessControlService)
            {
                maxLimit = 100;
            }
            if (limit > maxLimit)
            {
                return BadRequest($"The limit cannot exceed {maxLimit}.");
            }

            return _accessControlService
                .ListTxQuotaAddresses(offset, limit)
                .Select(a => a.ToString())
                .ToList();
        }
    }
}
