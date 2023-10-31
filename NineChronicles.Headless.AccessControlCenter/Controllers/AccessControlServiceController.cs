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
        public ActionResult<bool> IsAccessDenied(string address)
        {
            return _accessControlService.IsAccessDenied(new Address(address));
        }

        [HttpPost("entries/{address}/deny")]
        public ActionResult DenyAccess(string address)
        {
            _accessControlService.DenyAccess(new Address(address));
            return Ok();
        }

        [HttpPost("entries/{address}/allow")]
        public ActionResult AllowAccess(string address)
        {
            _accessControlService.AllowAccess(new Address(address));
            return Ok();
        }

        [HttpPost("entries/add-tx-quota/{address}/{quota}")]
        public ActionResult AddTxQuota(string address, string quota)
        {
            var txQuota = Convert.ToInt32(quota);
            _accessControlService.AddTxQuota(new Address(address), txQuota);
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
            return _accessControlService
                .ListBlockedAddresses(offset, limit)
                .Select(a => a.ToString())
                .ToList();
        }
    }
}
