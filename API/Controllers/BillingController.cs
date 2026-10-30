// API/Controllers/BillingController.cs
using System;
using Microsoft.AspNetCore.Mvc;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;

namespace API.Controllers
{
    [ApiController]
    [Route("api/billing")]
    public class BillingController : ControllerBase
    {
        private readonly IBillingEngine _billingEngine;

        public BillingController(IBillingEngine billingEngine)
        {
            _billingEngine = billingEngine;
        }

        [HttpPost("calculate-turn-fee")]
        public IActionResult CalculateTurnFee([FromBody] ParkingSession request)
        {
            try
            {
                decimal fee = _billingEngine.CalculateFee(request);
                return Ok(new { SessionId = request.SessionId, FeeAmount = fee, Currency = "VND" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Error = ex.Message });
            }
        }

        [HttpGet("monthly-fee-quote")]
        public IActionResult GetMonthlyFeeQuote([FromQuery] VehicleType vehicleType, [FromQuery] ParkingZone zone)
        {
            try
            {
                decimal fee = _billingEngine.CalculateMonthlyFee(vehicleType, zone);
                return Ok(new { VehicleType = vehicleType.ToString(), Zone = zone.ToString(), FeeAmount = fee, Currency = "VND/tháng" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Error = ex.Message });
            }
        }
    }
}