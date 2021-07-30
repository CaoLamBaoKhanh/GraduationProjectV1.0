﻿using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using TechShopSolution.AdminApp.Models;
using TechShopSolution.ApiIntegration;

namespace TechShopSolution.AdminApp.Controllers
{ 
    [Authorize]
    public class HomeController : Controller
    {
        private readonly IOrderApiClient _orderApiClient;
        private readonly IProductApiClient _productApiClient;

        public HomeController(IOrderApiClient orderApiClient, IProductApiClient productApiClient)
        {
            _orderApiClient = orderApiClient;
            _productApiClient = productApiClient;
        }

        public async Task<IActionResult> Index()
        {
            var orderStatistics = await _orderApiClient.GetOrderStatistics();
            var productRanking = await _productApiClient.GetProductViewRanking(10);
            return View(new DashBoardViewModel() {
                OrderStatistics = orderStatistics.ResultObject,
                viewRanking = productRanking,
            }); 
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
